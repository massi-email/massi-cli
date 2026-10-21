using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmailSweepAutomation;

/// <summary>
/// Per-rule outcome of evaluating a set of messages: how many messages matched the rule's
/// criteria, and — per branch — which of them each branch received and selected for its action
/// after its keep filters. A flat rule has exactly one branch (see
/// <see cref="SweepRule.EffectiveBranches"/>).
/// </summary>
public sealed record RuleEvaluation(
    SweepRule Rule,
    int MatchedCriteria,
    IReadOnlyList<BranchEvaluation> Branches)
{
    /// <summary>Every branch's selection: what the rule deletes, moves or alerts on. A Leave
    /// branch selects nothing.</summary>
    public IReadOnlyList<IMessageSummary> Selected { get; } = Branches.SelectMany(b => b.Selected).ToList();
}

/// <summary>One branch's share of a rule's candidates, and what it selected from them.</summary>
public sealed record BranchEvaluation(
    SweepBranch Branch,
    int Matched,
    IReadOnlyList<IMessageSummary> Selected);

/// <summary>
/// What a sweep does to the mailbox: the messages to delete, and the messages to move with
/// where each goes. Two lists rather than one list of tagged actions, so a message cannot be
/// described as both or neither.
/// </summary>
public sealed record SweepPlan(
    IReadOnlyList<IMessageSummary> Deletes,
    IReadOnlyList<(IMessageSummary Message, string Folder)> Moves)
{
    public int Count => Deletes.Count + Moves.Count;

    /// <summary>
    /// The one place that reads what a branch does to mail, so hosts never re-derive it from
    /// <see cref="SweepRule.Delete"/> and miss branches. Alert and Leave branches touch nothing.
    /// </summary>
    public static SweepPlan From(IEnumerable<RuleEvaluation> evaluations)
    {
        var branches = evaluations.SelectMany(e => e.Branches).ToList();
        return new(
            branches.Where(b => b.Branch.Action == SweepBranchAction.Delete).SelectMany(b => b.Selected).ToList(),
            branches.Where(b => b.Branch.Action == SweepBranchAction.MoveToFolder)
                .SelectMany(b => b.Selected.Select(m => (m, b.Branch.MoveToFolder!)))
                .ToList());
    }
}

/// <summary>
/// Interface for the rule engine that processes emails based on sweep rules.
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Evaluates every rule against the messages, returning a per-rule, per-branch breakdown
    /// (criteria matches and the messages selected for action). One entry is returned per
    /// rule, including rules that matched nothing.
    /// </summary>
    List<RuleEvaluation> EvaluateRules(
        IEnumerable<IMessageSummary> messages,
        IEnumerable<SweepRule> rules);

    /// <summary>
    /// Deletes messages. Moves them to <paramref name="trashFolder"/> when supplied;
    /// otherwise performs a standard IMAP delete (sets \Deleted, then expunges) — permanent.
    /// Sent in chunks of <see cref="RuleEngine.MaxUidsPerCommand"/>; returns how many were deleted.
    /// </summary>
    /// <param name="progress">
    /// Told the running total of deleted messages after each chunk the server accepts. Called
    /// synchronously, so when a chunk throws, the last value reported is exactly how many were
    /// deleted before it.
    /// </param>
    Task<int> DeleteMessagesAsync(
        IEnumerable<IMessageSummary> messages, IMailFolder folder, IMailFolder? trashFolder,
        IProgress<int>? progress = null);

    /// <summary>
    /// Moves each message to its folder, resolving every destination before the first move.
    /// Sent in chunks of <see cref="RuleEngine.MaxUidsPerCommand"/>; returns how many were moved.
    /// </summary>
    /// <param name="progress">
    /// Told the running total of moved messages, across every destination, after each chunk the
    /// server accepts. Same contract as <see cref="DeleteMessagesAsync"/>'s.
    /// </param>
    Task<int> MoveMessagesAsync(
        IEnumerable<(IMessageSummary Message, string Folder)> moves, IMailFolder folder,
        IProgress<int>? progress = null);
}

/// <summary>
/// The RuleEngine is the core component of the Email Sweep Engine.
/// It is responsible for processing emails based on a set of user-defined rules.
/// </summary>
public class RuleEngine : IRuleEngine
{
    /// <summary>
    /// The most UIDs one mutating IMAP command (MOVE, STORE, EXPUNGE) carries.
    /// </summary>
    /// <remarks>
    /// Gmail refuses a MOVE of a few thousand UIDs with <c>NO System Error (Failure)</c> — not a
    /// dropped connection, a refusal, and a deterministic one: a sweep that sent 6,380 deletes
    /// in one command failed identically on every retry. RFC 3501 sets no limit, but servers
    /// impose their own and do not publish them; 500 is an order of magnitude under where Gmail
    /// refused, and costs a sweep of thousands only a handful of extra round trips. Chunking also
    /// gives the act phase its progress: each accepted chunk is reported, so a failure part-way
    /// says how far the sweep got.
    /// </remarks>
    public const int MaxUidsPerCommand = 500;

