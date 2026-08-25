using System;
using System.Collections.Generic;
using Spectre.Console;

namespace EmailSweepAutomation;

/// <summary>
/// Spectre.Console implementation of <see cref="IRulePrompter"/>. Thin I/O shell:
/// it only reads from and writes to the console and contains no rule logic, so it is
/// not unit-tested (the live-console boundary, like the IMAP connection).
/// </summary>
public sealed class SpectreRulePrompter : IRulePrompter
{
    private const string DoneChoice = "✓ Done — finish";

    public SenderSummary? SelectSender(IReadOnlyList<SenderSummary> senders)
    {
        var byLabel = new Dictionary<string, SenderSummary>();
        var choices = new List<string>();
        foreach (var sender in senders)
        {
            var label = $"{Markup.Escape(sender.Address)} ({sender.Count})";
            byLabel[label] = sender;
            choices.Add(label);
        }
        choices.Add(DoneChoice);

        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a sender to create a rule for:")
                .PageSize(15)
                .AddChoices(choices));

        return picked == DoneChoice ? null : byLabel[picked];
    }

    public string? PromptSubjectContains()
    {
        return AnsiConsole.Prompt(
            new TextPrompt<string>("Subject contains (optional, press Enter to skip):")
                .AllowEmpty());
    }

    public KeepRuleChoice PromptKeepRule()
    {
        const string countMode = "Keep N most recent";
        const string timeMode = "Keep messages newer than a duration";

        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Keep-rule:")
                .AddChoices(countMode, timeMode));

        if (mode == countMode)
        {
            var count = AnsiConsole.Prompt(
                new TextPrompt<int>("How many recent messages to keep?")
                    .Validate(n => n >= 0
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Must be zero or greater")));
            return new KeepRuleChoice(count, null);
        }

        var duration = AnsiConsole.Prompt(
            new TextPrompt<string>("Keep messages newer than (e.g. 30d):")
                .Validate(value =>
                {
                    try
                    {
                        DurationParser.Parse(value);
                        return ValidationResult.Success();
                    }
                    catch (FormatException ex)
                    {
                        return ValidationResult.Error(ex.Message);
                    }
                }));
        return new KeepRuleChoice(null, DurationParser.Parse(duration));
    }

    public RuleActionChoice PromptAction()
    {
        const string deleteAction = "Delete";
        const string moveAction = "Move to folder";
        const string alertAction = "Alert (watch only — never touches mail)";

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Action:")
                .AddChoices(deleteAction, moveAction, alertAction));

        if (action == deleteAction)
        {
            return new RuleActionChoice(true, null);
        }
        if (action == alertAction)
        {
            return new RuleActionChoice(false, null, Alert: true);
        }

        var folder = AnsiConsole.Prompt(new TextPrompt<string>("Destination folder name:"));
        return new RuleActionChoice(false, folder);
    }

    public bool ConfirmRule(SweepRule rule)
    {
        AnsiConsole.Write(new Rule("[green]New rule[/]") { Justification = Justify.Left });
        AnsiConsole.MarkupLineInterpolated($"[green]From:[/] {rule.FromAddress}");
        if (!string.IsNullOrEmpty(rule.SubjectContains))
        {
            AnsiConsole.MarkupLineInterpolated($"[green]Subject contains:[/] {rule.SubjectContains}");
        }
        if (rule.KeepRecent.HasValue)
        {
            AnsiConsole.MarkupLineInterpolated($"[green]Keep recent:[/] {rule.KeepRecent} newest");
        }
        if (rule.KeepRecentTime.HasValue)
        {
            AnsiConsole.MarkupLineInterpolated($"[green]Keep recent time:[/] {rule.KeepRecentTime}");
        }
        if (rule.Delete == true)
        {
            AnsiConsole.MarkupLine("[green]Action:[/] Delete");
        }
        if (!string.IsNullOrEmpty(rule.MoveToFolder))
        {
            AnsiConsole.MarkupLineInterpolated($"[green]Action:[/] Move to {rule.MoveToFolder}");
        }
        if (rule.Alert == true)
        {
            AnsiConsole.MarkupLine("[green]Action:[/] Alert (watch only)");
        }

        return AnsiConsole.Prompt(new ConfirmationPrompt("Save this rule?"));
    }

    public void ReportInvalid(string message)
    {
        AnsiConsole.MarkupLineInterpolated($"[red]Cannot save rule:[/] {message}");
    }

    public bool AskAddAnother()
    {
        return AnsiConsole.Prompt(new ConfirmationPrompt("Add another rule?"));
    }
}
