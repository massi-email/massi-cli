using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmailSweepAutomation;

/// <summary>
/// Per-rule outcome of evaluating a set of messages: how many messages matched the
/// rule's criteria, and which messages were selected for the rule's action after the
/// KeepRecent / KeepRecentTime filters were applied. <c>MatchedCriteria - Selected.Count</c>
/// is the number of messages the rule kept.
/// </summary>
public sealed record RuleEvaluation(
    SweepRule Rule,
    int MatchedCriteria,
    IReadOnlyList<IMessageSummary> Selected);

/// <summary>
/// Interface for the rule engine that processes emails based on sweep rules.
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Evaluates every rule against the messages, returning a per-rule breakdown
    /// (criteria matches and the messages selected for action). One entry is returned
    /// per rule, including rules that matched nothing.
    /// </summary>
    List<RuleEvaluation> EvaluateRules(
        IEnumerable<IMessageSummary> messages,
        IEnumerable<SweepRule> rules);

    /// <summary>
    /// Gets all messages that match any of the provided rules.
    /// </summary>
    List<(IMessageSummary Message, SweepRule Rule)> GetRuleMatches(
        IEnumerable<IMessageSummary> messages,
        IEnumerable<SweepRule> rules);

    /// <summary>
    /// Deletes messages. Moves them to <paramref name="trashFolder"/> when supplied;
    /// otherwise performs a standard IMAP delete (sets \Deleted, then expunges) — permanent.
    /// </summary>
    Task DeleteMessagesAsync(IEnumerable<IMessageSummary> messages, IMailFolder folder, IMailFolder? trashFolder);

    /// <summary>
    /// Moves messages to their destination folders based on rule configuration.
    /// </summary>
    /// <returns>How many messages were moved.</returns>
    Task<int> MoveMessagesAsync(IEnumerable<(IMessageSummary Message, SweepRule Rule)> matches, IMailFolder folder);
}

/// <summary>
/// The RuleEngine is the core component of the Email Sweep Engine.
/// It is responsible for processing emails based on a set of user-defined rules.
/// </summary>
public class RuleEngine : IRuleEngine
{
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
            // tagging each with the sender address it matched on (used for grouping).
            var matched = new List<(IMessageSummary Message, string SenderKey)>();
            foreach (var message in messageList)
            {
                if (TryMatchCriteria(message, rule, out var senderKey))
                {
                    matched.Add((message, senderKey));
                }
            }

            IEnumerable<IMessageSummary> eligible = matched.Select(x => x.Message);

            // KeepRecent: keep the N most recent messages per matched sender (ranked by
            // date), leaving the older ones eligible for the rule's action.
            if (rule.KeepRecent.HasValue)
            {
                eligible = matched
                    .GroupBy(x => x.SenderKey)
                    .SelectMany(group => group
                        .OrderByDescending(x => x.Message.Date)
                        .Skip(rule.KeepRecent.Value)
                        .Select(x => x.Message));
            }

            // KeepRecentTime: keep messages newer than the cutoff, leaving the older ones.
            if (rule.KeepRecentTime.HasValue)
            {
                var cutoff = DateTimeOffset.Now.Subtract(rule.KeepRecentTime.Value);
                eligible = eligible.Where(m => m.Date <= cutoff);
            }

            evaluations.Add(new RuleEvaluation(rule, matched.Count, eligible.ToList()));
        }

        return evaluations;
    }

    public List<(IMessageSummary Message, SweepRule Rule)> GetRuleMatches(
        IEnumerable<IMessageSummary> messages,
        IEnumerable<SweepRule> rules)
    {
        return EvaluateRules(messages, rules)
            .SelectMany(evaluation => evaluation.Selected.Select(message => (message, evaluation.Rule)))
            .ToList();
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

    public async Task DeleteMessagesAsync(IEnumerable<IMessageSummary> messages, IMailFolder folder, IMailFolder? trashFolder)
    {
        var uids = messages.Select(m => m.UniqueId).ToList();
        if (uids.Count == 0)
            return;

        if (trashFolder is not null)
        {
            await folder.MoveToAsync(uids, trashFolder);
        }
        else
        {
            // No Trash special-folder on this server: standard, permanent IMAP delete.
            await folder.StoreAsync(uids, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Deleted) { Silent = true });
            await folder.ExpungeAsync(uids);
        }
    }

    public async Task<int> MoveMessagesAsync(IEnumerable<(IMessageSummary Message, SweepRule Rule)> matches, IMailFolder folder)
    {
        // Group by destination folder to batch operations
        var byFolder = matches
            .Where(m => !string.IsNullOrWhiteSpace(m.Rule.MoveToFolder))
            .GroupBy(m => m.Rule.MoveToFolder!)
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
            await folder.MoveToAsync(uids, destination);
            moved += uids.Count;
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