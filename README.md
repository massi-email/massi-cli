# Massi CLI

Massi cleans up your inbox using rules you define. This is the **open-source**
command-line tool and shared engine behind [Massi](https://massi.email) —
self-hosted, bring-your-own-compute, no account required. You run sweeps from your own
machine and can read exactly what touches your mailbox.

Manual cleanup is free and open here. The hosted product adds scheduled/automated sweeps,
multi-account management, longer history, and a web dashboard.

## Install & run

```bash
# .NET SDK is pinned by global.json
dotnet build Massi.sln

# Run the CLI
dotnet run --project cli/EmailSweepAutomation.csproj -- --help
```

## Commands

- `gmail-login` — OAuth authenticate with your Gmail account.
- `login` — verify credentials for a provider (gmail or imap).
- `top-senders` — analyze the inbox and summarize the top senders; with `--interactive`,
  build sweep rules from selected senders.
- `print-rules` — print the current sweep rules.
- `sweep` — apply the rules to the inbox.

Rules live in `sweep-rules.json`; non-secret defaults in `cli/appsettings.json`.

## Layout

```
cli/      The command-line tool (EmailSweepAutomation)
core/     Shared engine library
tests/    xUnit + Moq unit tests
```

## Contributing

This repository is a published mirror of the open-source surface of Massi's monorepo.
Development happens upstream; contributions here are reviewed and ported back. Open an
issue or PR and we'll pick it up.

## License

[Apache License 2.0](./LICENSE).
