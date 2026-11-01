# M2Zones – postup pro Adama

Vlastní M2 indikátor pro NinjaTrader 8 podle `ZADANI_M2_INDIKATOR.md` (vault Trading coach).

## 1. Instalace
1. `M2Zones.cs` už je zkopírovaný v `Documents\NinjaTrader 8\bin\Custom\Indicators\Braunn\`
   (master kopie je tady, v `F:\Trading\DEV\M2 Indicator\`, repozitář
   [github.com/Braunn318/M2_Indicator_NT8](https://github.com/Braunn318/M2_Indicator_NT8)).
2. NT8 → **New → NinjaScript Editor** → otevřít `Indicators\M2Zones` → **F5** (kompilace).
   Dole nesmí být žádná chyba. (Ověřeno: soubor jde zkompilovat proti DLL z tvé instalace NT8.)

## 2. Přidání na graf
- Graf → **Indicators…** (Ctrl+I) → `M2Zones` → Add → OK.
- `CandleBreakoutPremiumV2` z grafu sundej, ať se zóny nepřekrývají.

## 3. Parametry
| Skupina | Parametr | Default | Co dělá |
|---|---|---|---|
| Logika | ShowLong / ShowShort | ano / ano | kreslit long / short M2 |
| Logika | PreviousM2Mode | Dim | starší M2 ve stejném směru: Keep = podle stavu, Dim = jako testovaná, Hide = nekreslit |
| Logika | ShowBroken | ano | prolomené M2 šedě (backtest); vypnuto = čistší live graf |
| Logika | MaxHistoryBars | 2000 | jak daleko dozadu držet prolomené zóny (0 = bez limitu) |
| Vzhled | LongColor / ShortColor / BrokenColor | zelená / červená / šedá | |
| Vzhled | OpacityUntested / Tested / Broken | 35 / 12 / 8 % | krytí boxu |
| Vzhled | EdgeWidth | 1 | tloušťka hrany |
| Vzhled | ShowTestMarker | ano | tečka na hraně v místě prvního testu |
| Vzhled | ShowChartLabel | ne | vypisovat název indikátoru vlevo nahoře v grafu (v okně Indicators je název vidět vždy) |
| Svíčky | EnableCandleColoring | ne | obarvení svíček podle close nad / pod 50 % range |
| Svíčky | ColorAbove50 / ColorBelow50 | zelená / červená | |

## 4. Jak kreslí
- **M2 long** = poslední svíčka s close pod 50 % range (barva nerozhoduje), potvrzená první pozdější
  svíčkou celou nad jejím high – **nemusí jít o hned další svíčku**. Novější negativní svíčka před
  potvrzením starého kandidáta zruší. Short zrcadlově. Close přesně na 50 % = není M2.
- **Box** = range M2 svíčky od M2 po potvrzovací svíčku. **Hrana** = čára od potvrzení doprava.
- **Netestovaná**: plná barva. **Testovaná** (první dotek hrany po potvrzení): slabší, přerušovaná.
  **Prolomená** (close za opačnou hranou, ne knot): šedá, hrana končí na svíčce prolomení, zůstává v grafu.
- Hrana a obrys boxu mají krytí 3× krytí boxu (min. 25 %), aby byly čitelné i u slabých stavů.
- „Aktivní" M2 v každém směru = nejnovější potvrzená neprolomená. Když se prolomí, aktivní se stane
  předchozí neprolomená.

## 5. Co ověřit (§7 zadání)
- [ ] Seznam **Drawing Objects** (Ctrl+B) je prázdný – indikátor nevytváří žádné objekty.
- [ ] Vlevo nahoře v grafu není text `M2Zones(...)` a v Data Boxu žádný řádek.
      *Pokud text vlevo nahoře přesto zůstane, napiš mi – řešením je krátký `Name` místo prázdného `DisplayName`.*
- [ ] ES 12-26, 1 min, 21:00–21:12 (screen `M2-indikator-LTA-screen.png`): M2 svíčka s deltou 97 se
      potvrdí svíčkou s deltou 391 a vykreslí se; starší M2 (delta −1009) podle PreviousM2Mode.
- [ ] Po doteku hrany zóna zeslábne; po zavření za opačnou hranou zešedne a přestane se prodlužovat.
- [ ] **Playback**: zóna se objeví až po uzavření potvrzovací svíčky a nikdy se neposune ani nezmizí
      jinak než změnou stavu. Přechod Historical → Realtime zóny nesmaže.
- [ ] 3 replay dny MES: indikátor označí stejné M2 jako ty ručně. Rozdíly oproti CandleBreakoutPremiumV2
      by měly jít vysvětlit definicí (hlavně potvrzení o víc svíček později).
- [ ] Změna barev / krytí se projeví bez chyb; graf několikrát zavřít a otevřít (Log bez chyb).

## 6. Když replay / OF graf nic neukazuje (není to M2Zones)
Prověřeno 1. 11. 2026:
- **Playback nepřehrává, i když čas běží** → pro **ES 12-26 / MES 12-26 nejsou stažená Market Replay
  data** (v `db\replay` je jen ES 06-26 a ES 09-26). Řešení: Control Center → Tools → Historical Data →
  Load → Market Replay, ES 12-26 (a MES 12-26), vybrat dny → Download. Nebo v okně Playback zvolit
  **Historical** – použije stažená tick data (ES 12-26 do 30. 10.).
- **OF graf je prázdný o víkendu** → OF taby (ES 1M / 3M / 30M OF) mají v Data Series **Days to load = 0**,
  tj. jen dnešek. Mimo session = 0 barů. Nastav Days to load aspoň 2–3 (Data Series, Ctrl+F).
- Chyba v Logu `Indicator 'Footprints' … only 4 bars` patří vendor indikátoru Footprints (málo barů), ne M2Zones.

## Historie
- **1.0.1 (2026-11-01)** – název M2Zones je vidět v okně Indicators (dřív prázdný řádek); nová volba
  ShowChartLabel; kreslení drží zámek jen na kopii dat (datové vlákno nečeká na vykreslení); zmrazené brushe.
- **1.0 (2026-10-31)** – první verze.
