using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmailSweepAutomation;

/// <summary>
/// Orchestrates the interactive "build a sweep rule from a selected sender" loop.
/// All user interaction goes through <see cref="IRulePrompter"/>; persistence goes
/// through <see cref="IWritableSweepRuleRepository"/>. Each confirmed rule is appended
/// and saved immediately so partial sessions still persist their confirmed rules.
/// </summary>
public sealed class InteractiveRuleBuilder
{
    private readonly IRulePrompter prompter;

    public InteractiveRuleBuilder(IRulePrompter prompter)
    {
        this.prompter = prompter ?? throw new ArgumentNullException(nameof(prompter));
    }

    public async Task RunAsync(
        IReadOnlyList<SenderSummary> senders,
        IWritableSweepRuleRepository repository)
    {
        ArgumentNullException.ThrowIfNull(senders);
        ArgumentNullException.ThrowIfNull(repository);

        while (true)
        {
            var sender = prompter.SelectSender(senders);
            if (sender is null)
            {
                break;
            }

            var subject = prompter.PromptSubjectContains();
            var action = prompter.PromptAction();
            var keep = action.Alert ? null : prompter.PromptKeepRule();

            var rule = new SweepRule
            {
                FromAddress = sender.Address,
                SubjectContains = string.IsNullOrWhiteSpace(subject) ? null : subject,
                KeepRecent = keep?.KeepRecent,
                KeepRecentTime = keep?.KeepRecentTime,
                Delete = action.Delete ? true : null,
                MoveToFolder = string.IsNullOrWhiteSpace(action.MoveToFolder) ? null : action.MoveToFolder,
                Alert = action.Alert ? true : null,
            };

            var (isValid, error) = rule.IsRuleValid();
            if (!isValid)
            {
                prompter.ReportInvalid(error!);
            }
            else if (prompter.ConfirmRule(rule))
            {
                await repository.AddRuleAsync(rule);
                await repository.SaveAsync();
            }

            if (!prompter.AskAddAnother())
            {
                break;
            }
        }
    }
}
