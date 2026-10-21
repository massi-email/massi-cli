namespace EmailSweepAutomation;

/// <summary>Why a rule is invalid. See <see cref="RuleProblem"/>.</summary>
public enum RuleProblemKind
{
    NoCondition,
    AddressAndDomain,
    BadDomain,
    NoAction,
    SeveralActions,
    MissingFolder,
    KeepsEverything,
    NoKeep,
    NegativeKeepRecent,
    NegativeKeepRecentTime,
    BranchedRuleHasAction,
    BranchedRuleHasKeep,
    TooManyBranches,
    UnlabelledBranch,
    UnknownBranchAction,
    AlertBranch,
    CatchAllNotLast,
    CatchAllDeletes,
    BadSender,
    BlankSubjectKeyword,
    FolderWithoutMove,
}

/// <summary>
/// The first thing wrong with a rule. Typed so each host words it for its own reader — the CLI
/// prints <see cref="Message"/>, the cloud maps <see cref="Kind"/> to a sentence for users —
/// without re-deriving the checks or matching on message strings.
/// </summary>
/// <param name="Branch">The offending branch's label, or null when the problem is the rule's own
/// (for a flat rule, that includes its action and keep).</param>
/// <param name="Detail">The offending value, where one helps (a bad sender entry).</param>
public sealed record RuleProblem(RuleProblemKind Kind, string? Branch = null, string? Detail = null)
{
    /// <summary>Developer-facing: names the rule file's properties.</summary>
    public string Message => Kind switch
    {
        RuleProblemKind.NoCondition => "At least one of FromAddress, FromDomain or SubjectContains must be specified",
        RuleProblemKind.AddressAndDomain => "Only one of FromAddress or FromDomain can be specified",
        RuleProblemKind.BadDomain => "FromDomain must be a domain such as shop.com",
        RuleProblemKind.NoAction => "At least one of Delete, MoveToFolder or Alert must be specified",
        RuleProblemKind.SeveralActions => "Only one of Delete, MoveToFolder or Alert can be specified",
        RuleProblemKind.MissingFolder => Branch is null ? "MoveToFolder must name a folder" : $"Branch '{Branch}' must name a folder",
        RuleProblemKind.KeepsEverything => Branch is null
            ? "KeepRecent and KeepRecentTime cannot be used with Alert"
            : "A leave branch keeps everything, so it has no KeepRecent or KeepRecentTime",
        RuleProblemKind.NoKeep => Branch is null
            ? "At least one of KeepRecent or KeepRecentTime must be specified"
            : $"Branch '{Branch}' needs KeepRecent or KeepRecentTime",
        RuleProblemKind.NegativeKeepRecent => "KeepRecent cannot be negative",
        RuleProblemKind.NegativeKeepRecentTime => "KeepRecentTime cannot be negative",
        RuleProblemKind.BranchedRuleHasAction => "A rule with branches cannot have an action of its own",
        RuleProblemKind.BranchedRuleHasKeep => "A rule with branches cannot have KeepRecent or KeepRecentTime of its own",
        RuleProblemKind.TooManyBranches => $"A rule can have at most {SweepRuleExtensions.MaxBranches} branches",
        RuleProblemKind.UnlabelledBranch => "Every branch needs a label",
        RuleProblemKind.UnknownBranchAction => $"Branch '{Branch}' has an unknown action",
        RuleProblemKind.AlertBranch => $"Branch '{Branch}' cannot alert; only a rule without branches can",
        RuleProblemKind.CatchAllNotLast => "Only the last branch can have no conditions",
        RuleProblemKind.CatchAllDeletes => "The last branch cannot delete everything else",
        RuleProblemKind.BadSender => $"Branch '{Branch}' has a sender that is not user@host, @host, @*.host or a pattern (name*@host, *name@host, *name*@host): {Detail}",
        RuleProblemKind.BlankSubjectKeyword => $"Branch '{Branch}' has a blank subject keyword",
        RuleProblemKind.FolderWithoutMove => "Only a move branch can name a folder",
        _ => $"Invalid rule ({Kind})",
    };
}

public static class SweepRuleExtensions
{
    public const int MaxBranches = 12;

    /// <summary>The first problem with this rule, or null when it is valid.</summary>
    public static RuleProblem? Validate(this SweepRule rule)
    {
        // Blank, not just null. A present-but-empty criterion is treated as "no criterion"
        // by RuleEngine.TryMatchCriteria, so a rule carrying one matches every message it
        // is shown. Callers that build rules from a form null their blanks; callers that
        // build them from JSON do not.
        if (string.IsNullOrWhiteSpace(rule.FromAddress) && string.IsNullOrWhiteSpace(rule.FromDomain)
            && string.IsNullOrWhiteSpace(rule.SubjectContains))
            return new(RuleProblemKind.NoCondition);
        if (!string.IsNullOrWhiteSpace(rule.FromAddress) && !string.IsNullOrWhiteSpace(rule.FromDomain))
            return new(RuleProblemKind.AddressAndDomain);
        if (!string.IsNullOrWhiteSpace(rule.FromDomain) && !SenderMatch.IsValidHost(rule.FromDomain.Trim()))
            return new(RuleProblemKind.BadDomain);

        return rule.Branches is { Count: > 0 } branches ? ValidateBranches(rule, branches) : ValidateFlat(rule);
    }

    public static (bool isValid, string? error) IsRuleValid(this SweepRule rule) =>
        rule.Validate() is { } problem ? (false, problem.Message) : (true, null);

