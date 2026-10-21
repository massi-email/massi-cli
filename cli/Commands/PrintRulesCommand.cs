using Spectre.Console;
using Spectre.Console.Cli;

namespace EmailSweepAutomation.Commands;

/// <summary>
/// Prints a rules file the way the engine reads it, and checks it: every rule prints, an invalid
/// one with what is wrong, and the exit code is 1 when any rule is invalid. The wording lives in
/// <see cref="RuleDescriptions"/>; this is the console shell around it.
/// </summary>
public class PrintRulesCommand : AsyncCommand<PrintRulesCommand.Settings>
{
    public const string Name = "print-rules";

    protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            AnsiConsole.MarkupLineInterpolated($"Executing command [green]{nameof(PrintRulesCommand)}[/]");
            var rulesRepository = new SweepRuleRepository(settings.RulesFile);
            var rules = (await rulesRepository.GetRulesAsync()).ToList();
            var invalid = 0;
            foreach (var rule in rules)
            {
                AnsiConsole.Write(new Rule($"[green]Rule {rule.Id}[/]") { Justification = Justify.Left });
                var description = RuleDescriptions.Describe(rule);
                if (description.Matches is not null)
                    AnsiConsole.MarkupLineInterpolated($"[green]Matches:[/] {description.Matches}");
                if (description.Action is not null)
                    AnsiConsole.MarkupLineInterpolated($"[green]Action:[/] {description.Action}");
                if (description.Branches.Count > 0)
                {
                    AnsiConsole.MarkupLineInterpolated($"[green]Branches:[/]");
                    foreach (var item in description.Branches)
                        AnsiConsole.MarkupLineInterpolated($"  {item}");
                }
                if (rule.Validate() is { } problem)
                {
                    invalid++;
                    AnsiConsole.MarkupLineInterpolated($"[red]Invalid: {problem.Message}[/]");
                }
            }
            AnsiConsole.Write(new Rule());

            var summary = RuleDescriptions.Summary(rules.Count, invalid);
            if (invalid > 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{summary}[/]");
                return 1;
            }
            AnsiConsole.MarkupLineInterpolated($"[green]{summary}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[RulesFile]")]
        public string RulesFile { get; set; } = string.Empty;

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(RulesFile))
            {
                return ValidationResult.Error("Rules file not specified");
            }

            return !File.Exists(RulesFile)
                ? ValidationResult.Error("Rules file not found")
                : ValidationResult.Success();
        }
    }
}
