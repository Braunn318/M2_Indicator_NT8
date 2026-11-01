// M2Zones – M2 zóny pro NinjaTrader 8 (zadání ZADANI_M2_INDIKATOR.md v1.0, 31. 10. 2026)
//
// - Žádné Draw.* objekty: vše se kreslí v OnRender přes SharpDX.
// - Žádný popisek vlevo nahoře (v grafu DisplayName = "", lze zapnout ShowChartLabel) a žádný řádek v Data Boxu.
// - Netestovaná / testovaná / prolomená M2; prolomená zůstává v grafu šedě.
// - Vzhled určuje jen stav (netestovaná / testovaná / prolomená), nikdy stáří zóny.
// - Calculate.OnBarClose, bez repaintu.
//
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

// ==== M2 ENGINE BEGIN ====
// Čistá logika bez závislosti na NinjaTraderu (testuje se samostatně).
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

	public class M2Engine
	{
		private class Candidate
		{
			public int		Bar;
			public double	High;
			public double	Low;
		}

		private readonly List<M2Zone>	zones = new List<M2Zone>();
		private Candidate				longCandidate;
		private Candidate				shortCandidate;

		public readonly object	Sync			= new object();
		public int				MaxHistoryBars	= 2000;
		public int				LastBar			= -1;

		// Pro čtení zamknout Sync (OnRender běží v jiném vlákně než OnBarUpdate).
		public List<M2Zone> Zones { get { return zones; } }

		// Negativní svíčka: close pod 50 % range. Přesně 50 % = ani negativní, ani pozitivní.
		public static bool IsNegative(double high, double low, double close) { return close * 2.0 < high + low; }
		public static bool IsPositive(double high, double low, double close) { return close * 2.0 > high + low; }

		// Volat jednou za každou UZAVŘENOU svíčku, ve vzestupném pořadí indexů.
		public void AddBar(int bar, double high, double low, double close)
		{
			lock (Sync)
			{
				if (bar <= LastBar)
					return;
				LastBar = bar;

				// (a) Stav potvrzených zón – test hrany a prolomení (close za opačnou hranou).
				for (int i = 0; i < zones.Count; i++)
				{
					M2Zone z = zones[i];
					if (z.State == M2ZoneState.Broken)
						continue;

					bool touched	= z.IsLong ? low <= z.High : high >= z.Low;
					bool broken		= z.IsLong ? close < z.Low : close > z.High;

					if (touched && z.TestBar < 0)
						z.TestBar = bar;

					if (broken)
					{
						z.State		= M2ZoneState.Broken;
						z.BrokenBar	= bar;
					}
					else if (touched && z.State == M2ZoneState.Untested)
						z.State = M2ZoneState.Tested;
				}

				// (b) Potvrzení kandidáta – svíčka celá nad High (long) / celá pod Low (short).
				//     Nemusí jít o bezprostředně následující svíčku.
				if (longCandidate != null && low > longCandidate.High)
				{
					zones.Add(NewZone(true, longCandidate, bar));
					longCandidate = null;
				}
				if (shortCandidate != null && high < shortCandidate.Low)
				{
					zones.Add(NewZone(false, shortCandidate, bar));
					shortCandidate = null;
				}

				// (c) Nový kandidát – novější negativní (long) / pozitivní (short) svíčka nahradí
				//     starého kandidáta, který zaniká bez vykreslení.
				if (IsNegative(high, low, close))
					longCandidate = new Candidate { Bar = bar, High = high, Low = low };
				else if (IsPositive(high, low, close))
					shortCandidate = new Candidate { Bar = bar, High = high, Low = low };

				// (d) Prolomené zóny starší než MaxHistoryBars ven.
				if (MaxHistoryBars > 0)
					zones.RemoveAll(z => z.State == M2ZoneState.Broken && bar - z.BrokenBar > MaxHistoryBars);
			}
		}

		private static M2Zone NewZone(bool isLong, Candidate c, int confBar)
		{
			return new M2Zone { IsLong = isLong, M2Bar = c.Bar, ConfBar = confBar, High = c.High, Low = c.Low };
		}
	}
}
// ==== M2 ENGINE END ====

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

				LongUntestedColor			= Brushes.LimeGreen;
				LongTestedColor				= Brushes.Olive;
				ShortUntestedColor			= Brushes.Red;
				ShortTestedColor			= Brushes.DarkOrange;
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
				engine.MaxHistoryBars = MaxHistoryBars;
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

			if (EnableCandleColoring)
			{
				if (M2Engine.IsPositive(h, l, c))
					BarBrushes[0] = ColorAbove50;
				else if (M2Engine.IsNegative(h, l, c))
					BarBrushes[0] = ColorBelow50;
			}
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

			// 1) Pod zámkem jen kopie dat – žádné volání NT API, aby datové vlákno (OnBarUpdate) nečekalo na kreslení.
			views.Clear();
			lock (engine.Sync)
			{
				int		lastBar		= engine.LastBar;
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
