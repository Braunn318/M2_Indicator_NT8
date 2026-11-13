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
| Logika | CandidateMode | AllPending | **1.1**: AllPending = každá negativní / pozitivní svíčka čeká, dokud se nepotvrdí nebo nezruší; OnlyLast = chování 1.0.x (jen pro porovnání); **1.2**: HH_LL_OB_GAP = varianta HH/LL/OB/GAP (viz níže) |
| Logika | MaxPendingBars | 0 | **1.1**: kandidát starší než N svíček se zahodí (jen výkon); 0 = bez limitu |
| Logika | NewestConfirmedOnly | ano | **1.1.1**: když jedna svíčka potvrdí víc kandidátů, kreslí se jen nejnovější; starší čekající ve stejném směru se zahodí (žádné dva boxy v sobě). Vypnuto = chování 1.1.0 |
| Ladění | DebugLog | ne | **1.1**: výpis každé události do NinjaScript Output (viz kap. 4) |
| Vzhled | Long – netestovaná / Long – testovaná | lime / lime | barva long M2 podle stavu; výchozí stejná, liší se krytím |
| Vzhled | Short – netestovaná / Short – testovaná | červená / červená | barva short M2 podle stavu; výchozí stejná, liší se krytím |
| Vzhled | Prolomená | šedá | long i short |
| Vzhled | Tečka testu | černá | tečka v místě prvního testu |
| Vzhled | Krytí – netestovaná / testovaná / prolomená | 70 / 12 / 8 % | krytí boxu |
| Vzhled | EdgeWidth | 1 | tloušťka hrany |
| Vzhled | ShowTestMarker | ano | tečka na hraně v místě prvního testu |
| Vzhled | ShowChartLabel | ne | vypisovat název indikátoru vlevo nahoře v grafu (v okně Indicators je název vidět vždy) |
| Svíčky | EnableCandleColoring | ne | obarvení svíček podle close nad / pod 50 % range |
| Svíčky | ColorAbove50 / ColorBelow50 | zelená / červená | |

## 4. Jak kreslí
- **M2 long** = svíčka s close pod 50 % range (barva nerozhoduje), potvrzená první pozdější
  svíčkou celou nad jejím high – **nemusí jít o hned další svíčku**. Short zrcadlově. Close přesně na 50 % = není M2.
- **Čekající kandidáti (1.1, AllPending)**: každá negativní svíčka čeká na potvrzení, i když mezitím vznikne
  novější negativní – ta ji nepřepíše. Kandidát se zruší, když nějaká svíčka **zavře** pod jeho low dřív, než
  přišlo potvrzení (short: zavře nad high). Jedna svíčka může potvrdit víc kandidátů najednou.
  Pořadí na každé svíčce: potvrzení / zrušení čekajících → test / prolomení zón → svíčka sama jako nový kandidát.
  Čekající kandidáti se nekreslí. `CandidateMode = OnlyLast` vrátí chování 1.0.x (novější kandidát starého přepíše).
- **DebugLog**: zapnout v nastavení indikátoru (Ladění → DebugLog), výpis je v **New → NinjaScript Output**.
  Řádek: `čas M2 | LONG/SHORT | KANDIDÁT / POTVRZENO svíčkou HH:mm:ss / ZRUŠENO svíčkou … / TEST … / PROLOMENO … | high | low`.
  Časy jsou časy svíček na ose grafu NT8. Na 10 dnech 1min dat je výpis dlouhý – zapínat jen na kontrolu.
- **Jen nejnovější potvrzená (1.1.1, NewestConfirmedOnly)**: když jedna svíčka potvrdí víc čekajících kandidátů,
  M2 je jen ten nejnovější a všichni starší čekající ve stejném směru se zahodí (DebugLog: `NAHRAZENO …`).
  Potvrzená M2 se kreslí i tehdy, když po ní vznikl nový, zatím nepotvrzený kandidát (ten čeká dál).
- **Varianta HH/LL/OB/GAP (1.2, CandidateMode = HH_LL_OB_GAP, `docs/UPRESNENI_M2.md`)**: pattern = M2 … potvrzovací
  svíčka, uvnitř žádná další M2. Ze všech čekajících kandidátů ve směru vyhrává nejlepší: **123Gap** (svíčka 3 celá
  za svíčkou 1 – svíčka 1 je M2 bez ohledu na cokoli) > **outside** svíčka (high ≥ a low ≤ předchozí svíčky, aspoň
  jedno ostře; dokud čeká, potvrzení ostatních se ignoruje) > short **vyšší low** / long **nižší high**. Po potvrzení
  se ostatní čekající ve stejném směru zahodí (DebugLog `NAHRAZENO …`) a M2 se hledá znovu. Long a short nezávisle.
