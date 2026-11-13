// M2Zones – M2 zóny pro NinjaTrader 8 (zadání ZADANI_M2_INDIKATOR.md v1.1, 12. 11. 2026)
//
// - Žádné Draw.* objekty: vše se kreslí v OnRender přes SharpDX.
// - Žádný popisek vlevo nahoře (v grafu DisplayName = "", lze zapnout ShowChartLabel) a žádný řádek v Data Boxu.
// - Netestovaná / testovaná / prolomená M2; prolomená zůstává v grafu šedě.
// - Vzhled určuje jen stav (netestovaná / testovaná / prolomená), nikdy stáří zóny.
// - Calculate.OnBarClose, bez repaintu. Čekající kandidáti se nekreslí.
//
// 1.2.0 (2026-11-13): CandidateMode = HH_LL_OB_GAP (varianta HH/LL/OB/GAP, docs/UPRESNENI_M2.md) – z čekajících
//                     kandidátů vyhrává 123Gap (svíčka 3 celá za svíčkou 1) > outside svíčka (na její potvrzení se čeká)
//                     > short vyšší Low / long nižší High. Po potvrzení se ostatní čekající ve směru zahodí. Výchozí
//                     režim zůstává AllPending.
// 1.1.1 (2026-11-13): NewestConfirmedOnly (default) – když svíčka potvrdí víc kandidátů, kreslí se jen nejnovější
//                     a starší čekající ve stejném směru se zahodí (žádné dva boxy v sobě). Hrana neprolomené M2
//                     vede až do aktuální (tvořící se) svíčky, je vidět hned po potvrzení.
// 1.1.0 (2026-11-12): režim „všichni čekající" (CandidateMode = AllPending, default) – každá negativní / pozitivní
//                     svíčka čeká, dokud se nepotvrdí nebo nezruší (close za opačnou hranou); novější ji nepřepíše.
//                     Pořadí na svíčce: potvrzení / zrušení → stav zón → nový kandidát. OnlyLast = chování 1.0.x.
//                     Nové MaxPendingBars (0 = bez limitu) a DebugLog (výpis do NinjaScript Output).
// 1.0.4 (2026-11-04): neutrální svíčka (close přesně na 50 % range) se už nikdy nebere jako M2 – dřív se u CL, GC,
//                     6E, SI… kvůli zaokrouhlení double často vyhodnotila jako negativní / pozitivní.
// 1.0.3 (2026-11-01): zrušeno ztlumení starších M2 (Dim kreslil starší netestovanou jako testovanou);
//                     vlastní barva pro každý stav (long/short × netestovaná/testovaná, prolomená, tečka testu).
// 1.0.2 (2026-11-01): netestovaná M2 sytější – výchozí OpacityUntested 70 %, hrana vždy 100 % a o 1 px silnější.
// 1.0.1 (2026-11-01): název je vidět v okně Indicators (dříve prázdný řádek); OnRender drží zámek
//                     jen na kopii dat, NT API volá až mimo zámek; zmrazené brushe; ShowChartLabel.
//
// Zdroják: F:\Trading\DEV\M2 Indicator\M2Zones.cs (github.com/Braunn318/M2_Indicator_NT8)
// Instalace: Documents\NinjaTrader 8\bin\Custom\Indicators\Braunn\M2Zones.cs, kompilace F5 v NinjaScript Editoru.

#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript.Indicators.M2ZonesCore;
#endregion

// Zrušeno v 1.0.3 – zůstává jen kvůli načtení starších workspace / šablon (vlastnost PreviousM2Mode je skrytá a nic nedělá).
public enum M2ZonesPreviousMode
{
	Keep,
	Dim,
	Hide
}

// Namespace .Braunn = složka „Braunn“ v seznamu indikátorů NT8 (stejně jako TradeSaber). Neměnit.
namespace NinjaTrader.NinjaScript.Indicators.Braunn
{
	public class M2Zones : Indicator
	{
		// Styly vykreslení
		private const int StyleUntested	= 0;
		private const int StyleTested	= 1;
		private const int StyleBroken	= 2;

		private M2Engine engine;

		// [směr 0 = long, 1 = short][styl]; broken je pro oba směry stejná barva.
		private SharpDX.Direct2D1.Brush[,]		fillDx	= new SharpDX.Direct2D1.Brush[2, 3];
		private SharpDX.Direct2D1.Brush[,]		edgeDx	= new SharpDX.Direct2D1.Brush[2, 3];
		private SharpDX.Direct2D1.Brush			markerDx;
		private SharpDX.Direct2D1.StrokeStyle	dashStyle;

		// Data jedné zóny pro vykreslení – kopie pořízená pod zámkem, kreslí se až mimo něj.
		private struct ZoneView
		{
			public bool		IsLong;
			public int		M2Bar;
			public int		ConfBar;
			public int		EndBar;
			public int		TestBar;
			public int		Style;
			public double	High;
			public double	Low;
			public double	Edge;
		}

		// Jen pro vlákno OnRender.
		private readonly List<ZoneView> views = new List<ZoneView>();

		// Jen pro datové vlákno (DebugLog).
		private readonly List<M2Event> pendingEvents = new List<M2Event>();

		private const string M2ZonesVersion = "1.2.0";

