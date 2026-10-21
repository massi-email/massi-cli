using System.Text.Json.Serialization;

namespace EmailSweepAutomation
{
    /// <summary>
    /// What a branch does with the mail routed to it. <see cref="Leave"/> is first so that a
    /// branch whose action is missing from stored JSON reads as "do nothing", never as delete.
    /// Serialised by name, so stored rules and rules files stay readable.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SweepBranchAction>))]
    public enum SweepBranchAction
    {
        Leave,
        Delete,
        MoveToFolder,

        /// <summary>
        /// Select the mail to report it, without touching it. Only the implicit branch of a flat
        /// alert rule carries this (see <see cref="SweepRule.EffectiveBranches"/>); a stored
        /// branch cannot alert.
        /// </summary>
        Alert,
    }

    /// <summary>
    /// One arm of a branched rule. A message goes to the first branch whose conditions match
    /// (both set = AND; neither set = everything else). See <see cref="SweepRule.Branches"/>.
    /// </summary>
    public class SweepBranch
    {
        public string Label { get; set; } = "";

        /// <summary>Any of: <c>user@host</c> (exact), <c>@host</c> (exactly that host),
        /// <c>@*.host</c> (the host and its subdomains), or a name pattern on one host:
        /// <c>name*@host</c>, <c>*name@host</c>, <c>*name*@host</c>.</summary>
        public List<string>? FromAny { get; set; }

        /// <summary>Case-insensitive subject substrings, any of.</summary>
        public List<string>? SubjectContainsAny { get; set; }

        public SweepBranchAction Action { get; set; }
        public string? MoveToFolder { get; set; }
        public int? KeepRecent { get; set; }
        public TimeSpan? KeepRecentTime { get; set; }

        [JsonIgnore]
        public bool IsCatchAll => FromAny is not { Count: > 0 } && SubjectContainsAny is not { Count: > 0 };

        /// <summary>
        /// Whether a message from these senders, with this subject, meets this branch's
        /// conditions. Any one sender is enough; with no sender condition the subject alone
        /// decides, even for a message with no From at all.
        /// </summary>
        public bool Matches(IEnumerable<string> fromAddresses, string? subject) =>
            (FromAny is not { Count: > 0 } from
                || fromAddresses.Any(a => from.Any(e => SenderMatch.EntryMatches(a, e.Trim()))))
            && (SubjectContainsAny is not { Count: > 0 } keywords
                || (subject is not null && keywords.Any(k => subject.Contains(k, StringComparison.OrdinalIgnoreCase))));
    }
}
