using MailKit;
using MailKit.Net.Imap;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace EmailSweepAutomation.Commands;

public class SweepCommand : AsyncCommand<SweepCommand.Settings>
{
    private readonly IMailClientFactory mailClientFactory;
    private readonly IRuleEngine ruleEngine;
    private readonly MailboxFetcher mailboxFetcher;
    private readonly IPasswordPrompt passwordPrompt;

    public SweepCommand(
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
        AnsiConsole.MarkupLineInterpolated($"Executing command [green]{nameof(SweepCommand)}[/]");

        try
        {
            var rulesRepository = new SweepRuleRepository(settings.RulesFile);
            IEnumerable<SweepRule> rules = await rulesRepository.GetRulesAsync();
            if (!rules.Any())
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]No rules found in {settings.RulesFile}[/]");
                return 0;
            }

            var validationErrors = rules.GetValidationErrors();
            if (validationErrors.Count > 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Found {validationErrors.Count} invalid rule(s). Aborting; no emails were touched.[/]");
                foreach (var (rule, error) in validationErrors)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]  Rule {rule.Id}:[/] {error}");
                }
                return 1;
            }

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
            var inboxAccess = await client.Inbox.OpenAsync(FolderAccess.ReadWrite);
            AnsiConsole.MarkupLineInterpolated($"Inbox access: [green]{inboxAccess}[/]");
            var inbox = client.Inbox;
            AnsiConsole.MarkupLineInterpolated($"Total messages: [red]{inbox.Count}[/]");

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
            
            var evaluations = ruleEngine.EvaluateRules(emails, rules);
            var alertMatches = evaluations
                .Where(evaluation => evaluation.Rule.Alert == true)
                .SelectMany(evaluation => evaluation.Selected.Select(message => (Message: message, Rule: evaluation.Rule)))
                .ToList();
            var matchedMessages = evaluations
                .Where(evaluation => evaluation.Rule.Alert != true)
                .SelectMany(evaluation => evaluation.Selected.Select(message => (Message: message, Rule: evaluation.Rule)))
                .ToList();
            AnsiConsole.MarkupLineInterpolated($"Matched [green]{matchedMessages.Count + alertMatches.Count}[/] messages with rules.");

            PrintAlerts(alertMatches);

            if (settings.DryRun)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]Dry run enabled. No emails will be moved or deleted.[/]");
                var messageCounter = 0;
                var table = new Table();

                // Configure the table design
                table.Border(TableBorder.Rounded);
                table.AddColumn("No.");
                table.AddColumn("Subject");
                table.AddColumn("From Address");
                table.AddColumn("Actions");
                
                foreach (var (messageSummary, sweepRule) in matchedMessages)
                {
                    // Increment message counter
                    messageCounter++;

                    // Extracting the required information
                    var subject = MessageCells.Subject(messageSummary);
                    var fromAddress = MessageCells.From(messageSummary);
                    var actions = Markup.Escape(GetActionsDescription(sweepRule));

                    // Adding a row for each matched message
                    table.AddRow(messageCounter.ToString(), subject, fromAddress, actions);
                }

                // Write the table to the console
                AnsiConsole.Write(table);
            }
            else
            {
                await AnsiConsole.Status()
                .StartAsync("Moving and deleting emails...", async ctx =>
                {
                    // Moves first, deletes second. A destination the server will not give us
                    // throws, and a throw before the first delete leaves the mailbox untouched
                    // rather than emptied-then-aborted. The two sets are disjoint — a rule either
                    // deletes or moves — so the order costs nothing.
                    var messagesToMove = matchedMessages
                        .Where((matchedMessage) =>
                        {
                            var (_, sweepRule) = matchedMessage;
                            return !string.IsNullOrWhiteSpace(sweepRule.MoveToFolder);
                        })
                        .ToArray();
                    ctx.Status($"Moving {messagesToMove.Length} messages");
                    var movedCount = await ruleEngine.MoveMessagesAsync(messagesToMove, inbox);

                    var messagesToDelete = matchedMessages
                        .Where((matchedMessage) =>
                        {
                            var (_, sweepRule) = matchedMessage;
                            return sweepRule.Delete == true;
                        })
                        .Select(x => x.Message)
                        .ToArray();
                    ctx.Status($"Deleting {messagesToDelete.Length} messages");
                    var trashFolder = ResolveTrashFolder(client);
                    if (trashFolder is null && messagesToDelete.Length > 0)
                        AnsiConsole.MarkupLine("[yellow]No Trash folder on this server; deletes will be permanent (flag + expunge).[/]");
                    await ruleEngine.DeleteMessagesAsync(messagesToDelete, inbox, trashFolder);

                    AnsiConsole.MarkupLineInterpolated($"Moved {movedCount}, deleted {messagesToDelete.Length}.");
                });
            }

            PrintRuleSummary(evaluations, settings.DryRun);
            AnsiConsole.MarkupLineInterpolated($"[green]Done![/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private static IMailFolder? ResolveTrashFolder(ImapClient client)
    {
        try { return client.GetFolder(SpecialFolder.Trash); }
        catch { return null; }
    }

    /// <summary>
    /// Prints matches from alert rules. Alert rules never touch mail — the sweep's only job
    /// for them is making the matches visible. Stateless by design: every run re-prints the
    /// current matches (dedup exists only in the cloud, where sweeps run unattended).
    /// </summary>
    private static void PrintAlerts(IReadOnlyList<(IMessageSummary Message, SweepRule Rule)> alertMatches)
    {
        if (alertMatches.Count == 0)
            return;

        AnsiConsole.MarkupLineInterpolated($"[bold yellow]⚠ {alertMatches.Count} alert match(es)[/]");
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("No.");
        table.AddColumn("From");
        table.AddColumn("Subject");
        table.AddColumn("Date");
        table.AddColumn("Rule");

        var counter = 0;
        foreach (var (message, rule) in alertMatches.OrderByDescending(m => m.Message.Date))
        {
            counter++;
            table.AddRow(
                counter.ToString(),
                MessageCells.From(message),
                MessageCells.Subject(message),
                MessageCells.Date(message),
                Markup.Escape(GetCriteriaDescription(rule)));
        }
        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Prints a per-rule breakdown so each run is self-verifying: how many messages
    /// matched each rule's criteria, how many it kept, and how many it acted on.
    /// </summary>
    private void PrintRuleSummary(IReadOnlyList<RuleEvaluation> evaluations, bool dryRun)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Rule");
        table.AddColumn("Criteria");
        table.AddColumn("Action");
        table.AddColumn(new TableColumn("Matched").RightAligned());
        table.AddColumn(new TableColumn("Kept").RightAligned());
        table.AddColumn(new TableColumn(dryRun ? "Would action" : "Actioned").RightAligned());

        foreach (var evaluation in evaluations)
        {
            var rule = evaluation.Rule;
            var actioned = evaluation.Selected.Count;
            var kept = evaluation.MatchedCriteria - actioned;

            table.AddRow(
                Markup.Escape(rule.Id.ToString()),
                Markup.Escape(GetCriteriaDescription(rule)),
                Markup.Escape(GetActionLabel(rule)),
                evaluation.MatchedCriteria.ToString(),
                kept.ToString(),
                actioned.ToString());
        }

        AnsiConsole.Write(table);
    }

    private static string GetCriteriaDescription(SweepRule rule)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(rule.FromAddress)) parts.Add($"From ~ {rule.FromAddress}");
        if (!string.IsNullOrEmpty(rule.SubjectContains)) parts.Add($"Subject ~ {rule.SubjectContains}");
        return parts.Count > 0 ? string.Join(" AND ", parts) : "(any)";
    }

    private static string GetActionLabel(SweepRule rule)
    {
        string keep = rule.KeepRecent.HasValue ? $" (keep {rule.KeepRecent} newest)"
            : rule.KeepRecentTime.HasValue ? $" (keep last {rule.KeepRecentTime.Value.TotalDays:0.#}d)"
            : string.Empty;

        if (rule.Alert == true) return "Alert";
        if (rule.Delete == true) return "Delete" + keep;
        if (!string.IsNullOrEmpty(rule.MoveToFolder)) return $"Move → {rule.MoveToFolder}" + keep;
        return "(none)" + keep;
    }

    private string GetActionsDescription(SweepRule rule)
    {
        var actions = new List<string>();
        if (rule.Alert == true) actions.Add("Alert");
        if (rule.Delete == true) actions.Add("Delete");
        if (!string.IsNullOrEmpty(rule.MoveToFolder)) actions.Add($"Move to folder {rule.MoveToFolder}");
        if (rule.KeepRecent.HasValue) actions.Add($"Keep recent {rule.KeepRecent.Value} messages");
        if (rule.KeepRecentTime.HasValue) actions.Add($"Keep recent messages within {rule.KeepRecentTime.Value.TotalDays} days");

        return string.Join(", ", actions);
    }

    public class Settings : MailboxCommandSettings
    {
        [CommandArgument(1, "[RulesFile]")] public string RulesFile { get; set; } = string.Empty;

        [CommandOption("-d|--dry-run")]
        [DefaultValue(false)]
        public bool DryRun { get; set; }
        
        [CommandOption("-c|--count <COUNT>")]
        [Description("Analyze the newest COUNT emails by index. -1 for all. Mutually exclusive with --since.")]
        public int? Count { get; set; }

        [CommandOption("-s|--since <DURATION>")]
        [Description("Fetch emails newer than DURATION by sent date (per-calendar-day). Formats: 30d (days) or a .NET TimeSpan like 90.00:00:00. Minimum 1 day. Default: 30d. Mutually exclusive with --count.")]
        public string? Since { get; set; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;

            if (string.IsNullOrWhiteSpace(RulesFile))
                return ValidationResult.Error("Rules file not specified");

            if (!File.Exists(RulesFile))
                return ValidationResult.Error("Rules file not found");

            var selectionError = FetchSelectionResolver.Validate(Count, Since);
            if (selectionError is not null)
                return ValidationResult.Error(selectionError);

            return ValidationResult.Success();
        }
    }
}
