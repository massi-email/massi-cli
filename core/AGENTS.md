# oss/core — Massi.Core

The shared sweep engine. Host-agnostic: the CLI (`oss/cli`) and the cloud
(`cloud/application`, and through it api + worker) both reference it, so manual and scheduled
sweeps run identical logic instead of two implementations drifting apart.

## What lives here

- **Rule engine** — `RuleEngine` / `IRuleEngine`, evaluating rules against fetched messages.
- **Rule model and repositories** — `SweepRule` and the read/write repository abstractions plus
  the JSON-file implementation the CLI uses. The cloud stores the same rule shape as jsonb.
- **Mailbox fetch and selection** — `MailboxFetcher` and `FetchSelection`: execute an
  already-resolved selection against an already-opened folder. It knows nothing about where the
  selection came from, which is what lets both hosts drive it.
- **Duration parsing** — one parser serving every place a relative time window is accepted, so
  there is one duration format across the product.
- **Connector abstraction** — `IMailConnector` / `IMailClientFactory` (a registry keyed by
  provider), the basic-auth IMAP connector, the connection request, and password resolution with
  its prompt seam.

Types here are still declared in the **`EmailSweepAutomation` namespace**, not `Massi.Core`; the
assembly was renamed ahead of the namespaces. Renaming them is a tracked follow-up, not drift to
"fix" piecemeal.

## Rules

- Open source — same constraints as `../AGENTS.md`.
- **Host-agnostic:** no CLI, ASP.NET or Blazor dependencies, no Spectre.Console, no config
  binding, no secrets. Hosts inject configuration and I/O. The `.csproj` says the same thing at
  the point where a package reference would be added.
- No dependency on `cloud/`.
- Tests live in `../tests/`, not here.

## Still to move

**`GmailService` and the Gmail OAuth connector remain in `oss/cli`** — the Gmail path is the one
piece of mailbox access not yet extracted. Moving it is the remaining step of this extraction.
