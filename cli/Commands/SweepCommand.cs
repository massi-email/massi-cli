using MailKit;
using MailKit.Net.Imap;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace EmailSweepAutomation.Commands;

public class SweepCommand : AsyncCommand<SweepCommand.Settings>
{
    public const string Name = "sweep";

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
            var selections = evaluations
                .SelectMany(evaluation => evaluation.Branches.SelectMany(branch =>
                    branch.Selected.Select(message => (Message: message, evaluation.Rule, branch.Branch))))
                .ToList();
            var alertMatches = selections
                .Where(s => s.Branch.Action == SweepBranchAction.Alert)
                .Select(s => (s.Message, s.Rule))
                .ToList();
            AnsiConsole.MarkupLineInterpolated($"Matched [green]{selections.Count}[/] messages with rules.");

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
                
                foreach (var (messageSummary, sweepRule, branch) in selections.Where(s => s.Branch.Action != SweepBranchAction.Alert))
                {
                    // Increment message counter
                    messageCounter++;

                    // Extracting the required information
                    var subject = MessageCells.Subject(messageSummary);
                    var fromAddress = MessageCells.From(messageSummary);
                    var actions = Markup.Escape(RuleDescriptions.ActionsDescription(sweepRule, branch));

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
                    // rather than emptied-then-aborted. The two sets are disjoint — a rule (or
                    // branch) either deletes or moves — so the order costs nothing.
                    var plan = SweepPlan.From(evaluations);
                    // The engine sends these in chunks and reports each one, so a sweep of
                    // thousands shows it is moving rather than sitting on one line for minutes.
                    ctx.Status($"Moving {plan.Moves.Count} messages");
                    var movedCount = await ruleEngine.MoveMessagesAsync(plan.Moves, inbox,
                        new Progress<int>(n => ctx.Status($"Moved {n} of {plan.Moves.Count} messages")));

                    ctx.Status($"Deleting {plan.Deletes.Count} messages");
                    var trashFolder = await TrashFolderResolver.ResolveAsync(client);
                    if (trashFolder is null && plan.Deletes.Count > 0)
                        AnsiConsole.MarkupLine("[yellow]No Trash folder on this server; deletes will be permanent (flag + expunge).[/]");
                    var deletedCount = await ruleEngine.DeleteMessagesAsync(plan.Deletes, inbox, trashFolder,
                        new Progress<int>(n => ctx.Status($"Deleted {n} of {plan.Deletes.Count} messages")));

                    AnsiConsole.MarkupLineInterpolated($"Moved {movedCount}, deleted {deletedCount}.");
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
                Markup.Escape(CriteriaCell(rule)));
        }
        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Prints a per-rule (and, for a branched rule, per-branch) breakdown so each run is
    /// self-verifying: how many messages matched, how many were kept, and how many were acted on.
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
            foreach (var branch in evaluation.Branches)
            {
                var name = rule.IsBranched ? $"{rule.Id} › {branch.Branch.Label}" : rule.Id.ToString();
                var criteria = rule.IsBranched
                    ? $"{CriteriaCell(rule)} → {RuleDescriptions.BranchConditions(branch.Branch)}"
                    : CriteriaCell(rule);
                // A flat rule's matches are all its one branch's, so this is the rule's own count.
                var actioned = branch.Selected.Count;

                table.AddRow(
                    Markup.Escape(name),
                    Markup.Escape(criteria),
                    Markup.Escape(RuleDescriptions.ActionLabel(branch.Branch)),
                    branch.Matched.ToString(),
                    (branch.Matched - actioned).ToString(),
                    actioned.ToString());
            }
        }

        AnsiConsole.Write(table);
    }

    /// <summary>A rule's filter for a table cell; <c>(any)</c> when it has none.</summary>
    private static string CriteriaCell(SweepRule rule) => RuleDescriptions.Criteria(rule) ?? "(any)";

    public class Settings : MailboxCommandSettings
    {
        [CommandArgument(1, "[RulesFile]")] public string RulesFile { get; set; } = string.Empty;

        [CommandOption("-d|--dry-run")]
        [Description("Show what each rule would do, with per-branch counts; change nothing.")]
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
