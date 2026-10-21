using EmailSweepAutomation.Commands;
using EmailSweepAutomation.Configuration;
using EmailSweepAutomation.SpectreCliInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text;

namespace EmailSweepAutomation
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Load a .env file if present (does not overwrite real environment variables).
            try { DotNetEnv.Env.NoClobber().TraversePath().Load(); }
            catch { /* no .env file present — ignore */ }

            System.Console.OutputEncoding = Encoding.UTF8;
            IServiceCollection registrations = new ServiceCollection();

            // Create IConfiguration object to read appsettings.json
            // and register it as a singleton.

            var basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) 
                ?? Directory.GetCurrentDirectory();
            
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json")
                // Environment variables (including those loaded from a git-ignored .env
                // above) override appsettings.json. Supply Gmail OAuth credentials without
                // editing a tracked file via GmailSettings__ClientId / GmailSettings__ClientSecret.
                .AddEnvironmentVariables()
                .Build();
            registrations.AddSingleton<IConfiguration>(configuration);
            
            registrations.Configure<GmailSettingsOptions>(
                configuration.GetSection("GmailSettings"));
            
            // Register the Gmail OAuth service and the provider connector registry.
            registrations.AddSingleton<GmailService>();
            registrations.AddSingleton<IMailConnector, GmailConnector>();
            registrations.AddSingleton<IMailConnector, BasicImapConnector>();
            registrations.AddSingleton<IMailClientFactory, MailClientFactory>();
            registrations.AddSingleton<IPasswordPrompt, SpectrePasswordPrompt>();
            
            // Register the RuleEngine as a singleton.
            registrations.AddSingleton<IRuleEngine, RuleEngine>();

            // Register the MailboxFetcher as a singleton.
            registrations.AddSingleton<MailboxFetcher>();

            // Register the interactive rule-builder and its console prompter.
            registrations.AddSingleton<IRulePrompter, SpectreRulePrompter>();
            registrations.AddSingleton<InteractiveRuleBuilder>();

            // Register commands.
            registrations.AddSingleton<TopSendersCommand>();
            registrations.AddSingleton<GmailLoginCommand>();
            registrations.AddSingleton<LoginCommand>();
            registrations.AddSingleton<SweepCommand>();
            registrations.AddSingleton<PrintRulesCommand>();
            registrations.AddSingleton<FindCommand>();

            // Create a type registrar and register any dependencies.
            // A type registrar is an adapter for a DI framework.
            var registrar = new TypeRegistrar(registrations);

            // Create a new command app with the registrar
            // and run it with the provided arguments.
            var app = new CommandApp(registrar);

            app.Configure(config =>
            {
                config.SetApplicationName("massi");

                // Which build is about to touch your mail — the question the whole
                // publish-the-engine-as-source promise rests on. Read from the engine assembly,
                // not the CLI's, because the engine is the half that does the deleting.
                config.SetApplicationVersion(BuildInfo.Current.Display);

                // sweep --help and print-rules --help end with the rules-file reference.
                config.SetHelpProvider(new RulesFileHelpProvider(config.Settings));

                config.AddCommand<TopSendersCommand>("top-senders")
                    .WithDescription("List the inbox's top senders; with --interactive, turn them into sweep rules.");
                config.AddCommand<GmailLoginCommand>("gmail-login")
                    .WithDescription("Sign in to Gmail with OAuth and cache the token for later commands.");
                config.AddCommand<LoginCommand>("login")
                    .WithDescription("Check mailbox credentials (Gmail or IMAP) by connecting and disconnecting.");
                config.AddCommand<SweepCommand>(SweepCommand.Name)
                    .WithDescription("Apply a rules file to the inbox: delete, move or alert on matching mail.");
                config.AddCommand<PrintRulesCommand>(PrintRulesCommand.Name)
                    .WithDescription("Print a rules file as sweep reads it and report invalid rules.");
                config.AddCommand<FindCommand>("find")
                    .WithDescription("Search the inbox by --from/--subject and print the matches; changes nothing.");
            });
            return app.Run(args);
        }
    }
}
