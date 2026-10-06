# Support — win-fast-finder

This project is maintained by a single person in their spare time, so please
read the right document before opening an issue — it will get you a faster
answer.

## I need help using the app

**Start here:** [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)

It covers the common cases — hotkey not responding, empty search results,
runtime missing, SmartScreen warnings — and explains how to read the log at
`%APPDATA%\win-fast-finder\log.txt`, which answers most questions on its own.

For feature reference, see [docs/USAGE.md](docs/USAGE.md).

## I found a bug

Open a [bug report](https://github.com/maxico714/win-fast-finder/issues/new?template=bug_report.yml).

Please include:

| Required | Detail |
|---|---|
| ✅ | Windows version (`winver`) |
| ✅ | Installed release, or built from source |
| ✅ | Complete `log.txt` from `%APPDATA%\win-fast-finder\` |
| ✅ | Steps to reproduce |
| ✅ | Expected vs. actual behaviour |

The log is the single most valuable thing you can attach. If you have a
screenshot of a crash, include that too.

## I have a security concern

**Do not open a public issue.** Follow [SECURITY.md](SECURITY.md) and report
privately to [rekhan123451@live.com](mailto:rekhan123451@live.com).

## I want a feature

Open a [feature request](https://github.com/maxico714/win-fast-finder/issues/new?template=feature_request.yml).
Describe the **problem** you're solving, not just the solution — that helps
decide whether it belongs in scope.

Scope is deliberately tight. See [docs/ROADMAP.md](docs/ROADMAP.md) for what
is planned and what is parked. The short version of what is *not* coming:

- Content (file contents) search
- Cloud sync or any network feature
- Paid tiers
- Non-Windows platforms

## I want to contribute

Wonderful. Read [CONTRIBUTING.md](CONTRIBUTING.md) first — it covers the test
discipline (write a failing test, make it pass, mutation-verify) that every
change is held to.

## Direct contact

**Rehan Khan**

- Email: [rekhan123451@live.com](mailto:rekhan123451@live.com)
- GitHub: [github.com/maxico714](https://github.com/maxico714)

Email is best for security reports and anything that shouldn't be public.
Please allow a few days for a reply — this is a spare-time project.

## Response expectations

Honestly set so nobody waits forever:

| Kind | Typical response |
|---|---|
| Bug with a full log attached | Days, sometimes faster |
| Bug with no log | Requested again first |
| Feature request | Acknowledged; may be declined on scope |
| Security report | Acknowledged within a few days |
| "Is this maintained?" | Yes — see the [changelog](CHANGELOG.md) and [releases](https://github.com/maxico714/win-fast-finder/releases) |