    private static RuleProblem? ValidateFlat(SweepRule rule)
    {
        var actionsSpecified = 0;
        if (rule.Delete is not null) actionsSpecified++;
        // A blank folder name is not a move. Counting it as one let a rule past this check
        // and into MoveMessagesAsync, whose own guard is IsNullOrEmpty — which "  " clears.
        if (!string.IsNullOrWhiteSpace(rule.MoveToFolder)) actionsSpecified++;
        if (rule.Alert is not null) actionsSpecified++;

        if (actionsSpecified == 0) return new(RuleProblemKind.NoAction);
        if (actionsSpecified > 1) return new(RuleProblemKind.SeveralActions);

        // Validated as the rule reads, not as it evaluates: Delete: false is "a delete" here, so it
        // still needs a keep, exactly as it always has.
        var action = rule.Delete is not null ? SweepBranchAction.Delete
            : rule.Alert is not null ? SweepBranchAction.Alert
            : SweepBranchAction.MoveToFolder;
        return ValidateActionAndKeep(null, action, rule.MoveToFolder, rule.KeepRecent, rule.KeepRecentTime);
    }

    private static RuleProblem? ValidateBranches(SweepRule rule, List<SweepBranch> branches)
    {
        if (rule.DeclaresAction)
            return new(RuleProblemKind.BranchedRuleHasAction);
        if (rule.KeepRecent is not null || rule.KeepRecentTime is not null)
            return new(RuleProblemKind.BranchedRuleHasKeep);
        if (branches.Count > MaxBranches)
            return new(RuleProblemKind.TooManyBranches);

        for (var i = 0; i < branches.Count; i++)
        {
            var b = branches[i];
            if (string.IsNullOrWhiteSpace(b.Label)) return new(RuleProblemKind.UnlabelledBranch);
            if (!Enum.IsDefined(b.Action)) return new(RuleProblemKind.UnknownBranchAction, b.Label);
            if (b.Action == SweepBranchAction.Alert) return new(RuleProblemKind.AlertBranch, b.Label);
            if (b.IsCatchAll && i < branches.Count - 1) return new(RuleProblemKind.CatchAllNotLast, b.Label);
            // With other branches, a catch-all is "this sender's mail we haven't named yet", so
            // deleting it would silently drop new kinds of mail.
            if (b.IsCatchAll && branches.Count > 1 && b.Action == SweepBranchAction.Delete)
                return new(RuleProblemKind.CatchAllDeletes, b.Label);
            if (b.FromAny?.FirstOrDefault(e => !SenderMatch.IsValidEntry(e?.Trim())) is { } badSender)
                return new(RuleProblemKind.BadSender, b.Label, badSender);
            if (b.SubjectContainsAny?.Any(string.IsNullOrWhiteSpace) == true)
                return new(RuleProblemKind.BlankSubjectKeyword, b.Label);

            if (ValidateActionAndKeep(b.Label, b.Action, b.MoveToFolder, b.KeepRecent, b.KeepRecentTime) is { } problem)
                return problem;
        }

        return null;
    }

    /// <summary>
    /// The checks a flat rule and a branch share: a move names a folder and nothing else does,
    /// an action that selects nothing to act on (alert, leave) keeps nothing, and a delete or
    /// move says how much it keeps.
    /// </summary>
    private static RuleProblem? ValidateActionAndKeep(
        string? branch, SweepBranchAction action, string? folder, int? keepRecent, TimeSpan? keepRecentTime)
    {
        if (action is SweepBranchAction.Alert or SweepBranchAction.Leave)
        {
            // Keep-filters mean "spare the newest" — backwards for alerting, and a leave branch
            // already spares everything.
            if (keepRecent is not null || keepRecentTime is not null) return new(RuleProblemKind.KeepsEverything, branch);
            if (!string.IsNullOrWhiteSpace(folder)) return new(RuleProblemKind.FolderWithoutMove, branch);
            return null;
        }

        if (action == SweepBranchAction.MoveToFolder)
        {
            // '/' separates the levels of a destination path (see RuleEngine's folder walk), so
            // a path built only from separators names nothing to move into.
            if (string.IsNullOrWhiteSpace(folder) || SweepFolderPath.Segments(folder).Count == 0)
                return new(RuleProblemKind.MissingFolder, branch);
        }
        else if (!string.IsNullOrWhiteSpace(folder))
        {
            return new(RuleProblemKind.FolderWithoutMove, branch);
        }

        if (keepRecent is null && keepRecentTime is null) return new(RuleProblemKind.NoKeep, branch);

        // Zero is allowed on both keeps and means "act on everything this rule matches" —
        // a real intent ("delete all mail from this sender"), bounded by the criteria above.
        // Negative is not the same thing: Skip(-1) skips nothing and a negative window puts
        // the cutoff in the future, so both are silent "keep none" with nobody asking for it.
        if (keepRecent is < 0) return new(RuleProblemKind.NegativeKeepRecent, branch);
        if (keepRecentTime is { } keep && keep < TimeSpan.Zero) return new(RuleProblemKind.NegativeKeepRecentTime, branch);
        return null;
    }

    /// <summary>
    /// Validates every rule in the set and returns the validation error for each
    /// invalid rule. An empty result means all rules are valid.
    /// </summary>
    public static IReadOnlyList<(SweepRule Rule, string Error)> GetValidationErrors(this IEnumerable<SweepRule> rules) =>
        rules.Select(rule => (Rule: rule, Problem: rule.Validate()))
             .Where(x => x.Problem is not null)
             .Select(x => (x.Rule, x.Problem!.Message))
             .ToList();
}