    private readonly ILogger<RuleEngine> logger;

    // Logger is optional so the engine stays trivially constructable (e.g. `new RuleEngine()`
    // in tests and the CLI). Hosts that register logging get folder-creation diagnostics.
    public RuleEngine(ILogger<RuleEngine>? logger = null)
        => this.logger = logger ?? NullLogger<RuleEngine>.Instance;

    public List<RuleEvaluation> EvaluateRules(
        IEnumerable<IMessageSummary> messages,
        IEnumerable<SweepRule> rules)
    {
        var messageList = messages.ToList();
        var evaluations = new List<RuleEvaluation>();
        foreach (var rule in rules)
        {
            // Select the messages whose sender/subject satisfy the rule's criteria,
            // tagging each with the sender address (or domain) it matched on (used for grouping).
            var matched = new List<(IMessageSummary Message, string SenderKey)>();
            foreach (var message in messageList)
                if (TryMatchCriteria(message, rule, out var senderKey))
                    matched.Add((message, senderKey));

            evaluations.Add(EvaluateBranches(rule, matched));
        }
        return evaluations;
    }

    /// <summary>KeepRecent keeps the N newest per sender key; KeepRecentTime spares the last span.</summary>
    private static List<IMessageSummary> ApplyKeep(
        IReadOnlyList<(IMessageSummary Message, string SenderKey)> matched, int? keepRecent, TimeSpan? keepRecentTime)
    {
        IEnumerable<IMessageSummary> eligible = matched.Select(x => x.Message);
        if (keepRecent.HasValue)
        {
            eligible = matched
                .GroupBy(x => x.SenderKey)
                .SelectMany(group => group
                    .OrderByDescending(x => x.Message.Date)
                    .Skip(keepRecent.Value)
                    .Select(x => x.Message));
        }
        if (keepRecentTime.HasValue)
        {
            var cutoff = DateTimeOffset.Now.Subtract(keepRecentTime.Value);
            eligible = eligible.Where(m => m.Date <= cutoff);
        }
        return eligible.ToList();
    }

    /// <summary>First match wins; keep filters apply per branch, over what that branch received.</summary>
    private static RuleEvaluation EvaluateBranches(
        SweepRule rule, List<(IMessageSummary Message, string SenderKey)> candidates)
    {
        var branches = rule.EffectiveBranches;
        var routed = branches.Select(_ => new List<(IMessageSummary Message, string SenderKey)>()).ToList();
        foreach (var candidate in candidates)
        {
            var envelope = candidate.Message.Envelope;
            var senders = envelope.From.Mailboxes.Select(m => m.Address);
            for (var i = 0; i < branches.Count; i++)
            {
                if (!branches[i].Matches(senders, envelope.Subject)) continue;
                routed[i].Add(candidate);
                break;
            }
        }

        var results = branches.Select((branch, i) => new BranchEvaluation(
            branch,
            routed[i].Count,
            branch.Action == SweepBranchAction.Leave ? [] : ApplyKeep(routed[i], branch.KeepRecent, branch.KeepRecentTime)));
        return new RuleEvaluation(rule, candidates.Count, results.ToList());
    }

