using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
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

        /// <summary>
        /// Matches a sender whose host is this domain or a subdomain of it. An alternative to
        /// <see cref="FromAddress"/>; a rule sets at most one of them.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FromDomain { get; set; }

        /// <summary>
        /// Ordered branches. When present, the rule's criteria select candidates and each candidate
        /// goes to the first matching branch, which decides what happens to it; the rule itself then
        /// has no action or keep. A candidate matching no branch is left alone.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SweepBranch>? Branches { get; set; }

        [JsonIgnore]
        public bool IsBranched => Branches is { Count: > 0 };

        /// <summary>
        /// Whether the rule itself names an action (Delete, MoveToFolder or Alert), as written —
        /// <c>Delete: false</c> counts. Validation and descriptions both read it.
        /// </summary>
        [JsonIgnore]
        public bool DeclaresAction => Delete is not null || !string.IsNullOrWhiteSpace(MoveToFolder) || Alert is not null;

        /// <summary>
        /// The branches the engine evaluates: this rule's own, or for a flat rule a single
        /// catch-all carrying the rule's action and keep. Everything downstream of evaluation —
        /// actions, previews, summaries — reads branches only, so flat and branched rules share
        /// one path.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<SweepBranch> EffectiveBranches => IsBranched ? Branches! : [AsSingleBranch()];

        /// <remarks>
        /// <c>Delete == true</c>, not <c>is not null</c>: validation accepts <c>Delete: false</c>
        /// as "an action", and it has only ever meant "do nothing", so it maps to
        /// <see cref="SweepBranchAction.Leave"/> rather than to a delete.
        /// </remarks>
        private SweepBranch AsSingleBranch() => new()
        {
            Action = Delete == true ? SweepBranchAction.Delete
                : !string.IsNullOrWhiteSpace(MoveToFolder) ? SweepBranchAction.MoveToFolder
                : Alert == true ? SweepBranchAction.Alert
                : SweepBranchAction.Leave,
            MoveToFolder = MoveToFolder,
            KeepRecent = KeepRecent,
            KeepRecentTime = KeepRecentTime,
        };
    }
}
