# M2 Indicator NT8 (M2Zones) — Working Notes

- Master copy of the indicator is `M2Zones.cs` in this folder (git repo, pushed to `github.com/Braunn318/M2_Indicator_NT8`, public).
- Install: copy `M2Zones.cs` to `Documents\NinjaTrader 8\bin\Custom\Indicators\Braunn\`, then NinjaScript Editor → F5. Keep the installed copy identical to the one here.
- Spec: `ZADANI_M2_INDIKATOR.md` in the Trading coach vault (`F:\Claude workspace\Trading\Trading coach\`).
- Branches: `main` = stable, `test` = in-progress work, merged into `main` once the user verifies it in NT8.
- One changelog only: `CHANGELOG.txt`, newest on top, `## <version> - <date>`. Also bump the version note in the `M2Zones.cs` header and the README "Historie".
- Releases: `release_test` / `release_final` skills in `.claude/skills/` (also linked from the journal's skills in `F:\Trading\Denik\DEV\.claude\skills\`). Tags `vX.Y.Z-test` (pre-release on `test`) and `vX.Y.Z` (Latest on `main`), asset `M2Zones.cs`.
- Repo is public: no account data, API keys or real trade numbers in tracked files.
