# Massi — open-source CLI and engine

This is the open-source part of Massi: the command-line sweeper and the engine it runs, so
anyone can read exactly what runs against their mailbox. Treat everything here as public.

## Rules

- **No secrets** — no API keys, connection strings, customer data, or credentials.
  `appsettings.json` here holds only non-sensitive defaults / placeholders.
- **No hosted-service logic** — billing, multi-tenancy, and scheduling are not part of the
  open-source surface.
- Licensed under Apache-2.0 — see `LICENSE` at the repository root.

## Versioning

`Directory.Build.props` here declares `<VersionPrefix>` — the one place the open-source release
version lives, governing the engine, the CLI and the tests. The release process **refuses to
publish a tag that disagrees with it**, so a release cannot ship assemblies that misreport their
own version.

The build counter and upstream commit are not declared here: the release export writes them
into `build.props` beside it, and `Directory.Build.props` imports that file when it exists. A
contributor building from a clone has no such file and gets an honest `0.2.0 (local build)`
rather than a borrowed counter. `BuildInfo` reads it back, and `massi --version` prints it.

`InformationalVersion` is `BuildInfo`'s format and nothing else may write to it — which is why
`IncludeSourceRevisionInInformationalVersion` is off. The SDK otherwise appends
`+<SourceRevisionId>` *after* the release stamp, and a released binary then reports itself as a
local build. `BuildInfoTests` pins this by asserting on the raw attribute, because `Parse`
tolerates the corruption and so hides it.

## Layout

- `cli/` — the command-line tool (`EmailSweepAutomation`); also still holds Gmail access.
- `core/` — the shared engine (`Massi.Core`): rule engine, mailbox fetch/selection, rule model
  and repositories, duration parsing, the IMAP connector abstraction. The CLI references it.
  Its types are still declared in the `EmailSweepAutomation` namespace.
- `tests/` — xUnit + Moq unit tests for the open-source code. Tests live here, **not** inside
  the project folders, to keep production and test code separate.

## Test conventions

- xUnit for facts/theories; Moq for fakes.
- One test project for the whole open-source surface (`EmailSweepAutomation.Test.UnitTests`) —
  it covers both the CLI and `Massi.Core`. Do not add a second.
- Run tests with `dotnet test Massi.sln` from the repository root.
