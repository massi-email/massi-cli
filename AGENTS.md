# oss/ — Open-Source Surface

Everything in this folder is mirrored one-way to a separate **public repository** at release,
as a squashed single commit with no inherited history — deliberately *not* a subtree split,
which would republish secrets in this repo's history (see `infra/AGENTS.md`). Treat it as
already public.

## Rules

- **No dependency on `cloud/`** — not in code, project references, config, or docs.
- **No secrets** — no API keys, connection strings, customer data, or credentials.
  `appsettings.json` here holds only non-sensitive defaults / placeholders.
- **No commercial logic** — billing, multi-tenancy, and scheduling belong in `cloud/`.
- No `LICENSE` file lives here by design: Apache-2.0 is laid on by
  `infra/oss-release-overlay/` during the release export.

## Versioning

`Directory.Build.props` here declares `<VersionPrefix>` — the one place the open-source release
version lives, governing the engine, the CLI and the tests. It sits at the root of `oss/`, which
the release mirror rebases to the root of the public repo, so the same declaration applies in
both. `infra/scripts/publish-oss.sh` **refuses to publish a tag that disagrees with it**, so a
release cannot ship assemblies that misreport their own version.

The build counter and upstream commit are not declared here: the publish script writes them into
`build.props` beside it at export time, and `Directory.Build.props` imports that file when it
exists. A contributor building the public repo has no such file and gets an honest
`0.1.0 (local build)` rather than a borrowed counter. `BuildInfo` reads it back, and
`massi --version` prints it.

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
- `tests/` — xUnit + Moq unit tests for the OSS code. Tests live here, **not** inside the
  project folders, to keep production and test code separate.

## Test conventions

- xUnit for facts/theories; Moq for fakes.
- One test project for the whole OSS surface (`EmailSweepAutomation.Test.UnitTests`) — it
  covers both the CLI and `Massi.Core`. Do not add a second.
- Run tests with `dotnet test ../Massi.sln`. (`Massi.Oss.sln` is the public-release **build**
  surface — CLI + `Massi.Core` — so it contains no test project to run.)