		// V okně Indicators (klony ve SetDefaults / Configure) je vidět název.
		// V běžícím grafu se text vlevo nahoře nevypisuje, pokud není zapnuté ShowChartLabel.
		public override string DisplayName
		{
			get { return State < State.DataLoaded || ShowChartLabel ? Name : string.Empty; }
		}

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "M2 zóny podle definic M2 (close vůči 50 % range, potvrzení celou svíčkou za hranou). Kreslí bez drawing objektů.";
				Name						= "M2Zones";
				Calculate					= Calculate.OnBarClose;
				IsOverlay					= true;
				DisplayInDataBox			= false;
				ShowTransparentPlotsInDataBox = false;
				PaintPriceMarkers			= false;
				DrawOnPricePanel			= true;
				IsSuspendedWhileInactive	= true;
				ScaleJustification			= ScaleJustification.Right;

				ShowLong					= true;
				ShowShort					= true;
				PreviousM2Mode				= M2ZonesPreviousMode.Keep;
				ShowBroken					= true;
				MaxHistoryBars				= 2000;
				CandidateMode				= M2ZonesCandidateMode.AllPending;
				MaxPendingBars				= 0;
				NewestConfirmedOnly			= true;
				DebugLog					= false;

				LongUntestedColor			= Brushes.LimeGreen;
				LongTestedColor				= Brushes.LimeGreen;
				ShortUntestedColor			= Brushes.Red;
				ShortTestedColor			= Brushes.Red;
				BrokenColor					= Brushes.Gray;
				TestMarkerColor				= Brushes.Black;
				OpacityUntested				= 70;
				OpacityTested				= 12;
				OpacityBroken				= 8;
				EdgeWidth					= 1;
				ShowTestMarker				= true;
				ShowChartLabel				= false;

