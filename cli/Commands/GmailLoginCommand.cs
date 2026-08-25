using EmailSweepAutomation.Configuration;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailSweepAutomation.Commands
{
    public class GmailLoginCommand: AsyncCommand<GmailLoginCommand.Settings>
    {
        private readonly GmailService gmailService;
        private readonly GmailSettingsOptions gmailSettings;

        public GmailLoginCommand(GmailService gmailService, IOptions<GmailSettingsOptions> gmailSettingsOptions)
        {
            this.gmailService = gmailService ?? throw new ArgumentNullException(nameof(gmailService));
            this.gmailSettings = gmailSettingsOptions.Value ?? throw new ArgumentNullException(nameof(gmailSettingsOptions));
        }

        protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLineInterpolated($"Executing command [green]{nameof(GmailLoginCommand)}[/]");
            try
            {
                await gmailService.AuthenticateAsync(settings.Email, gmailSettings.ClientId, gmailSettings.ClientSecret);
                AnsiConsole.MarkupLineInterpolated($"[green]Authentication successful[/]");
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
            [CommandArgument(0, "[Email]")]
            public string Email { get; set; } = String.Empty;

            public override ValidationResult Validate()
            {
                if (string.IsNullOrWhiteSpace(Email))
                {
                    return ValidationResult.Error("Email not specified");
                }

                return ValidationResult.Success();
            }
        }
    }
}
