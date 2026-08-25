using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailSweepAutomation
{
    public class SweepRule
    {
        public int Id { get; set; }
        public string? FromAddress { get; set; }
        public string? SubjectContains { get; set; }
        public int? KeepRecent { get; set; }
        public TimeSpan? KeepRecentTime { get; set; }
        public bool? Delete { get; set; }
        public string? MoveToFolder { get; set; }
        public bool? Alert { get; set; }
    }

    public static class SweepRuleExtensions
    {
        public static (bool isValid, string? error) IsRuleValid(this SweepRule rule)
        {
            // Blank, not just null. A present-but-empty criterion is treated as "no criterion"
            // by RuleEngine.TryMatchCriteria, so a rule carrying one matches every message it
            // is shown. Callers that build rules from a form null their blanks; callers that
            // build them from JSON do not.
            if (string.IsNullOrWhiteSpace(rule.FromAddress) && string.IsNullOrWhiteSpace(rule.SubjectContains))
            {
                return (false,"At least one of FromAddress or SubjectContains must be specified");
            }

            var actionsSpecified = 0;
            if (rule.Delete is not null) actionsSpecified++;
            // A blank folder name is not a move. Counting it as one let a rule past this check
            // and into MoveMessagesAsync, whose own guard is IsNullOrEmpty — which "  " clears.
            if (!string.IsNullOrWhiteSpace(rule.MoveToFolder)) actionsSpecified++;
            if (rule.Alert is not null) actionsSpecified++;

            if (actionsSpecified == 0)
            {
                return (false, "At least one of Delete, MoveToFolder or Alert must be specified");
            }
            if (actionsSpecified > 1)
            {
                return (false, "Only one of Delete, MoveToFolder or Alert can be specified");
            }

            // '/' separates the levels of a destination path (see RuleEngine's folder walk), so
            // a path built only from separators names nothing to move into.
            if (!string.IsNullOrWhiteSpace(rule.MoveToFolder) && SweepFolderPath.Segments(rule.MoveToFolder).Count == 0)
            {
                return (false, "MoveToFolder must name a folder");
            }

            if (rule.Alert is not null)
            {
                // Keep-filters mean "spare the newest" — backwards for alerting, so forbidden.
                if (rule.KeepRecent is not null || rule.KeepRecentTime is not null)
                {
                    return (false, "KeepRecent and KeepRecentTime cannot be used with Alert");
                }
                return (true, null);
            }

            if (rule.KeepRecent is null && rule.KeepRecentTime is null)
            {
                return (false,"At least one of KeepRecent or KeepRecentTime must be specified");
            }

            // Zero is allowed on both keeps and means "act on everything this rule matches" —
            // a real intent ("delete all mail from this sender"), bounded by the criteria above.
            // Negative is not the same thing: Skip(-1) skips nothing and a negative window puts
            // the cutoff in the future, so both are silent "keep none" with nobody asking for it.
            if (rule.KeepRecent is < 0)
            {
                return (false, "KeepRecent cannot be negative");
            }

            if (rule.KeepRecentTime is { } keep && keep < TimeSpan.Zero)
            {
                return (false, "KeepRecentTime cannot be negative");
            }

            return (true, null);
        }

        /// <summary>
        /// Validates every rule in the set and returns the validation error for each
        /// invalid rule. An empty result means all rules are valid.
        /// </summary>
        public static IReadOnlyList<(SweepRule Rule, string Error)> GetValidationErrors(this IEnumerable<SweepRule> rules)
        {
            var errors = new List<(SweepRule Rule, string Error)>();
            foreach (var rule in rules)
            {
                var (isValid, error) = rule.IsRuleValid();
                if (!isValid)
                {
                    errors.Add((rule, error!));
                }
            }
            return errors;
        }
    }
}