				EnableCandleColoring		= false;
				ColorAbove50				= Brushes.LimeGreen;
				ColorBelow50				= Brushes.Red;
			}
			else if (State == State.Configure)
			{
				// Brushe z dialogu / workspace nemusí být zmrazené; BarBrushes se nastavuje z datového vlákna.
				LongUntestedColor	= Frozen(LongUntestedColor);
				LongTestedColor		= Frozen(LongTestedColor);
				ShortUntestedColor	= Frozen(ShortUntestedColor);
				ShortTestedColor	= Frozen(ShortTestedColor);
				BrokenColor			= Frozen(BrokenColor);
				TestMarkerColor		= Frozen(TestMarkerColor);
				ColorAbove50	= Frozen(ColorAbove50);
				ColorBelow50	= Frozen(ColorBelow50);
			}
			else if (State == State.DataLoaded)
			{
				// Zóny vznikají jen tady; přechod Historical -> Realtime je nemaže.
				engine = new M2Engine();
				engine.MaxHistoryBars	= MaxHistoryBars;
				engine.Mode				= CandidateMode;
				engine.MaxPendingBars	= MaxPendingBars;
				engine.NewestConfirmedOnly	= NewestConfirmedOnly;
				engine.CollectEvents	= DebugLog;
				if (DebugLog)
					Print(string.Format("M2Zones {0} | {1} | CandidateMode {2}, MaxPendingBars {3}, NewestConfirmedOnly {4} | čas M2 | směr | událost | high | low",
						M2ZonesVersion, Instrument != null ? Instrument.FullName : "", CandidateMode, MaxPendingBars, NewestConfirmedOnly));
			}
			else if (State == State.Terminated)
			{
				DisposeDx();
			}
		}

		protected override void OnBarUpdate()
		{
			if (engine == null)
				return;

			double h = High[0], l = Low[0], c = Close[0];
			engine.AddBar(CurrentBar, h, l, c);

			if (DebugLog)
				PrintEvents();

			if (EnableCandleColoring)
			{
				if (M2Engine.IsPositive(h, l, c))
					BarBrushes[0] = ColorAbove50;
				else if (M2Engine.IsNegative(h, l, c))
					BarBrushes[0] = ColorBelow50;
			}
		}

		// Ladicí výpis: jeden řádek na událost, mimo zámek enginu. Časy = časy svíček na ose grafu NT8.
		private void PrintEvents()
		{
			pendingEvents.Clear();
			engine.TakeEvents(pendingEvents);

			for (int i = 0; i < pendingEvents.Count; i++)
			{
				M2Event e = pendingEvents[i];
				string what;
				switch (e.Kind)
				{
					case M2EventKind.Candidate:	what = "KANDIDÁT"; break;
					case M2EventKind.Confirmed:	what = "POTVRZENO svíčkou " + BarTime(e.EventBar); break;
					case M2EventKind.Cancelled:	what = "ZRUŠENO svíčkou " + BarTime(e.EventBar); break;
					case M2EventKind.Expired:	what = "ZAHOZENO (MaxPendingBars) " + BarTime(e.EventBar); break;
					case M2EventKind.Tested:	what = "TEST " + BarTime(e.EventBar); break;
					case M2EventKind.Superseded:	what = "NAHRAZENO novější M2 potvrzenou svíčkou " + BarTime(e.EventBar); break;
					default:					what = "PROLOMENO " + BarTime(e.EventBar); break;
				}
				Print(string.Format("{0} | {1} | {2} | {3} | {4}",
					Bars.GetTime(e.M2Bar).ToString("yyyy-MM-dd HH:mm:ss"), e.IsLong ? "LONG" : "SHORT", what,
					Instrument.MasterInstrument.FormatPrice(e.High), Instrument.MasterInstrument.FormatPrice(e.Low)));
			}
			pendingEvents.Clear();
		}

		private string BarTime(int bar)
		{
			return Bars.GetTime(bar).ToString("HH:mm:ss");
		}

		#region Render
		public override void OnRenderTargetChanged()
		{
			DisposeDx();
			if (RenderTarget != null)
				CreateDx();
		}

		private void CreateDx()
		{
			float[] fillOpacity = { Pct(OpacityUntested), Pct(OpacityTested), Pct(OpacityBroken) };

			for (int dir = 0; dir < 2; dir++)
				for (int s = 0; s < 3; s++)
				{
					Brush b = ColorFor(dir == 0, s);
					fillDx[dir, s] = b.ToDxBrush(RenderTarget, fillOpacity[s]);
					edgeDx[dir, s] = b.ToDxBrush(RenderTarget, s == StyleUntested ? 1f : EdgeOpacity(fillOpacity[s]));
				}

			markerDx = (TestMarkerColor ?? Brushes.Black).ToDxBrush(RenderTarget, 1f);

			dashStyle = new SharpDX.Direct2D1.StrokeStyle(RenderTarget.Factory,
				new SharpDX.Direct2D1.StrokeStyleProperties { DashStyle = SharpDX.Direct2D1.DashStyle.Dash });
		}

		private void DisposeDx()
		{
			for (int dir = 0; dir < 2; dir++)
				for (int s = 0; s < 3; s++)
				{
					if (fillDx[dir, s] != null) { fillDx[dir, s].Dispose(); fillDx[dir, s] = null; }
					if (edgeDx[dir, s] != null) { edgeDx[dir, s].Dispose(); edgeDx[dir, s] = null; }
				}

			if (markerDx != null) { markerDx.Dispose(); markerDx = null; }
			if (dashStyle != null) { dashStyle.Dispose(); dashStyle = null; }
		}

		// Barva podle směru a stavu – každý stav má vlastní barvu z nastavení.
		private Brush ColorFor(bool isLong, int style)
		{
			Brush b;
			if (style == StyleBroken)
				b = BrokenColor;
			else if (style == StyleTested)
				b = isLong ? LongTestedColor : ShortTestedColor;
			else
				b = isLong ? LongUntestedColor : ShortUntestedColor;
			return b ?? Brushes.Gray;
		}

		private static Brush Frozen(Brush b)
		{
			if (b == null || b.IsFrozen)
				return b;
			Brush c = b.Clone();
			if (c.CanFreeze)
				c.Freeze();
			return c;
		}

		private static float Pct(int percent)
		{
			return Math.Max(0, Math.Min(100, percent)) / 100f;
		}

		// Hrana je vidět víc než výplň: 3× krytí výplně, v rozsahu 25–100 %.
		private static float EdgeOpacity(float fillOpacity)
		{
			return Math.Max(0.25f, Math.Min(1f, fillOpacity * 3f));
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (engine == null || ChartBars == null || RenderTarget == null)
				return;

			if (dashStyle == null)
				CreateDx();

			int fromIdx		= ChartBars.FromIndex;
			int toIdx		= ChartBars.ToIndex;
			float halfBar	= Math.Max(1f, chartControl.GetBarPaintWidth(ChartBars) / 2f);
			float edgeWidth	= Math.Max(1, EdgeWidth);

			// Neprolomená hrana vede až do aktuální (tvořící se) svíčky – s OnBarClose je to poslední index v ChartBars,
			// o jednu dál než poslední uzavřená. Mění se jen délka čáry, ne stav zóny (žádný repaint).
			int currentBar = ChartBars.Count - 1;

			// 1) Pod zámkem jen kopie dat – žádné volání NT API, aby datové vlákno (OnBarUpdate) nečekalo na kreslení.
			views.Clear();
			lock (engine.Sync)
			{
				int		lastBar		= Math.Max(engine.LastBar, currentBar);
				List<M2Zone> zones	= engine.Zones;

				for (int i = 0; i < zones.Count; i++)
				{
					M2Zone z = zones[i];

					if (z.IsLong ? !ShowLong : !ShowShort)
						continue;

					int endBar = z.State == M2ZoneState.Broken ? z.BrokenBar : lastBar;
					if (endBar < fromIdx || z.M2Bar > toIdx)
						continue;

					// Vzhled = jen stav zóny. Stáří (novější M2 ve stejném směru) na vzhled nemá vliv.
					int style;
					if (z.State == M2ZoneState.Broken)
					{
						if (!ShowBroken)
							continue;
						style = StyleBroken;
					}
					else if (z.State == M2ZoneState.Tested)
						style = StyleTested;
					else
						style = StyleUntested;

					views.Add(new ZoneView
					{
						IsLong	= z.IsLong,
						M2Bar	= z.M2Bar,
						ConfBar	= z.ConfBar,
						EndBar	= endBar,
						TestBar	= z.TestBar,
						Style	= style,
						High	= z.High,
						Low		= z.Low,
						Edge	= z.Edge
					});
				}
			}

			if (views.Count == 0)
				return;

			// 2) Kreslení mimo zámek.
			SharpDX.Direct2D1.AntialiasMode oldMode = RenderTarget.AntialiasMode;
			try
			{
				for (int i = 0; i < views.Count; i++)
				{
					ZoneView z		= views[i];
					int style		= z.Style;
					int endBar		= z.EndBar;
					int dir = z.IsLong ? 0 : 1;
					SharpDX.Direct2D1.Brush fill = fillDx[dir, style];
					SharpDX.Direct2D1.Brush edge = edgeDx[dir, style];
					SharpDX.Direct2D1.StrokeStyle stroke = style == StyleTested ? dashStyle : null;

					float xM2	= chartControl.GetXByBarIndex(ChartBars, z.M2Bar) - halfBar;
					float xConf	= chartControl.GetXByBarIndex(ChartBars, z.ConfBar) + halfBar;
					float xEnd	= chartControl.GetXByBarIndex(ChartBars, endBar) + halfBar;
					float yHigh	= chartScale.GetYByValue(z.High);
					float yLow	= chartScale.GetYByValue(z.Low);
					float yEdge	= chartScale.GetYByValue(z.Edge);

					// Box = range M2 svíčky, od M2 svíčky po potvrzovací svíčku.
					SharpDX.RectangleF rect = new SharpDX.RectangleF(xM2, yHigh, xConf - xM2, yLow - yHigh);
					RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
					RenderTarget.FillRectangle(rect, fill);
					if (stroke != null)
						RenderTarget.DrawRectangle(rect, edge, 1f, stroke);
					else
						RenderTarget.DrawRectangle(rect, edge, style == StyleUntested ? 2f : 1f);

					// Hrana od potvrzovací svíčky doprava – do prolomení, jinak do aktuální svíčky.
					if (xEnd > xConf)
					{
						SharpDX.Vector2 p1 = new SharpDX.Vector2(xConf, yEdge);
						SharpDX.Vector2 p2 = new SharpDX.Vector2(xEnd, yEdge);
						if (stroke != null)
							RenderTarget.DrawLine(p1, p2, edge, edgeWidth, stroke);
						else
							RenderTarget.DrawLine(p1, p2, edge, style == StyleUntested ? edgeWidth + 1 : edgeWidth);
					}

					// Tečka v místě prvního testu.
					if (ShowTestMarker && z.TestBar >= 0)
					{
						RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.PerPrimitive;
						float r = Math.Max(2.5f, edgeWidth + 1.5f);
						float xTest = chartControl.GetXByBarIndex(ChartBars, z.TestBar);
						RenderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(xTest, yEdge), r, r), markerDx);
					}
				}
			}
			finally
			{
				RenderTarget.AntialiasMode = oldMode;
			}
		}
		#endregion

		#region Properties
		// --- Logika ---
		[NinjaScriptProperty]
		[Display(Name = "ShowLong", Description = "Kreslit long M2.", Order = 1, GroupName = "Logika")]
		public bool ShowLong { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "ShowShort", Description = "Kreslit short M2.", Order = 2, GroupName = "Logika")]
		public bool ShowShort { get; set; }

		// Zrušeno v 1.0.3 (vzhled určuje jen stav). Skryté, nic nedělá – jen aby se načetly starší workspace.
		[Browsable(false)]
		public M2ZonesPreviousMode PreviousM2Mode { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "ShowBroken", Description = "Prolomené M2 kreslit šedě (backtest). Vypnuto = skrýt.", Order = 4, GroupName = "Logika")]
		public bool ShowBroken { get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "MaxHistoryBars", Description = "Jak daleko dozadu (v barech) držet prolomené zóny. 0 = neomezeně.", Order = 5, GroupName = "Logika")]
		public int MaxHistoryBars { get; set; }

		// Nové v 1.1. Bez [NinjaScriptProperty] – generovaný M2Zones(showLong, showShort, showBroken, maxHistoryBars)
		// zůstává stejný (volá ho M2 Trend AOS).
		[Display(Name = "CandidateMode", Description = "AllPending = každá negativní / pozitivní svíčka čeká na potvrzení, dokud se nepotvrdí nebo nezruší (close za opačnou hranou). OnlyLast = chování 1.0.x (jen nejnovější kandidát v každém směru), jen pro porovnání. HH_LL_OB_GAP = varianta HH/LL/OB/GAP: vyhrává nejlepší kandidát – 123Gap > outside svíčka (čeká se na její potvrzení) > short vyšší Low / long nižší High; po potvrzení se ostatní čekající ve směru zahodí.", Order = 6, GroupName = "Logika")]
		public M2ZonesCandidateMode CandidateMode { get; set; }

		[Range(0, int.MaxValue)]
		[Display(Name = "MaxPendingBars", Description = "AllPending: kandidát starší než N svíček se zahodí (jen kvůli výkonu). 0 = bez limitu.", Order = 7, GroupName = "Logika")]
		public int MaxPendingBars { get; set; }

		// Nové v 1.1.1. Samostatná vlastnost (ne hodnota CandidateMode), aby se default propsal i do uložených šablon.
		[Display(Name = "NewestConfirmedOnly", Description = "Když jedna svíčka potvrdí víc kandidátů, kreslí se jen nejnovější a starší čekající ve stejném směru se zahodí (žádné dva boxy v sobě). Vypnuto = chování 1.1.0.", Order = 8, GroupName = "Logika")]
		public bool NewestConfirmedOnly { get; set; }

		// --- Ladění ---
		[Display(Name = "DebugLog", Description = "Vypisovat do NinjaScript Output každou událost: kandidát, potvrzení (kým), zrušení, test, prolomení.", Order = 1, GroupName = "Ladění")]
		public bool DebugLog { get; set; }

		// --- Vzhled ---
		[XmlIgnore]
		[Display(Name = "Long – netestovaná", Description = "Barva netestované long M2.", Order = 10, GroupName = "Vzhled")]
		public Brush LongUntestedColor { get; set; }

		[Browsable(false)]
		public string LongUntestedColorSerializable
		{
			get { return Serialize.BrushToString(LongUntestedColor); }
			set { LongUntestedColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Long – testovaná", Description = "Barva testované long M2.", Order = 11, GroupName = "Vzhled")]
		public Brush LongTestedColor { get; set; }

		[Browsable(false)]
		public string LongTestedColorSerializable
		{
			get { return Serialize.BrushToString(LongTestedColor); }
			set { LongTestedColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Short – netestovaná", Description = "Barva netestované short M2.", Order = 12, GroupName = "Vzhled")]
		public Brush ShortUntestedColor { get; set; }

		[Browsable(false)]
		public string ShortUntestedColorSerializable
		{
			get { return Serialize.BrushToString(ShortUntestedColor); }
			set { ShortUntestedColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Short – testovaná", Description = "Barva testované short M2.", Order = 13, GroupName = "Vzhled")]
		public Brush ShortTestedColor { get; set; }

		[Browsable(false)]
		public string ShortTestedColorSerializable
		{
			get { return Serialize.BrushToString(ShortTestedColor); }
			set { ShortTestedColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Prolomená", Description = "Barva prolomené M2 (long i short).", Order = 14, GroupName = "Vzhled")]
		public Brush BrokenColor { get; set; }

		[Browsable(false)]
		public string BrokenColorSerializable
		{
			get { return Serialize.BrushToString(BrokenColor); }
			set { BrokenColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Tečka testu", Description = "Barva tečky v místě prvního testu.", Order = 15, GroupName = "Vzhled")]
		public Brush TestMarkerColor { get; set; }

		[Browsable(false)]
		public string TestMarkerColorSerializable
		{
			get { return Serialize.BrushToString(TestMarkerColor); }
			set { TestMarkerColor = Serialize.StringToBrush(value); }
		}

		// Starší workspace (do 1.0.2) měly jednu barvu na směr – načte se do netestované.
		[Browsable(false)]
		public string LongColorSerializable
		{
			get { return Serialize.BrushToString(LongUntestedColor); }
			set { LongUntestedColor = Serialize.StringToBrush(value); }
		}

		[Browsable(false)]
		public string ShortColorSerializable
		{
			get { return Serialize.BrushToString(ShortUntestedColor); }
			set { ShortUntestedColor = Serialize.StringToBrush(value); }
		}

		[Range(0, 100)]
		[Display(Name = "Krytí – netestovaná (%)", Order = 16, GroupName = "Vzhled")]
		public int OpacityUntested { get; set; }

		[Range(0, 100)]
		[Display(Name = "Krytí – testovaná (%)", Order = 17, GroupName = "Vzhled")]
		public int OpacityTested { get; set; }

		[Range(0, 100)]
		[Display(Name = "Krytí – prolomená (%)", Order = 18, GroupName = "Vzhled")]
		public int OpacityBroken { get; set; }

		[Range(1, 10)]
		[Display(Name = "EdgeWidth", Description = "Tloušťka hrany.", Order = 19, GroupName = "Vzhled")]
		public int EdgeWidth { get; set; }

		[Display(Name = "ShowTestMarker", Description = "Tečka na hraně v místě prvního testu.", Order = 20, GroupName = "Vzhled")]
		public bool ShowTestMarker { get; set; }

		[Display(Name = "ShowChartLabel", Description = "Vypisovat název indikátoru vlevo nahoře v grafu.", Order = 21, GroupName = "Vzhled")]
		public bool ShowChartLabel { get; set; }

		// --- Svíčky ---
		[Display(Name = "EnableCandleColoring", Description = "Obarvit svíčky podle close nad / pod 50 % range.", Order = 20, GroupName = "Svíčky")]
		public bool EnableCandleColoring { get; set; }

		[XmlIgnore]
		[Display(Name = "ColorAbove50", Order = 21, GroupName = "Svíčky")]
		public Brush ColorAbove50 { get; set; }

		[Browsable(false)]
		public string ColorAbove50Serializable
		{
			get { return Serialize.BrushToString(ColorAbove50); }
			set { ColorAbove50 = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "ColorBelow50", Order = 22, GroupName = "Svíčky")]
		public Brush ColorBelow50 { get; set; }

		[Browsable(false)]
		public string ColorBelow50Serializable
		{
			get { return Serialize.BrushToString(ColorBelow50); }
			set { ColorBelow50 = Serialize.StringToBrush(value); }
		}
		#endregion
	}
}

// ==== M2 ENGINE BEGIN ====
// Čistá logika bez závislosti na NinjaTraderu (testuje se samostatně).
// Engine MUSÍ být až za třídou indikátoru: generátor kódu NT8 bere první namespace Indicators.* v souboru
// a s enginem nahoře přepsal generovaný region na neexistující M2ZonesCore.M2Zones (ověřeno 12. 11. 2026,
// i u 1.0.4). Nepřesouvat nahoru.

// AllPending (1.1): každá negativní / pozitivní svíčka čeká, dokud se nepotvrdí nebo nezruší.
// OnlyLast: chování 1.0.x – v každém směru jen nejnovější kandidát, novější ho přepíše. Jen pro porovnání.
// Globální namespace jako M2ZonesPreviousMode (enum vlastnosti indikátoru).
// HH_LL_OB_GAP (1.2, docs/UPRESNENI_M2.md): z čekajících kandidátů vyhrává nejlepší – 123Gap > outside svíčka >
// short vyšší Low / long nižší High. Po potvrzení pattern končí, ostatní čekající ve směru se zahodí.
public enum M2ZonesCandidateMode
{
	AllPending,
	OnlyLast,
	HH_LL_OB_GAP
}

namespace NinjaTrader.NinjaScript.Indicators.M2ZonesCore
{
	using System;
	using System.Collections.Generic;

	public enum M2ZoneState
	{
		Untested,
		Tested,
		Broken
	}

	public class M2Zone
	{
		public bool			IsLong;
		public int			M2Bar;
		public int			ConfBar;
		public double		High;
		public double		Low;
		public M2ZoneState	State		= M2ZoneState.Untested;
		public int			TestBar		= -1;
		public int			BrokenBar	= -1;

		// Hrana M2 = vstupní úroveň (long: High, short: Low).
		public double Edge { get { return IsLong ? High : Low; } }
	}

	public enum M2EventKind
	{
		Candidate,
		Confirmed,
		Cancelled,
		Expired,
		Tested,
		Broken,
		Superseded
	}

	// Událost pro ladicí výpis (DebugLog). M2Bar = svíčka M2 / kandidáta, EventBar = svíčka, která událost způsobila.
	public struct M2Event
	{
		public M2EventKind	Kind;
		public bool			IsLong;
		public int			M2Bar;
		public int			EventBar;
		public double		High;
		public double		Low;
	}

	public class M2Engine
	{
		private class Candidate
		{
			public int		Bar;
			public double	High;
			public double	Low;
			// HH_LL_OB_GAP: outside vůči předchozí svíčce (high >= prev high, low <= prev low, aspoň jedno ostře).
			public bool		IsOutside;
		}

		// High / low předchozí uzavřené svíčky (pro outside).
		private bool	hasPrev;
		private double	prevHigh;
		private double	prevLow;

		private readonly List<M2Zone>		zones		= new List<M2Zone>();
		private readonly List<Candidate>	longPending		= new List<Candidate>();
		private readonly List<Candidate>	shortPending	= new List<Candidate>();
		private readonly List<M2Event>		events			= new List<M2Event>();

		public readonly object	Sync			= new object();
		public int				MaxHistoryBars	= 2000;
		public int				LastBar			= -1;

		public M2ZonesCandidateMode	Mode			= M2ZonesCandidateMode.AllPending;
		// AllPending: kandidát starší než N svíček se zahodí (jen kvůli výkonu). 0 = bez limitu.
		public int				MaxPendingBars	= 0;
		// 1.1.1: svíčka, která potvrdí víc kandidátů, vytvoří jen M2 z nejnovějšího; starší čekající ve stejném
		// směru se zahodí. false = chování 1.1.0 (každý potvrzený kandidát = vlastní M2).
		public bool				NewestConfirmedOnly	= true;
		// true = sbírat události pro ladicí výpis (TakeEvents). false = žádná režie.
		public bool				CollectEvents;

		public int PendingCount { get { lock (Sync) return longPending.Count + shortPending.Count; } }

		// Pro čtení zamknout Sync (OnRender běží v jiném vlákně než OnBarUpdate).
		public List<M2Zone> Zones { get { return zones; } }

		// Negativní svíčka: close pod 50 % range. Přesně 50 % = ani negativní, ani pozitivní (neutrální, nikdy M2).
		// Ceny jsou double: u ticků jako 0.01 / 0.1 / 0.00005 (CL, GC, 6E) vyjde u neutrální svíčky
		// close*2 a high+low rozdílně o ~1e-14. Proto tolerance 1e-9 × cena – o řády pod jedním tickem.
		private const double RelEps = 1e-9;

		// -1 = negativní, +1 = pozitivní, 0 = neutrální (close přesně na 50 % range).
		public static int Side(double high, double low, double close)
		{
			double d	= close * 2.0 - (high + low);
			double tol	= RelEps * Math.Max(Math.Abs(high), Math.Abs(low));
			return d < -tol ? -1 : d > tol ? 1 : 0;
		}

		public static bool IsNegative(double high, double low, double close) { return Side(high, low, close) < 0; }
		public static bool IsPositive(double high, double low, double close) { return Side(high, low, close) > 0; }

		// Volat jednou za každou UZAVŘENOU svíčku, ve vzestupném pořadí indexů.
		public void AddBar(int bar, double high, double low, double close)
		{
			lock (Sync)
			{
				if (bar <= LastBar)
					return;
				LastBar = bar;
				int firstNew = zones.Count;

				// (1) Čekající kandidáti – potvrzení svíčkou celou za hranou (long: Low > High[m2], short: High < Low[m2]),
				//     nemusí být bezprostředně následující. Jedna svíčka může potvrdit víc kandidátů.
				//     AllPending: zrušení, když svíčka zavře za opačnou hranou dřív, než přišlo potvrzení.
				if (Mode == M2ZonesCandidateMode.HH_LL_OB_GAP)
				{
					ProcessPendingBest(longPending, true, bar, high, low, close);
					ProcessPendingBest(shortPending, false, bar, high, low, close);
				}
				else
				{
					ProcessPending(longPending, true, bar, high, low, close);
					ProcessPending(shortPending, false, bar, high, low, close);
				}

				// (2) Stav potvrzených zón – test hrany a prolomení (close za opačnou hranou).
				//     Zóny potvrzené touto svíčkou se přeskočí (potvrzovací svíčka není test).
				for (int i = 0; i < firstNew; i++)
				{
					M2Zone z = zones[i];
					if (z.State == M2ZoneState.Broken)
						continue;

					bool touched	= z.IsLong ? low <= z.High : high >= z.Low;
					bool broken		= z.IsLong ? close < z.Low : close > z.High;

					if (touched && z.TestBar < 0)
					{
						z.TestBar = bar;
						AddEvent(M2EventKind.Tested, z.IsLong, z.M2Bar, bar, z.High, z.Low);
					}

					if (broken)
					{
						z.State		= M2ZoneState.Broken;
						z.BrokenBar	= bar;
						AddEvent(M2EventKind.Broken, z.IsLong, z.M2Bar, bar, z.High, z.Low);
					}
					else if (touched && z.State == M2ZoneState.Untested)
						z.State = M2ZoneState.Tested;
				}

				// (3) Svíčka sama jako nový kandidát – až po kroku (1), takže nikdy nepotvrdí sama sebe.
				//     AllPending: přidá se k čekajícím. OnlyLast: nahradí předchozího kandidáta ve svém směru.
				int side = Side(high, low, close);
				if (side != 0)
				{
					bool isLong = side < 0;
					List<Candidate> list = isLong ? longPending : shortPending;
					if (Mode == M2ZonesCandidateMode.OnlyLast)
						list.Clear();
					bool outside = hasPrev && high >= prevHigh && low <= prevLow && (high > prevHigh || low < prevLow);
					list.Add(new Candidate { Bar = bar, High = high, Low = low, IsOutside = outside });
					AddEvent(M2EventKind.Candidate, isLong, bar, bar, high, low);
				}

				hasPrev		= true;
				prevHigh	= high;
				prevLow		= low;

				// (4) Prolomené zóny starší než MaxHistoryBars ven.
				if (MaxHistoryBars > 0)
					zones.RemoveAll(z => z.State == M2ZoneState.Broken && bar - z.BrokenBar > MaxHistoryBars);
			}
		}

		// Průchod vpřed s kompakcí na místě: pořadí zůstává (zóny vznikají ve vzestupném pořadí M2Bar), seznam neroste.
		private void ProcessPending(List<Candidate> list, bool isLong, int bar, double high, double low, double close)
		{
			bool allPending = Mode == M2ZonesCandidateMode.AllPending;

			// 1.1.1 NewestConfirmedOnly: když svíčka potvrdí víc kandidátů, platí jen nejnovější z nich.
			// Všichni starší čekající ve stejném směru (potvrzení i nepotvrzení) se zahodí – žádné dva boxy v sobě.
			// Novější kandidáti za ním čekají dál. Seznam je seřazený vzestupně podle Bar.
			int newest = -1;
			if (NewestConfirmedOnly)
				for (int i = list.Count - 1; i >= 0; i--)
					if (isLong ? low > list[i].High : high < list[i].Low)
					{
						newest = i;
						break;
					}

			int keep = 0;
			for (int i = 0; i < list.Count; i++)
			{
				Candidate c = list[i];

				if (i < newest)
				{
					AddEvent(M2EventKind.Superseded, isLong, c.Bar, bar, c.High, c.Low);
					continue;
				}

				// Za indexem newest už nic potvrzené není (newest je poslední potvrzený), stačí stejná podmínka.
				if (isLong ? low > c.High : high < c.Low)
				{
					zones.Add(NewZone(isLong, c, bar));
					AddEvent(M2EventKind.Confirmed, isLong, c.Bar, bar, c.High, c.Low);
					continue;
				}

				if (allPending)
				{
					if (isLong ? close < c.Low : close > c.High)
					{
						AddEvent(M2EventKind.Cancelled, isLong, c.Bar, bar, c.High, c.Low);
						continue;
					}
					if (MaxPendingBars > 0 && bar - c.Bar >= MaxPendingBars)
					{
						AddEvent(M2EventKind.Expired, isLong, c.Bar, bar, c.High, c.Low);
						continue;
					}
				}

				list[keep++] = c;
			}
			if (keep < list.Count)
				list.RemoveRange(keep, list.Count - keep);
		}

		// 1.2 HH_LL_OB_GAP (docs/UPRESNENI_M2.md). Pattern = M2 … potvrzovací svíčka, uvnitř žádná další M2.
		// Vítěz z kandidátů potvrzených touto svíčkou, podle priority:
		//   1) 123Gap – kandidát o 2 svíčky zpět (svíčka 3 celá za svíčkou 1), bez ohledu na cokoli dalšího;
		//   2) čeká-li outside kandidát, smí se potvrdit jen outside (ostatní potvrzení se ignorují, čekají dál);
		//   3) nejlepší hrana: short nejvyšší Low, long nejnižší High (shoda → novější).
		// Po vítězi se všichni ostatní čekající ve směru zahodí a M2 se hledá znovu. Jinak zrušení / expirace jako AllPending.
		private void ProcessPendingBest(List<Candidate> list, bool isLong, int bar, double high, double low, double close)
		{
			if (list.Count == 0)
				return;

			bool anyOutside = false;
			for (int i = 0; i < list.Count; i++)
				if (list[i].IsOutside) { anyOutside = true; break; }

			int winner = -1;
			for (int i = 0; i < list.Count; i++)
			{
				Candidate c = list[i];
				if (!(isLong ? low > c.High : high < c.Low))
					continue;

				if (c.Bar == bar - 2)
				{
					winner = i;
					break;
				}

				if (anyOutside && !c.IsOutside)
					continue;

				if (winner < 0 || (isLong ? c.High <= list[winner].High : c.Low >= list[winner].Low))
					winner = i;
			}

			if (winner >= 0)
			{
				Candidate w = list[winner];
				zones.Add(NewZone(isLong, w, bar));
				AddEvent(M2EventKind.Confirmed, isLong, w.Bar, bar, w.High, w.Low);
				for (int i = 0; i < list.Count; i++)
					if (i != winner)
						AddEvent(M2EventKind.Superseded, isLong, list[i].Bar, bar, list[i].High, list[i].Low);
				list.Clear();
				return;
			}

			int keep = 0;
			for (int i = 0; i < list.Count; i++)
			{
				Candidate c = list[i];
				if (isLong ? close < c.Low : close > c.High)
				{
					AddEvent(M2EventKind.Cancelled, isLong, c.Bar, bar, c.High, c.Low);
					continue;
				}
				if (MaxPendingBars > 0 && bar - c.Bar >= MaxPendingBars)
				{
					AddEvent(M2EventKind.Expired, isLong, c.Bar, bar, c.High, c.Low);
					continue;
				}
				list[keep++] = c;
			}
			if (keep < list.Count)
				list.RemoveRange(keep, list.Count - keep);
		}

		private void AddEvent(M2EventKind kind, bool isLong, int m2Bar, int eventBar, double high, double low)
		{
			if (CollectEvents)
				events.Add(new M2Event { Kind = kind, IsLong = isLong, M2Bar = m2Bar, EventBar = eventBar, High = high, Low = low });
		}

		// Přesune nasbírané události do target (volat mimo kreslení; vypisovat až po návratu, mimo zámek).
		public void TakeEvents(List<M2Event> target)
		{
			lock (Sync)
			{
				target.AddRange(events);
				events.Clear();
			}
		}

		private static M2Zone NewZone(bool isLong, Candidate c, int confBar)
		{
			return new M2Zone { IsLong = isLong, M2Bar = c.Bar, ConfBar = confBar, High = c.High, Low = c.Low };
		}
	}
}
// ==== M2 ENGINE END ====

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Braunn.M2Zones[] cacheM2Zones;
		public Braunn.M2Zones M2Zones(bool showLong, bool showShort, bool showBroken, int maxHistoryBars)
		{
			return M2Zones(Input, showLong, showShort, showBroken, maxHistoryBars);
		}

		public Braunn.M2Zones M2Zones(ISeries<double> input, bool showLong, bool showShort, bool showBroken, int maxHistoryBars)
		{
			if (cacheM2Zones != null)
				for (int idx = 0; idx < cacheM2Zones.Length; idx++)
					if (cacheM2Zones[idx] != null && cacheM2Zones[idx].ShowLong == showLong && cacheM2Zones[idx].ShowShort == showShort && cacheM2Zones[idx].ShowBroken == showBroken && cacheM2Zones[idx].MaxHistoryBars == maxHistoryBars && cacheM2Zones[idx].EqualsInput(input))
						return cacheM2Zones[idx];
			return CacheIndicator<Braunn.M2Zones>(new Braunn.M2Zones(){ ShowLong = showLong, ShowShort = showShort, ShowBroken = showBroken, MaxHistoryBars = maxHistoryBars }, input, ref cacheM2Zones);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Braunn.M2Zones M2Zones(bool showLong, bool showShort, bool showBroken, int maxHistoryBars)
		{
			return indicator.M2Zones(Input, showLong, showShort, showBroken, maxHistoryBars);
		}

		public Indicators.Braunn.M2Zones M2Zones(ISeries<double> input , bool showLong, bool showShort, bool showBroken, int maxHistoryBars)
		{
			return indicator.M2Zones(input, showLong, showShort, showBroken, maxHistoryBars);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Braunn.M2Zones M2Zones(bool showLong, bool showShort, bool showBroken, int maxHistoryBars)
		{
			return indicator.M2Zones(Input, showLong, showShort, showBroken, maxHistoryBars);
		}

		public Indicators.Braunn.M2Zones M2Zones(ISeries<double> input , bool showLong, bool showShort, bool showBroken, int maxHistoryBars)
		{
			return indicator.M2Zones(input, showLong, showShort, showBroken, maxHistoryBars);
		}
	}
}

#endregion
