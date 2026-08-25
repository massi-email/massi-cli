using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EmailSweepAutomation.Commands;

public class PrintRulesCommand: AsyncCommand<PrintRulesCommand.Settings>
{
    public PrintRulesCommand()
    {
        
    }

    protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            AnsiConsole.MarkupLineInterpolated($"Executing command [green]{nameof(PrintRulesCommand)}[/]");
            var rulesRepository = new SweepRuleRepository(settings.RulesFile);
            var rules = await rulesRepository.GetRulesAsync();
            foreach (var rule in rules)
            {
                AnsiConsole.Write(new Rule($"[green]Rule {rule.Id}[/]") { Justification = Justify.Left });
                AnsiConsole.MarkupLineInterpolated($"[green]From:[/] {rule.FromAddress}");
                AnsiConsole.MarkupLineInterpolated($"[green]Subject Contains:[/] {rule.SubjectContains}");
                AnsiConsole.MarkupLineInterpolated($"[green]Keep Recent:[/] {rule.KeepRecent}");
                AnsiConsole.MarkupLineInterpolated($"[green]Keep Recent Time:[/] {rule.KeepRecentTime}");
                AnsiConsole.MarkupLineInterpolated($"[green]Delete:[/] {rule.Delete}");
                AnsiConsole.MarkupLineInterpolated($"[green]Move To Folder:[/] {rule.MoveToFolder}");
            }
            AnsiConsole.Write(new Rule());
        } 
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        return 0;
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