    /// <summary>
    /// Determines whether a message satisfies a rule's selection criteria
    /// (FromAddress and/or SubjectContains). When matched, <paramref name="senderKey"/>
    /// is set to the sender address that matched (or empty for subject-only rules),
    /// which is used to group messages for KeepRecent.
    /// </summary>
    private static bool TryMatchCriteria(IMessageSummary message, SweepRule rule, out string senderKey)
    {
        senderKey = string.Empty;
        string? matchedAddress = null;

        // Check FromAddress first - early exit if no match
        if (!string.IsNullOrEmpty(rule.FromAddress))
        {
            var matchingMailbox = message.Envelope.From.Mailboxes
                .FirstOrDefault(m => m.Address.Contains(rule.FromAddress, StringComparison.OrdinalIgnoreCase));

            if (matchingMailbox == null)
                return false;

            matchedAddress = matchingMailbox.Address;
        }

        if (!string.IsNullOrWhiteSpace(rule.FromDomain))
        {
            var domain = rule.FromDomain.Trim();
            if (!message.Envelope.From.Mailboxes.Any(m => SenderMatch.DomainMatches(m.Address, domain)))
                return false;
            // Keep-N groups by the domain: "keep the 3 newest from shop.com", not 3 per sub-address.
            matchedAddress = domain.ToLowerInvariant();
        }

        // Check SubjectContains - early exit if no match
        if (!string.IsNullOrEmpty(rule.SubjectContains))
        {
            if (message.Envelope.Subject == null ||
                !message.Envelope.Subject.Contains(rule.SubjectContains, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        senderKey = matchedAddress ?? string.Empty;
        return true;
    }

    public async Task<int> DeleteMessagesAsync(
        IEnumerable<IMessageSummary> messages, IMailFolder folder, IMailFolder? trashFolder,
        IProgress<int>? progress = null)
    {
        var uids = messages.Select(m => m.UniqueId).ToList();

        var deleted = 0;
        foreach (var chunk in uids.Chunk(MaxUidsPerCommand))
        {
            if (trashFolder is not null)
            {
                await folder.MoveToAsync(chunk, trashFolder);
            }
            else
            {
                // No Trash special-folder on this server: standard, permanent IMAP delete.
                // Flag and expunge chunk by chunk rather than flagging everything first: if a
                // command fails, at most this one chunk is left flagged \Deleted but still
                // present, instead of the whole remaining selection sitting flagged for the next
                // client that expunges. A chunk only counts once it is expunged — flagged is not
                // deleted, and a re-run selects (and re-flags) whatever is still there. The UID
                // form of EXPUNGE touches only these messages, never mail the user flagged.
                await folder.StoreAsync(chunk, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Deleted) { Silent = true });
                await folder.ExpungeAsync(chunk);
            }

            deleted += chunk.Length;
            progress?.Report(deleted);
        }

        return deleted;
    }

    public async Task<int> MoveMessagesAsync(
        IEnumerable<(IMessageSummary Message, string Folder)> moves, IMailFolder folder,
        IProgress<int>? progress = null)
    {
        // Group by destination folder to batch operations
        var byFolder = moves
            .Where(m => !string.IsNullOrWhiteSpace(m.Folder))
            .GroupBy(m => m.Folder)
            .ToList();

        // Every destination first, before the first message moves. Resolving lazily inside the
        // loop meant a folder the server would not give us surfaced only after the earlier rules
        // had already been applied, leaving the mailbox half-swept with no record of where it
        // stopped.
        var destinations = new List<(IMailFolder Folder, List<UniqueId> Uids)>();
        foreach (var group in byFolder)
        {
            destinations.Add((
                await ResolveFolderAsync(folder, group.Key),
                group.Select(m => m.Message.UniqueId).ToList()));
        }

        var moved = 0;
        foreach (var (destination, uids) in destinations)
        {
            foreach (var chunk in uids.Chunk(MaxUidsPerCommand))
            {
                await folder.MoveToAsync(chunk, destination);
                moved += chunk.Length;
                progress?.Report(moved);
            }
        }

        return moved;
    }

    /// <summary>
    /// Resolves a rule's destination path to a folder, creating the levels that do not exist.
    /// </summary>
    /// <remarks>
    /// Walked one level at a time, because the whole path is not a folder name: MailKit rejects
    /// a name containing the server's hierarchy delimiter, and each server picks its own. This is
    /// also the only place that can tell a user their server cannot hold the name they asked for
    /// — <c>Receipts.Amazon</c> on a Dovecot box that delimits with <c>.</c> — which beats
    /// swallowing the failure and reporting a move that never happened.
    /// </remarks>
    private async Task<IMailFolder> ResolveFolderAsync(IMailFolder parent, string path)
    {
        var segments = SweepFolderPath.Segments(path);
        if (segments.Count == 0)
            throw new InvalidOperationException($"'{path}' does not name a folder to move messages into.");

        var current = parent;
        foreach (var segment in segments)
        {
            var separator = current.DirectorySeparator;
            if (separator != '\0' && segment.Contains(separator))
            {
                throw new InvalidOperationException(
                    $"This mail server separates folders with '{separator}', so it cannot hold a "
                    + $"folder named '{segment}'. Rename the destination, or write it with '/' "
                    + $"between the levels — '{segment.Replace(separator, '/')}' — to nest it.");
            }

            current = await GetOrCreateFolderAsync(current, segment);
        }

        return current;
    }

    private async Task<IMailFolder> GetOrCreateFolderAsync(IMailFolder parent, string name)
    {
        try
        {
            return await parent.GetSubfolderAsync(name);
        }
        catch (FolderNotFoundException)
        {
            logger.LogInformation("Folder {Folder} not found. Creating folder...", name);

            // MailKit types this as IMailFolder?, so a create can report success and hand back
            // nothing. Passing that on makes the move fail as a bare NullReferenceException,
            // naming neither the folder nor the rule that asked for it.
            return await parent.CreateAsync(name, true)
                ?? throw new InvalidOperationException(
                    $"The mail server did not create the folder '{name}'.");
        }
    }
}