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
| Logika | ShowBroken | ano | prolomené M2 šedě (backtest); vypnuto = čistší live graf |
| Logika | MaxHistoryBars | 2000 | jak daleko dozadu držet prolomené zóny (0 = bez limitu) |
| Vzhled | Long – netestovaná / Long – testovaná | lime / olivová | barva long M2 podle stavu |
| Vzhled | Short – netestovaná / Short – testovaná | červená / tmavě oranžová | barva short M2 podle stavu |
| Vzhled | Prolomená | šedá | long i short |
| Vzhled | Tečka testu | černá | tečka v místě prvního testu |
| Vzhled | Krytí – netestovaná / testovaná / prolomená | 70 / 12 / 8 % | krytí boxu |
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
- **Netestovaná**: sytá barva, plná hrana. **Testovaná** (první dotek hrany po potvrzení): jiná barva, slabší, přerušovaná.
  **Prolomená** (close za opačnou hranou, ne knot): šedá, hrana končí na svíčce prolomení, zůstává v grafu.
- Vzhled určuje **jen stav**. Starší M2 (napravo už je novější ve stejném směru) se kreslí úplně stejně
  jako nejnovější – netestovaná zůstane netestovaná.
- Hrana a obrys testované / prolomené mají krytí 3× krytí boxu (min. 25 %); netestovaná má hranu vždy 100 %.

## 5. Co ověřit (§7 zadání)
- [ ] Seznam **Drawing Objects** (Ctrl+B) je prázdný – indikátor nevytváří žádné objekty.
- [ ] Vlevo nahoře v grafu není text `M2Zones(...)` a v Data Boxu žádný řádek.
      *Pokud text vlevo nahoře přesto zůstane, napiš mi – řešením je krátký `Name` místo prázdného `DisplayName`.*
- [ ] ES 12-26, 1 min, 21:00–21:12 (screen `M2-indikator-LTA-screen.png`): M2 svíčka s deltou 97 se
      potvrdí svíčkou s deltou 391 a vykreslí se; starší M2 (delta −1009) se kreslí podle svého stavu.
- [ ] ES 12-26, 1 min, 30. 10. 20:50: netestovaná long M2 je sytá s plnou hranou, i když napravo jsou novější long M2.
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
- **1.0.3 (2026-11-01)** – opraveno: starší netestovaná M2 se kreslila jako testovaná (režim Dim, zrušen –
  vzhled určuje jen stav); vlastní barva pro každý stav (long/short × netestovaná/testovaná, prolomená, tečka testu).
- **1.0.2 (2026-11-01)** – netestovaná M2 sytější (výchozí OpacityUntested 70 %, hrana 100 % a +1 px).
- **1.0.1 (2026-11-01)** – název M2Zones je vidět v okně Indicators (dřív prázdný řádek); nová volba
  ShowChartLabel; kreslení drží zámek jen na kopii dat (datové vlákno nečeká na vykreslení); zmrazené brushe.
- **1.0 (2026-10-31)** – první verze.
