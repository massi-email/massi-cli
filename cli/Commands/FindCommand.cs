using MailKit;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace EmailSweepAutomation.Commands;

/// <summary>
/// One-off read-only search: builds a transient alert rule from the flags and prints the
/// matching messages. No rules file involved and nothing on the server is modified —
/// the persistent equivalent is an alert rule swept on a schedule (cloud).
/// </summary>
public class FindCommand : AsyncCommand<FindCommand.Settings>
{
    private readonly IMailClientFactory mailClientFactory;
    private readonly IRuleEngine ruleEngine;
    private readonly MailboxFetcher mailboxFetcher;
    private readonly IPasswordPrompt passwordPrompt;

    public FindCommand(
        IMailClientFactory mailClientFactory,
        IRuleEngine ruleEngine,
        MailboxFetcher mailboxFetcher,
        IPasswordPrompt passwordPrompt)
    {
        this.mailClientFactory = mailClientFactory ?? throw new ArgumentNullException(nameof(mailClientFactory));
        this.ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        this.mailboxFetcher = mailboxFetcher ?? throw new ArgumentNullException(nameof(mailboxFetcher));
        this.passwordPrompt = passwordPrompt ?? throw new ArgumentNullException(nameof(passwordPrompt));
    }

    protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
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

            var rule = settings.ToTransientRule();
            var evaluation = ruleEngine.EvaluateRules(emails, new[] { rule }).Single();
            await client.DisconnectAsync(true);

            if (evaluation.Selected.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No matching messages found.[/]");
                return 0;
            }

            AnsiConsole.MarkupLineInterpolated($"Found [green]{evaluation.Selected.Count}[/] matching message(s).");
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("No.");
            table.AddColumn("From");
            table.AddColumn("Subject");
            table.AddColumn("Date");

            var counter = 0;
            foreach (var message in evaluation.Selected.OrderByDescending(m => m.Date))
            {
                counter++;
                table.AddRow(
                    counter.ToString(),
                    MessageCells.From(message),
                    MessageCells.Subject(message),
                    MessageCells.Date(message));
            }
            AnsiConsole.Write(table);
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
        [CommandOption("--from <SUBSTRING>")]
        [Description("Match messages whose sender address contains SUBSTRING (case-insensitive).")]
        public string? From { get; set; }

        [CommandOption("--subject <SUBSTRING>")]
        [Description("Match messages whose subject contains SUBSTRING (case-insensitive).")]
        public string? Subject { get; set; }

        [CommandOption("-c|--count <COUNT>")]
        [Description("Search the newest COUNT emails by index. -1 for all. Mutually exclusive with --since.")]
        public int? Count { get; set; }

        [CommandOption("-s|--since <DURATION>")]
        [Description("Search emails newer than DURATION by sent date (per-calendar-day). Formats: 30d (days) or a .NET TimeSpan like 90.00:00:00. Minimum 1 day. Default: 30d. Mutually exclusive with --count.")]
        public string? Since { get; set; }

        /// <summary>The one-off search expressed as an alert rule, so `find` and alert
        /// rules share exactly one matching implementation (the engine's).</summary>
        public SweepRule ToTransientRule() => new()
        {
            FromAddress = string.IsNullOrWhiteSpace(From) ? null : From,
            SubjectContains = string.IsNullOrWhiteSpace(Subject) ? null : Subject,
            Alert = true,
        };

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;

            if (string.IsNullOrWhiteSpace(From) && string.IsNullOrWhiteSpace(Subject))
                return ValidationResult.Error("At least one of --from or --subject is required");

            var selectionError = FetchSelectionResolver.Validate(Count, Since);
            if (selectionError is not null)
                return ValidationResult.Error(selectionError);

            return ValidationResult.Success();
        }
    }
}
