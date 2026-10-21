using EmailSweepAutomation.Commands;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace EmailSweepAutomation.SpectreCliInfrastructure;

/// <summary>
/// Spectre's help, plus the rules-file reference after the options of the commands that read a
/// rules file. A footer rather than part of the description, because the description is also
/// what the root <c>--help</c> lists beside every command.
/// </summary>
public sealed class RulesFileHelpProvider(ICommandAppSettings settings) : HelpProvider(settings)
{
    private static readonly HashSet<string> CommandsReadingRulesFiles = [SweepCommand.Name, PrintRulesCommand.Name];

    public override IEnumerable<IRenderable> GetFooter(ICommandModel model, ICommandInfo? command)
    {
        var footer = base.GetFooter(model, command);
        if (command is null || !CommandsReadingRulesFiles.Contains(command.Name))
            return footer;

        return footer.Concat(
        [
            Text.NewLine,
            new Markup("[yellow]RULES FILE:[/]"),
            Text.NewLine,
            // Text, not Markup: the reference is JSON-heavy and its brackets are literal.
            new Text(RulesFileHelp.Text),
            Text.NewLine,
        ]);
    }
}
