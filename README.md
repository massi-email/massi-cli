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

The examples below write `massi` for that `dotnet run` line. An alias makes them work as
written:

```bash
alias massi='dotnet run --project /path/to/massi-cli/cli/EmailSweepAutomation.csproj --'
```

Every command takes `--help`; `sweep --help` and `print-rules --help` end with the full
rules-file reference.

## Connect a mailbox

Commands that open a mailbox take the address as their first argument and a `--provider`
(default `gmail`).

| Provider | Sign-in | Notes |
|----------|---------|-------|
| `gmail` | OAuth | Run `gmail-login` once; the token is cached for later commands. |
| `apple` | IMAP password | iCloud Mail. Use an app-specific password. |
| `yahoo` | IMAP password | Use an app password. |
| `fastmail` | IMAP password | Use an app password. |
| `imap` | IMAP password | Any other host: `--host`, and optionally `--port`, `--starttls`, `--user`. |

**Microsoft 365 / Outlook.com is not supported by the CLI yet.** Microsoft has turned off
password sign-in for IMAP, so `--provider imap` against Outlook won't work either. The hosted
app at [massi.email](https://massi.email) connects Microsoft 365 mailboxes over OAuth.

### Gmail

Gmail needs an OAuth client of your own: in Google Cloud Console, create a *Desktop app*
OAuth client with the Gmail API enabled. Supply its ID and secret through environment
variables (or a `.env` file in or above the working directory), not by editing a tracked file:

```bash
GmailSettings__ClientId=...
GmailSettings__ClientSecret=...
```

Then sign in. A browser window opens for consent:

```bash
massi gmail-login you@gmail.com
```

### IMAP

```bash
massi login you@icloud.com --provider apple
massi login you@example.com --provider imap --host imap.example.com
```

The password comes from `--password`, then `MASSI_IMAP_PASSWORD`, then an interactive
prompt. Prefer the env var or the prompt — a flag ends up in your shell history. To avoid
repeating the connection flags, set them once in the environment or `.env`; an explicit flag
still wins:

```bash
MASSI_IMAP_PROVIDER=imap
MASSI_IMAP_HOST=imap.example.com
MASSI_IMAP_PORT=993
MASSI_IMAP_USER=you@example.com
MASSI_IMAP_PASSWORD=...
```

## Commands

- `gmail-login <email>` — sign in to Gmail with OAuth and cache the token.
- `login <email>` — check credentials for any provider by connecting and disconnecting.
- `top-senders <email> [rules-file]` — summarize who fills your inbox; with `--interactive`,
  turn selected senders into rules appended to the rules file.
- `find <email> --from <text> --subject <text>` — read-only search (at least one of the
  two); prints matches, changes nothing.
- `print-rules <rules-file>` — print a rules file as `sweep` reads it and report invalid rules.
- `sweep <email> <rules-file>` — apply the rules to the inbox. Add `--dry-run` first: it
  shows what each rule would do, with per-branch counts, and changes nothing.

`top-senders`, `find` and `sweep` look at mail from the last 30 days by default; widen it with
`--since 90d` (or a TimeSpan such as `90.00:00:00`), or use `--count N` for the newest N
messages (`-1` for all).

A typical first run:

```bash
massi top-senders you@gmail.com my-rules.json --interactive
massi print-rules my-rules.json
massi sweep you@gmail.com my-rules.json --dry-run
massi sweep you@gmail.com my-rules.json
```

## Rules file

A rules file is a JSON array of rules. A rule's **filter** picks mail; then either its
**action** or its **branches** decide what happens to it. `cli/sweep-rules.json` is a
starting point.

### Filter

Set at least one; when several are set, all of them must match.

| Field | Matches |
|-------|---------|
| `FromAddress` | sender address contains this (case-insensitive) |
| `FromDomain` | sender host is this domain or a subdomain of it (not with `FromAddress`) |
| `SubjectContains` | subject contains this (case-insensitive) |

`Id` is the rule's number in output.

### Flat rules: one action

A flat rule has exactly one action:

- `"Delete": true` — delete matches.
- `"MoveToFolder": "Receipts"` — move matches to a folder (nest with `/`, e.g. `Finance/Bank`).
- `"Alert": true` — only list matches. Touches nothing and takes no keep.

`Delete` and `MoveToFolder` require a **keep**, so a rule never wipes a sender out by
accident. Use either or both; `0` means act on everything:

- `KeepRecent` — keep the N newest messages per sender.
- `KeepRecentTime` — keep mail newer than this TimeSpan, e.g. `"30.00:00:00"` for 30 days.

```json
{ "Id": 1, "FromAddress": "news@shop.com", "Delete": true, "KeepRecent": 3 }
```

### Branched rules: different handling for one sender

When one sender sends several kinds of mail, give the rule `Branches` instead of an action.
Each message the filter picks goes to the **first** branch it fits; mail that fits no branch
is left alone. A rule can have up to 12 branches, and a branched rule has no action or keep of
its own.

| Branch field | Meaning |
|--------------|---------|
| `Label` | required; names the branch in output |
| `FromAny` | sender is any of these [sender entries](#sender-entries) |
| `SubjectContainsAny` | subject contains any of these (case-insensitive) |
| `Action` | `Delete`, `MoveToFolder` or `Leave` (missing = `Leave`) |
| `MoveToFolder` | the folder for a `MoveToFolder` branch |
| `KeepRecent`, `KeepRecentTime` | required for `Delete` and `MoveToFolder`; not for `Leave` |

If a branch sets both `FromAny` and `SubjectContainsAny`, both must match. A branch that sets
neither is a catch-all for everything else. It is only allowed as the last branch, and after
other branches it cannot `Delete`. Branches cannot `Alert`.

```json
{ "Id": 2, "FromDomain": "bank.com", "Branches": [
  { "Label": "Statements", "SubjectContainsAny": ["statement"],
    "Action": "MoveToFolder", "MoveToFolder": "Finance/Bank",
    "KeepRecentTime": "90.00:00:00" },
  { "Label": "Offers", "FromAny": ["offers@bank.com", "promo*@bank.com"],
    "Action": "Delete", "KeepRecent": 0 },
  { "Label": "Everything else", "Action": "Leave" } ] }
```

### Sender entries

`FromAny` entries take these forms:

| Entry | Matches |
|-------|---------|
| `user@host` | exactly that address |
| `@host` | any address at exactly that host |
| `@*.host` | the host and its subdomains |
| `name*@host` | on that host, the name starts with `name` |
| `*name@host` | on that host, the name ends with `name` |
| `*name*@host` | on that host, the name contains `name` |

Name patterns are for senders that rotate addresses, such as `promo-1234@shop.com`.

Check a file with `print-rules` before sweeping. It says what is wrong with each invalid rule.
`sweep` refuses to run a file that has any invalid rule, and touches no mail.

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
