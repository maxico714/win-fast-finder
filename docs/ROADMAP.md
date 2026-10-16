# ROADMAP — win-fast-finder

SemVer + Keep-a-Changelog from day one. Version is centralised in
`Directory.Build.props`.

## Current — v1.0.2 (released)

Core value is shipped and proven, filters finished to the documented spec:

- Parallel index walk, binary cache (v2: paths + sizes + mtimes),
  incremental refresh (Quick/Deep)
- Fuzzy + glob + full `ext:` / `size:` / `dm:` / `path:` grammars,
  `[AND]` / `[OR]` logic, multi-term AND, invalid-directive feedback
- Search over the whole match set (no caps), true totals, cancellation
- Configurable `RegisterHotKey` hotkey + optional Ctrl double-tap
- Borderless popup, System/Light/Dark themes, amber/cyan/red highlighting,
  autocomplete with index-backed values
- Full settings surface, compat-gated UAC installer, silent uninstall
- 207 mutation-verified tests + synthetic-keypress E2E
- Zero network code, zero third-party runtime dependencies

## Next — v1.1.x (stability and polish)

| Item | Why |
|---|---|
| Code-sign the installer | The single biggest trust gap; needs funding |
| Result previews (thumbnail / metadata pane) | Makes the popup more useful without leaving it |
| Result previews (thumbnail / metadata pane) | Makes the popup more useful without leaving it |
| Keyboard navigation polish (PageUp/PageDown, jump-to-letter) | Power-user ergonomics |
| Localisation of the UI | Settings window and hint bar are English-only |
| winget manifest | Needs a stable public release URL first |

## Later — v1.2.x

| Item | Why |
|---|---|
| Multiple named index profiles (e.g. "Work", "Personal") | Different roots, different ignore rules |
| Saved searches / pinned results | Frequently opened files should not need typing |
| Plugin surface for custom ranking | Without breaking the zero-dependency rule |
| Optional Everything-import | Read their cache format, don't become them |

## Parked — explicitly not planned

These stay out unless the constraints change:

| Idea | Why parked |
|---|---|
| File **content** search | Different engine, different privacy story, different scale |
| Cloud sync / any network feature | Would invalidate the "no network code" claim that the project is built on |
| NTFS MFT / USN journal reads (admin) | Needs an optional, separately-auditable native helper; revisit when packaging exists |
| Non-Windows ports | The speed comes from Windows-specific choices |
| Paid tiers | MIT means MIT |

## Maintenance commitments

- **Security fixes** are released as patch versions; see
  [SECURITY.md](../SECURITY.md).
- **Backwards compatibility:** settings and cache files are versioned; a
  corrupt or older file falls back to safe defaults rather than crashing.
- **Test discipline:** every behavioural change ships with a test that fails
  before the change and passes after — see
  [CONTRIBUTING.md](../CONTRIBUTING.md).

## How to influence the roadmap

Open an issue describing the **problem**, not the solution. The most useful
reports include:

1. What you were trying to do
2. What happened instead
3. How often it happens
4. Your Windows version and install method

Priority is driven by user pain, not by what is easy to implement.
