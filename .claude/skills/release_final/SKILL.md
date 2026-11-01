---
name: release_final
description: Ship a verified M2Zones (NinjaTrader 8 indicator) release once the user has checked the `test` branch in NT8 — merges test into main (fast-forward), publishes a GitHub release marked Latest in Braunn318/M2_Indicator_NT8 with M2Zones.cs attached. Use when the user says "release_final", "releasni main", "vydej finální verzi" while working on the M2 indicator.
---

# Release Final — M2Zones (NT8 indicator)

Ships a version the user has already checked in NT8 on `test`. Same idea as `release_test` in this folder (`.claude/skills/release_test/SKILL.md`); read it for the compile check and the notes format.

Repo: `F:\Trading\DEV\M2 Indicator` → `github.com/Braunn318/M2_Indicator_NT8` (public). Pass `--repo Braunn318/M2_Indicator_NT8` to `gh`.

No installer, no zip: the only deliverable is `M2Zones.cs`.

## Before running

- The version in `CHANGELOG.txt` (top `## X.Y.Z`), the `M2Zones.cs` header and `README.md` "Historie" are the same. **Never bump here**: the test builds and the final release share the number, only the tag differs (`v<X.Y.Z>-test`, `-test.2` … → `v<X.Y.Z>`).
- The user has said it works in NT8.
- Tag `v<X.Y.Z>` must not exist yet.

## Steps

1. **State.** On `test`, `git status --short` clean (commit and push any pending work on `test` first, same staging rules as release_test).
2. **Compile check** as in release_test step 2. Stop on errors.
3. **Installed copy** in `Documents\NinjaTrader 8\bin\Custom\Indicators\Braunn\M2Zones.cs` is identical to the repo (hash). If not, copy it and say so.
4. **Merge:**
   ```bash
   git fetch origin && git checkout main && git merge --ff-only test && git push origin main && git checkout test
   ```
   `--ff-only` is intentional; if it fails, stop and investigate (main diverged).
5. **Release notes**: CHANGELOG sections since the last *final* release (normally just `## X.Y.Z`) + the install line from release_test (quoted heredoc, not printf).
6. **Publish without asking again** (running release_final is the approval):
   ```bash
   gh release create v<X.Y.Z> M2Zones.cs --repo Braunn318/M2_Indicator_NT8 --target main --latest --title "M2Zones <X.Y.Z>" --notes-file <notes>
   ```
7. **Verify**: `gh release view v<X.Y.Z> --repo Braunn318/M2_Indicator_NT8 --json tagName,targetCommitish,isPrerelease,assets` → target `main`, not a pre-release, asset `M2Zones.cs`; `git log -1 main` = `test` HEAD; current branch is `test`.
8. **Tell the user** the tag and a one-line summary, and that `main` = `test`.
