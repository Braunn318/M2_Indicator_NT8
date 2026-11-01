---
name: release_test
description: Release the current in-progress M2Zones (NinjaTrader 8 indicator) work on the `test` branch only — compile check against the NT8 DLLs, install into NT8, commit and push test, publish a tagged GitHub PRE-release targeting `test` in Braunn318/M2_Indicator_NT8 with M2Zones.cs attached. Never touches `main`. Use when the user says "release_test", "release na test" while working on the M2 indicator.
---

# Release Test — M2Zones (NT8 indicator)

Everything stays on `test`: commit, push, **pre-release** targeting `test`. `main` is never merged, checked out for writing or pushed. Merging into `main` waits for the user's check in NT8 (`release_final`).

Repo: `F:\Trading\DEV\M2 Indicator` → `github.com/Braunn318/M2_Indicator_NT8` (public; no account data, API keys or real trade numbers). Pass `--repo Braunn318/M2_Indicator_NT8` to `gh`.

Not this project: Futures Journal PRO and Backtest Lab have their own skills (`F:\Trading\Denik\DEV\.claude\skills\`, `F:\Trading\Denik\BackTestLab DEV\.claude\skills\`).

No installer, no zip: the only deliverable is `M2Zones.cs` (the user compiles it in NT8 with F5).

## Before running

- The version is in three places, all the same: top entry `## X.Y.Z - <date>` in `CHANGELOG.txt`, the version note in the `M2Zones.cs` header, and `README.md` "Historie". Don't invent a number; ask if it's unclear. Numbering: +1 per batch of work (1.0.3 → 1.0.4), no skipping. One number covers all test builds and the final release of that version.
- **Tag = `v<X.Y.Z>-test`**. If it already exists (`git tag -l`, `gh release list`), use `v<X.Y.Z>-test.<N>` with the next free N. Never overwrite a tag.
- Versions in `CHANGELOG.txt` with no published tag go into the release notes too.

## Steps

1. **State.** `git branch --show-current` — if not `test`, `git checkout test` (stop if blocked). `git status --short`.

2. **Compile check — stop and report on any error.** The engine block alone, plus the full indicator against the NT8 DLLs (the generated-code region is cut off; it would clash with the NinjaTrader.Custom.dll that is already built):
   ```bash
   D="<scratchpad>/m2build"; mkdir -p "$D"
   NT="E:/Programme/NinjaTrader/bin"   # if missing: (Get-Process NinjaTrader).Path, or ask
   CU="$USERPROFILE/Documents/NinjaTrader 8/bin/Custom"
   FW="C:/Windows/Microsoft.NET/Framework64/v4.0.30319"
   sed '/#region NinjaScript generated code/,$d' "/f/Trading/DEV/M2 Indicator/M2Zones.cs" > "$D/full.cs"
   "$FW/csc.exe" -nologo -nowarn:0436,1702 -target:library -out:"$D/full.dll" \
     -r:"$NT/NinjaTrader.Core.dll" -r:"$NT/NinjaTrader.Gui.dll" -r:"$NT/SharpDX.dll" -r:"$NT/SharpDX.Direct2D1.dll" \
     -r:"$CU/NinjaTrader.Custom.dll" -r:"$FW/WPF/PresentationCore.dll" -r:"$FW/WPF/PresentationFramework.dll" \
     -r:"$FW/WPF/WindowsBase.dll" -r:"$FW/System.Xaml.dll" -r:"$FW/System.ComponentModel.DataAnnotations.dll" \
     -r:"$FW/System.Xml.dll" "$D/full.cs"
   ```
   Also check the generated region matches the `[NinjaScriptProperty]` properties (parameter list of the `M2Zones(...)` overloads and the cache compare).

3. **Install into NT8.** Copy `M2Zones.cs` → `Documents\NinjaTrader 8\bin\Custom\Indicators\Braunn\M2Zones.cs` and check that the hashes match (`Get-FileHash`).

4. **Commit on `test`.** Stage `M2Zones.cs`, `CHANGELOG.txt`, `README.md`, `CLAUDE.md`, `.claude/`. Never stage build output (`bin/`, `obj/`, `*.dll`) or NT8 export zips. Commit message summarizing the release + the attribution line from the session reminder, then `git push origin test`.

5. **Release notes** = the `CHANGELOG.txt` section(s) of this version (and unpublished versions before it), plus one line:
   `Instalace: zkopírovat M2Zones.cs do Documents\NinjaTrader 8\bin\Custom\Indicators\Braunn\, v NinjaScript Editoru F5.`
   Write the file with a quoted heredoc (`<<'EOF'`), not `printf`: `\b` in `\bin\` gets eaten otherwise.

6. **Publish without asking again** (running release_test is the approval):
   ```bash
   gh release create v<X.Y.Z>-test M2Zones.cs --repo Braunn318/M2_Indicator_NT8 --target test --prerelease --title "M2Zones <X.Y.Z> (test)" --notes-file <notes>
   ```

7. **Verify**: `gh release view <tag> --repo Braunn318/M2_Indicator_NT8 --json tagName,isPrerelease,targetCommitish,assets` → pre-release, target `test`, asset `M2Zones.cs`; the notes contain the correct install path; `main` unchanged.

8. **Tell the user**: the tag, a one-line summary, that the file is already installed in NT8 (F5 in NinjaScript Editor, then F5 in the chart), and that a chart that is already open keeps its saved colours / settings (new defaults apply only to a newly added indicator). Merging into `main` waits for their check.
