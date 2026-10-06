# Contributing to win-fast-finder

Thanks for considering a contribution. This project is small on purpose â€”
focused scope, zero runtime dependencies, and tests that prove behaviour
rather than describe it.

## Ground rules

1. **No runtime dependencies.** The engine uses the base class library only.
   A feature that needs a NuGet package is a feature that needs a different
   design. (Test-time packages are fine.)
2. **Windows, non-admin.** Nothing may require elevation to *run*. The
   installer may elevate; the app may not.
3. **Filenames only.** This is a filename search tool. Content indexing is
   explicitly out of scope.
4. **No network code, ever.** See [PRIVACY](docs/PRIVACY.md). A single
   `HttpClient` reference would invalidate the project's central claim.
5. **Tests are not optional.** See the test discipline below.

## Getting set up

```powershell
git clone https://github.com/maxico714/win-fast-finder.git
cd win-fast-finder
dotnet build Wff.sln --no-incremental
dotnet test tests/Wff.Tests/Wff.Tests.csproj --no-build
```

You should see **0 warnings, 0 errors** and **62 passed**. If you don't, fix
that before changing anything â€” you want a known-good baseline.

Full details in [docs/BUILDING.md](docs/BUILDING.md).

## Test discipline

Every behavioural change follows the same loop:

1. **Write the test first.** It must fail â€” confirm it.
2. **Make it pass** with the smallest change that works.
3. **Mutation-verify.** Deliberately break the new code path (flip a
   comparison, delete a guard) and confirm the test fails again.
4. **Restore** the code and confirm 62/62.

Step 3 is the part people skip, and it is the part that catches tests which
pass for the wrong reason. A test that cannot fail is not a test.

Match the existing structure:

| Change | Test file |
|---|---|
| Query parsing / filters | `FilterTests.cs`, `GlobTests.cs` |
| Ranking and highlighting | `IndexTests.cs`, `HighlightTests.cs`, `PathSortTests.cs` |
| Walking / incremental | `WalkerTests.cs`, `ParallelWalkerTests.cs`, `IncrementalTests.cs` |
| Cache round-trip | `CacheTests.cs` |
| Settings behaviour | `SettingsTests.cs`, `ProdSettingsTests.cs`, `SafeDefaultTests.cs` |
| Hotkeys | `HotkeyParseTests.cs`, `DoubleTapTests.cs`, `DoubleTapHoldTests.cs` |
| Installer rules | `WizardValidationTests.cs`, `RuntimeCheckTests.cs` |
| Adversarial input | `AdversarialTests.cs` |

Pure logic belongs in `Wff.Engine` so it is testable without a UI. Interactive
code (popup, theme, hook plumbing) stays thin and delegates to the engine.

## Style

- C# with file-scoped namespaces, `Nullable` enabled, implicit usings.
- Formatting is enforced by [`.editorconfig`](.editorconfig) â€” most IDEs
  apply it automatically.
- XML doc comments are not required; a good file-header comment explaining
  *why* is preferred. Several engine files already do this.
- Log with `Logger.Info(...)`. Never throw from logging.

## Pull requests

1. Branch from `main`.
2. Keep the diff focused â€” one concern per PR.
3. Make sure `dotnet build` is clean and all tests pass.
4. Update docs if behaviour changed: `README.md`, `docs/USAGE.md`,
   `docs/ARCHITECTURE.md` as applicable.
5. Add a `CHANGELOG.md` entry under **Unreleased** describing the change in
   one or two lines.

PRs that add a feature without a test that fails first will be marked as
draft until the test exists.

## Reporting bugs

Use [GitHub Issues](https://github.com/maxico714/win-fast-finder/issues).
Please include:

- Windows version (`winver`)
- Installed release vs. built from source
- The complete log: `%APPDATA%\win-fast-finder\log.txt`
- Steps to reproduce, expected behaviour, actual behaviour

For anything security-related, follow [SECURITY.md](SECURITY.md) and do **not**
open a public issue.

## Suggesting features

Open an issue with the problem you are trying to solve, not just the solution
you have in mind. Scope is deliberately tight â€” see
[docs/ROADMAP.md](docs/ROADMAP.md) for what is planned and what is parked.

## Project layout at a glance

| Path | Role |
|---|---|
| `src/Wff.Engine` | Index, search, walk, cache, settings â€” no UI |
| `src/Wff.App` | WPF popup, hotkeys, headless CLI |
| `src/Wff.Setup` | Installer wizard |
| `tests/Wff.Tests` | Unit tests |
| `tests/e2e` | Synthetic-keypress harness |
| `build/` | Packaging scripts |
| `docs/` | Architecture, usage, privacy, troubleshooting |

## Licence

By contributing, you agree that your contributions are licensed under the
[MIT License](LICENSE), the same as the rest of the project.
