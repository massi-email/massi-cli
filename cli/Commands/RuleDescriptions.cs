namespace EmailSweepAutomation.Commands;

/// <summary>A rule as <c>print-rules</c> shows it; null/empty parts are left out.</summary>
public sealed record RuleDescription(string? Matches, string? Action, IReadOnlyList<string> Branches);

/// <summary>
/// Plain-text descriptions of rules and branches, shared by <c>sweep</c> and <c>print-rules</c>
/// so a rule reads the same in both. Unescaped: callers escape for Spectre markup.
/// </summary>
public static class RuleDescriptions
{
    /// <summary>The rule's filter, e.g. <c>From domain shop.com AND Subject ~ sale</c>; null when it has none.</summary>
    public static string? Criteria(SweepRule rule)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(rule.FromAddress)) parts.Add($"From ~ {rule.FromAddress}");
        if (!string.IsNullOrEmpty(rule.FromDomain)) parts.Add($"From domain {rule.FromDomain}");
        if (!string.IsNullOrEmpty(rule.SubjectContains)) parts.Add($"Subject ~ {rule.SubjectContains}");
        return parts.Count > 0 ? string.Join(" AND ", parts) : null;
    }

    /// <summary>A branch's conditions; <c>(everything else)</c> for a catch-all.</summary>
    public static string BranchConditions(SweepBranch branch)
    {
        var parts = new List<string>();
        if (branch.FromAny is { Count: > 0 } from) parts.Add($"From any of {string.Join(", ", from)}");
        if (branch.SubjectContainsAny is { Count: > 0 } subjects) parts.Add($"Subject has any of {string.Join(", ", subjects)}");
        return parts.Count > 0 ? string.Join(" AND ", parts) : "(everything else)";
    }

    /// <summary>What a branch does, with its keep, e.g. <c>Delete (keep 3 newest)</c>.</summary>
    public static string ActionLabel(SweepBranch branch)
    {
        string keep = branch.KeepRecent.HasValue ? $" (keep {branch.KeepRecent} newest)"
            : branch.KeepRecentTime.HasValue ? $" (keep last {branch.KeepRecentTime.Value.TotalDays:0.#}d)"
            : string.Empty;

        return branch.Action switch
        {
            SweepBranchAction.Alert => "Alert",
            SweepBranchAction.Delete => "Delete" + keep,
            SweepBranchAction.MoveToFolder => $"Move → {branch.MoveToFolder}" + keep,
            _ => "Leave",
        };
    }

    /// <summary>
    /// The dry-run table's action cell for a message the branch selected: the same wording as
    /// <see cref="ActionLabel"/>, prefixed with the branch for a branched rule.
    /// </summary>
    public static string ActionsDescription(SweepRule rule, SweepBranch branch) =>
        rule.IsBranched ? $"Branch {branch.Label}: {ActionLabel(branch)}" : ActionLabel(branch);

    /// <summary>
    /// A whole rule for <c>print-rules</c>: its filter, then its action or numbered branches.
    /// Parts the rule does not have are null or empty. Does not validate; the caller does.
    /// </summary>
    public static RuleDescription Describe(SweepRule rule)
    {
        var action = !rule.IsBranched && rule.DeclaresAction ? ActionLabel(rule.EffectiveBranches[0]) : null;

        var branches = new List<string>();
        if (rule.IsBranched)
        {
            var number = 0;
            foreach (var branch in rule.Branches!)
            {
                number++;
                var label = string.IsNullOrWhiteSpace(branch.Label) ? "" : $"{branch.Label}  ";
                branches.Add($"{number}. {label}{BranchConditions(branch)} → {ActionLabel(branch)}");
            }
        }

        return new RuleDescription(Criteria(rule), action, branches);
    }

    /// <summary>The closing count, e.g. <c>5 rules, 1 invalid</c>.</summary>
    public static string Summary(int rules, int invalid) =>
        $"{rules} {(rules == 1 ? "rule" : "rules")}, {invalid} invalid";
}