- **Box** = range M2 svíčky od M2 po potvrzovací svíčku. **Hrana** = čára od potvrzení doprava až do aktuální
  (tvořící se) svíčky – je vidět hned po potvrzení (1.1.1; dřív končila na poslední uzavřené svíčce).
- **Netestovaná**: sytá barva, plná hrana. **Testovaná** (první dotek hrany po potvrzení): stejná barva, nízké krytí, přerušovaná.
  **Prolomená** (close za opačnou hranou, ne knot): šedá, hrana končí na svíčce prolomení, zůstává v grafu.
- **Nápis M2_L / M2_S (1.2.0, ShowM2Labels)**: prolomená M2 má v pravém horním rohu boxu malý šedý nápis se směrem,
  netestovaná ho má nad pravým koncem hrany (barvou hrany). Testovaná bez nápisu.
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
- [ ] **1.1** ES 12-26, 1 min, 12. 11. 2026: long M2 16:51 se vykreslí (box 16:51 → 16:54, hrana ≈ 7628,50),
      i když 16:53 vznikla negativní svíčka. V DebugLog řádek `… 16:51:00 | LONG | POTVRZENO svíčkou 16:54:00 | …`.
- [ ] **1.1** CandidateMode = OnlyLast: M2 16:51 zmizí (staré chování).
- [ ] **1.1.1** ES 12-26, 1 min, 13. 11. 2026: svíčka 16:30 potvrdí jen M2 16:26 (jeden box), 16:22 se nekreslí.
- [ ] **1.1.1** Hrana nové M2 je vidět hned na tvořící se svíčce po potvrzení a vede až k ní.
- [ ] **1.2** CandidateMode = HH_LL_OB_GAP: v DebugLog u každé M2 zkontrolovat, že vyhrál gap / outside / nejlepší
      low (high) a ostatní jsou `NAHRAZENO`; mezi M2 a potvrzovací svíčkou žádná další M2 ve stejném směru.
- [ ] **1.1** Porovnání s CW Challenger (M2), 12. 11. 2026 14:00–16:45: každý rozdíl jde vysvětlit pravidly (DebugLog).

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
- **1.2.0 (2026-11-13)** – nová varianta CandidateMode = HH_LL_OB_GAP (HH/LL/OB/GAP): vyhrává 123Gap > outside
  svíčka > vyšší low (short) / nižší high (long); po potvrzení se ostatní čekající zahodí. Default zůstává AllPending.
  Nápis M2_L / M2_S (ShowM2Labels): prolomená v pravém horním rohu boxu, netestovaná nad pravým koncem hrany.
- **1.1.1 (2026-11-13)** – když jedna svíčka potvrdí víc kandidátů, kreslí se jen nejnovější M2 (NewestConfirmedOnly,
  default; žádné dva boxy v sobě). Hrana vede až do aktuální svíčky a je vidět hned po potvrzení.
- **1.1.0 (2026-11-12)** – opraveno: M2 potvrzená až po vzniku novější negativní (pozitivní) svíčky se nevykreslila
  (např. ES 12. 11. 16:51). Nový režim CandidateMode = AllPending (default): všichni kandidáti čekají, dokud se
  nepotvrdí nebo nezruší (close za opačnou hranou). OnlyLast = staré chování. Nové MaxPendingBars a DebugLog.
- **1.0.4 (2026-11-04)** – opraveno: neutrální svíčka (close přesně na 50 % range) se u CL, GC, 6E, SI… kvůli
  zaokrouhlení desetinných čísel často brala jako negativní / pozitivní a mohla se stát M2. Teď je vždy neutrální.
- **1.0.3 (2026-11-01)** – opraveno: starší netestovaná M2 se kreslila jako testovaná (režim Dim, zrušen –
  vzhled určuje jen stav); vlastní barva pro každý stav (long/short × netestovaná/testovaná, prolomená, tečka testu).
- **1.0.2 (2026-11-01)** – netestovaná M2 sytější (výchozí OpacityUntested 70 %, hrana 100 % a +1 px).
- **1.0.1 (2026-11-01)** – název M2Zones je vidět v okně Indicators (dřív prázdný řádek); nová volba
  ShowChartLabel; kreslení drží zámek jen na kopii dat (datové vlákno nečeká na vykreslení); zmrazené brushe.
- **1.0 (2026-10-31)** – první verze.
