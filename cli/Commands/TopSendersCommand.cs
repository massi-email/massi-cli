using MailKit;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace EmailSweepAutomation.Commands
{
    public class TopSendersCommand: AsyncCommand<TopSendersCommand.Settings>
    {
        private readonly IMailClientFactory mailClientFactory;
        private readonly MailboxFetcher mailboxFetcher;
        private readonly InteractiveRuleBuilder ruleBuilder;
        private readonly IPasswordPrompt passwordPrompt;

        public TopSendersCommand(
            IMailClientFactory mailClientFactory,
            MailboxFetcher mailboxFetcher,
            InteractiveRuleBuilder ruleBuilder,
            IPasswordPrompt passwordPrompt)
        {
            this.mailClientFactory = mailClientFactory ?? throw new ArgumentNullException(nameof(mailClientFactory));
            this.mailboxFetcher = mailboxFetcher ?? throw new ArgumentNullException(nameof(mailboxFetcher));
            this.ruleBuilder = ruleBuilder ?? throw new ArgumentNullException(nameof(ruleBuilder));
            this.passwordPrompt = passwordPrompt ?? throw new ArgumentNullException(nameof(passwordPrompt));
        }

        protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            try
            {
                AnsiConsole.MarkupLineInterpolated($"Executing command [green]{nameof(TopSendersCommand)}[/]");

                var request = MailboxConnectionResolver.Resolve(settings, null);
                if (request.Provider == "imap")
                {
                    var password = PasswordResolver.Resolve(
                        settings.Password,
                        Environment.GetEnvironmentVariable,
                        () => passwordPrompt.PromptSecret(request.Username ?? string.Empty, request.Host ?? string.Empty));
                    request = request with { Password = password };
                }
                using var client = await mailClientFactory.ConnectAsync(request, cancellationToken);
                await client.Inbox.OpenAsync(FolderAccess.ReadOnly);
                var inbox = client.Inbox;
                int count = inbox.Count;
                AnsiConsole.MarkupLineInterpolated($"Total messages: [red]{count}[/]");

                var selection = FetchSelectionResolver.Resolve(settings.Count, settings.Since, DateTimeOffset.Now);
                var items = MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.GMailMessageId;

                var emails = new List<IMessageSummary>();
                await AnsiConsole.Status()
                    .StartAsync("Retrieving emails...", async ctx =>
                    {
                        var progress = new StatusProgress(ctx);
                        var fetched = await mailboxFetcher.FetchAsync(inbox, selection, items, progress, cancellationToken);
                        emails.AddRange(fetched);
                    });

                var emailAddresses = emails
                    .SelectMany(item => item.Envelope.From.Mailboxes.Select(mb => (Mailbox: mb, Message: item)))
                    .GroupBy(x => x.Mailbox.Address)
                    .ToDictionary(
                        g => g.Key,
                        g => new SenderSummary(
                            g.Key,
                            g.Count(),
                            g.Max(x => x.Message.Date),
                            g.Min(x => x.Message.Date),
                            g.Select(x => x.Message.Envelope.Subject).Distinct().ToList(),
                            g.Select(x => x.Message).ToList()));

                // Create a table
                var table = new Table();

                table.AddColumn("From:");
                table.AddColumn(new TableColumn("Count:").Centered());
                table.AddColumn(new TableColumn("First:").Centered());
                table.AddColumn(new TableColumn("Last:").Centered());
                foreach (var address in emailAddresses.Where(x => x.Value.Count > 1).OrderByDescending(x => x.Value.Count))
                {
                    table.AddRow(address.Key, address.Value.Count.ToString(), address.Value.FirstReceived.ToString("d"), address.Value.LastReceived.ToString("d"));
                }

                await client.DisconnectAsync(true);
                AnsiConsole.Write(table);

                if (settings.Interactive)
                {
                    var selectable = emailAddresses.Values
                        .Where(s => s.Count > 1)
                        .OrderByDescending(s => s.Count)
                        .ToList();

                    if (selectable.Count == 0)
                    {
                        AnsiConsole.MarkupLine("No senders with more than one message — nothing to build rules from.");
                        return 0;
                    }

                    var repository = new SweepRuleRepository(settings.RulesFile);
                    await ruleBuilder.RunAsync(selectable, repository);
                    AnsiConsole.MarkupLineInterpolated($"[green]Done building rules.[/] Rules file: {settings.RulesFile}");
                }

                return 0;
            } 
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
                return 1;
            }
        }
        
        public class Settings : MailboxCommandSettings
        {
            [CommandArgument(1, "[RulesFile]")]
            [Description("Rules file to append to in interactive mode. Created if it does not exist.")]
            public string RulesFile { get; set; } = string.Empty;

            [CommandOption("-c|--count <COUNT>")]
            [Description("Analyze the newest COUNT emails by index. -1 for all. Mutually exclusive with --since.")]
            public int? Count { get; set; }

            [CommandOption("-s|--since <DURATION>")]
            [Description("Fetch emails newer than DURATION by sent date (per-calendar-day). Formats: 30d (days) or a .NET TimeSpan like 90.00:00:00. Minimum 1 day. Default: 30d. Mutually exclusive with --count.")]
            public string? Since { get; set; }

            [CommandOption("-i|--interactive")]
            [Description("After listing senders, interactively build sweep rules for selected senders and append them to the rules file.")]
            public bool Interactive { get; set; }

            public override ValidationResult Validate()
            {
                var baseResult = base.Validate();
                if (!baseResult.Successful)
                    return baseResult;

                if (Interactive && string.IsNullOrWhiteSpace(RulesFile))
                    return ValidationResult.Error("Rules file is required with --interactive");

                var selectionError = FetchSelectionResolver.Validate(Count, Since);
                if (selectionError is not null)
                    return ValidationResult.Error(selectionError);

                return ValidationResult.Success();
            }
        }
    }
}
