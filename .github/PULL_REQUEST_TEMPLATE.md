## Summary

<!-- What does this PR do, and why? Link related issues with "Fixes #123". -->

## Type of change

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing behaviour to change)
- [ ] Documentation only
- [ ] Refactor / internal cleanup

## Test discipline

Every behavioural change needs a test that:
1. **Fails before** this change
2. **Passes after** it
3. Was **mutation-verified** (you deliberately broke the new code path and saw the test fail)

- [ ] I added/updated tests for this change
- [ ] I confirmed the new test fails without my code change
- [ ] I mutation-verified the test
- [ ] `dotnet build Wff.sln --no-incremental` — 0 warnings, 0 errors
- [ ] `dotnet test tests/Wff.Tests/Wff.Tests.csproj --no-build` — all green

Skip the test-discipline boxes only for pure documentation changes.

## Checklist

- [ ] No new runtime dependencies (base class library only — see CONTRIBUTING.md)
- [ ] No network code added (see docs/PRIVACY.md)
- [ ] No elevation required at runtime
- [ ] Documentation updated if behaviour changed (README, docs/USAGE.md, docs/ARCHITECTURE.md as applicable)
- [ ] CHANGELOG.md entry added under **Unreleased**

## Notes for the reviewer

<!-- Anything tricky, anything you'd like a second opinion on, screenshots for UI changes. -->
