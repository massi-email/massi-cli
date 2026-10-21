# cli — Massi CLI

The open-source command-line email sweeper. Project: `EmailSweepAutomation`
(`net10.0`, `OutputType` Exe). Built on Spectre.Console.Cli with
Microsoft.Extensions dependency injection.

## What it does

Connects to a mailbox (Gmail over OAuth, or any IMAP host with basic auth), loads sweep rules,
and applies them (delete/archive/alert) to the inbox.

## Commands (registered in `Program.cs`)

- `gmail-login` — OAuth authenticate with the user's Gmail account.
- `login` — verify credentials for any provider (gmail or imap) by connecting and disconnecting.
- `top-senders` — analyze the inbox and summarize the top senders; with `--interactive`,
  build sweep rules from selected senders and append them to a rules file.
- `print-rules` — print the current sweep rules.
- `sweep` — apply the rules to the inbox.
- `find` — one-off read-only search (`--from`/`--subject`); prints matches, touches nothing.

## What is here vs. in `../core`

The engine — rule evaluation, the rule model and repositories, mailbox fetch/selection, duration
parsing, the connector abstraction — lives in **`../core` (`Massi.Core`)**, which this project
references. Do not add engine logic here. What remains CLI-side:

- `Program.cs` — DI container + `CommandApp` wiring.
- `Commands/` — the Spectre command implementations and their settings, including the shared
  connection flags.
- `Mailbox/` — the CLI half of connection: env binding of the `MASSI_IMAP_*` variables, the
  provider preset table and settings→request resolution, and the Spectre password prompt.
- `GmailService.cs` + `Mailbox/GmailConnector.cs` — Gmail API + MailKit access on the OAuth path.
  **The one piece of the engine extraction still outstanding**; it moves to `../core` later.
- The interactive rule builder and its prompter seam — `InteractiveRuleBuilder.cs` orchestrates
  the `top-senders --interactive` loop and is unit-tested against a fake prompter; the
  Spectre implementation stays a thin console shell, untested like the live IMAP connection.
- `Configuration/`, `SpectreCliInfrastructure/`.

## Config

- `appsettings.json` — `GmailSettings` section (non-secret defaults only).
- `sweep-rules.json` — the rule set, copied to the output directory on build.

## Run

From this folder:

```bash
dotnet run -- <command>
```
