using System;
using System.Collections.Generic;

namespace EmailSweepAutomation;

/// <summary>Keep-rule choice captured from the user: exactly one of the two is set.</summary>
public sealed record KeepRuleChoice
{
    public int? KeepRecent { get; }
    public TimeSpan? KeepRecentTime { get; }

    public KeepRuleChoice(int? keepRecent, TimeSpan? keepRecentTime)
    {
        if (keepRecent.HasValue == keepRecentTime.HasValue)
        {
            throw new ArgumentException(
                "Exactly one of KeepRecent or KeepRecentTime must be set.");
        }

        KeepRecent = keepRecent;
        KeepRecentTime = keepRecentTime;
    }
}

/// <summary>Action choice captured from the user: Delete, a destination folder, or Alert (watch only).</summary>
public sealed record RuleActionChoice(bool Delete, string? MoveToFolder, bool Alert = false);

/// <summary>
/// The interactive I/O seam for building sweep rules. The concrete implementation
/// (SpectreRulePrompter) is a thin console shell; InteractiveRuleBuilder is tested
/// against a fake implementation of this interface.
/// </summary>
public interface IRulePrompter
{
    /// <summary>Returns the chosen sender, or null when the user chooses "Done".</summary>
    SenderSummary? SelectSender(IReadOnlyList<SenderSummary> senders);

    /// <summary>Returns the SubjectContains value, or null/empty to skip.</summary>
    string? PromptSubjectContains();

    KeepRuleChoice PromptKeepRule();

    RuleActionChoice PromptAction();

    /// <summary>Shows the assembled rule and returns whether to save it.</summary>
    bool ConfirmRule(SweepRule rule);

    /// <summary>Reports that an assembled rule failed validation and will not be saved.</summary>
    void ReportInvalid(string message);

    bool AskAddAnother();
}
