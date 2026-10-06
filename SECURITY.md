# Security — win-fast-finder

## Reporting a vulnerability

Please **do not** open a public GitHub issue for a security vulnerability.

Instead, use one of these channels:

| Channel | Address |
|---|---|
| Email | [rekhan123451@live.com](mailto:rekhan123451@live.com) |
| GitHub private advisory | https://github.com/maxico714/win-fast-finder/security/advisories/new |

Include, where possible:

- A description of the issue and its impact
- Steps to reproduce, or a proof of concept
- Affected version (see **Help → About**, or the release tag)
- Any suggested fix

You will get a response within a few days. If the issue is confirmed, a fix
will be prepared and credited to you in the release notes unless you prefer
to remain anonymous.

## Supported versions

| Version | Supported |
|---|---|
| 1.0.x | ✅ |
| < 1.0 | ❌ upgrade |

Security fixes are released as patch versions and published to
[Releases](https://github.com/maxico714/win-fast-finder/releases).

## Scope

In scope:

- The `win-fast-finder` application, engine, and installer as published here
- The packaging scripts in `build/`
- Anything that would let an attacker read, modify, or exfiltrate files the
  user did not intend to expose, or execute code unexpectedly

Out of scope:

- Issues that require physical access or an already-compromised machine
- The .NET runtime itself (report those to Microsoft)
- Denial of service via the user indexing an enormous filesystem (documented
  behaviour: indexing is CPU- and IO-bound by design)
- Social-engineering of the unsigned installer — see below

## Trust model, stated plainly

This project makes several claims. Here is exactly what they are and what
they are not:

### 1. No network code

`src/` contains no `HttpClient`, `WebRequest`, `TcpClient`, `Socket`, or DNS
lookup. This is verifiable:

```powershell
Select-String -Path (Get-ChildItem src -Recurse -Include *.cs) `
  -Pattern 'HttpClient|WebRequest|TcpClient|UdpClient|Socket\(|Dns\.'
```

Expected: empty output. See [docs/PRIVACY.md](docs/PRIVACY.md).

### 2. The installer is NOT code-signed

This is the most important thing to understand.

The installer was built without a code-signing certificate, because
certificates cost money and this is an unfunded open-source project. As a
result:

- **SmartScreen may block it** on first run ("Windows protected your PC")
- **Defender or other antivirus may flag it**, because unsigned installers
  that request elevation and copy files match generic heuristics
- **You are trusting the binary by trusting the source** — if you cannot
  verify that, do not run it

Your options, in increasing order of assurance:

1. **Build it yourself.** The source is the ground truth:
   ```powershell
   git clone https://github.com/maxico714/win-fast-finder.git
   cd win-fast-finder
   .\build\package.ps1
   ```
   Compare your output hash to the published one — they should match.
2. **Verify the published checksum** against the `.sha256` file on the
   release page. This proves the file was not altered in transit; it does not
   prove the publisher is trustworthy.
3. **Run it sandboxed** first (Windows Sandbox, a VM) if you are uncertain.

We will not pretend the warning is a false positive. **Treat an unsigned
binary accordingly.** If this project ever gains funding, code signing is the
first thing it should buy.

### 3. The keyboard hook is optional and constrained

The Ctrl double-tap feature installs a `WH_KEYBOARD_LL` hook. By design it:

- Forwards only Ctrl press timestamps
- Discards every other key event immediately
- Never stores key codes, sequences, or text

There is no configuration that turns this into a keylogger. That said,
low-level hooks attract antivirus attention **on principle**, and some
endpoints will block them. If your organisation forbids hooks, disable the
feature (Settings → Hotkeys) or use the app without it — `Ctrl+Shift+F`
uses `RegisterHotKey` and involves no hook at all.

### 4. Non-admin by design

The app runs as a normal user. It never opens elevated handles, never reads
the NTFS MFT, and never writes to `HKEY_LOCAL_MACHINE`. The installer
elevates once to place files in `C:\Program Files`; that elevation is
contained to the install step.

### 5. It indexes filenames, not contents

The index stores paths and nothing else. No file is ever opened for reading
during indexing. Selected results are handed to the shell for
association-based opening.

## Dependency policy

**No runtime dependencies.** The installed product uses only the .NET base
class library. Development-only packages (xunit, Microsoft.NET.Test.Sdk,
coverlet) are pinned in `tests/Wff.Tests` and are not part of any shipped
artifact. This small surface is deliberate — there is no supply chain to
attack beyond the SDK you already trust.

## Disclosure policy

- Reported issues are acknowledged within a few days.
- Confirmed issues get a patch release and a public advisory after a fix is
  available.
- Reporter credit is given in the release notes unless anonymity is requested.
- Safe harbour: good-faith research that follows this policy will not face
  legal action from the project.

## Contact

**Rehan Khan** — [rekhan123451@live.com](mailto:rekhan123451@live.com) ·
[github.com/maxico714](https://github.com/maxico714)
