import { useGetMarketPulse, getGetMarketPulseQueryKey, MarketPulseItem } from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useProMode } from "@/lib/pro-mode-context";
import { TrendingUp, TrendingDown, RefreshCw, Activity, AlertCircle } from "lucide-react";
import {
  BarChart, Bar, Cell, XAxis, YAxis, Tooltip,
  ResponsiveContainer, CartesianGrid, ReferenceLine,
} from "recharts";

const MOOD_CONFIG: Record<string, { emoji: string; color: string; bg: string; desc: string }> = {
  "Extreme Fear": { emoji: "😱", color: "text-red-500",    bg: "bg-red-500/10 border-red-500/40",    desc: "Investors are very fearful. Historically, extreme fear can signal a buying opportunity for contrarians." },
  "Fear":         { emoji: "😨", color: "text-orange-500", bg: "bg-orange-500/10 border-orange-500/40", desc: "Markets are nervous. Investors are pulling back and being more cautious than usual." },
  "Neutral":      { emoji: "😐", color: "text-yellow-500", bg: "bg-yellow-500/10 border-yellow-500/40", desc: "Markets are balanced. Neither excessive greed nor fear is driving prices right now." },
  "Greed":        { emoji: "🤑", color: "text-green-400",  bg: "bg-green-500/10 border-green-500/40",  desc: "Investors are feeling bullish and optimistic. Be careful not to get caught up in euphoria." },
  "Extreme Greed":{ emoji: "🚀", color: "text-emerald-400",bg: "bg-emerald-500/10 border-emerald-500/40", desc: "Markets are euphoric. Extreme greed often precedes a pullback — proceed with caution." },
};

const MOOD_ORDER = ["Extreme Fear", "Fear", "Neutral", "Greed", "Extreme Greed"];

function pctColor(v: number) {
  return v > 0 ? "text-green-500" : v < 0 ? "text-destructive" : "text-muted-foreground";
}
function pctFmt(v: number, decimals = 2) {
  return `${v >= 0 ? "+" : ""}${v.toFixed(decimals)}%`;
}
function priceFmt(n: number) {
  return n >= 1000 ? n.toFixed(2) : n >= 10 ? n.toFixed(2) : n.toFixed(4);
}

function IndexCard({ item }: { item: MarketPulseItem }) {
  const up = item.changePct >= 0;
  return (
    <div className="bg-card border border-border p-4 flex flex-col gap-1">
      <div className="flex items-center justify-between">
        <span className="text-xs text-muted-foreground uppercase tracking-widest font-semibold">{item.symbol}</span>
        <span className={`text-xs font-mono flex items-center gap-1 ${up ? "text-green-500" : "text-destructive"}`}>
          {up ? <TrendingUp className="h-3 w-3" /> : <TrendingDown className="h-3 w-3" />}
          {pctFmt(item.changePct)}
        </span>
      </div>
      <div className="text-xl font-bold font-mono text-foreground">${priceFmt(item.price)}</div>
      <div className="text-xs text-muted-foreground font-medium">{item.label}</div>
      <div className="flex gap-3 mt-1 text-[10px] text-muted-foreground">
        <span>1W <span className={pctColor(item.change1wPct)}>{pctFmt(item.change1wPct)}</span></span>
        <span>1M <span className={pctColor(item.change1mPct)}>{pctFmt(item.change1mPct)}</span></span>
      </div>
    </div>
  );
}

function BeginnerMoodCard({ mood, vix, rotationNote }: { mood: string; vix: number; rotationNote: string }) {
  const cfg = MOOD_CONFIG[mood] ?? MOOD_CONFIG["Neutral"];
  return (
    <Card className={`rounded-none border ${cfg.bg}`}>
      <CardContent className="p-6">
        <div className="text-5xl mb-3">{cfg.emoji}</div>
        <h2 className="text-2xl font-bold text-foreground mb-1">Market mood: <span className={cfg.color}>{mood}</span></h2>
        <p className="text-sm text-muted-foreground leading-relaxed">{cfg.desc}</p>
        {rotationNote && (
          <p className="mt-3 text-sm text-foreground/80 border-l-2 border-primary/40 pl-3">{rotationNote}</p>
        )}
        <p className="mt-2 text-xs text-muted-foreground">VIX (fear index): <span className="font-mono text-foreground">{vix.toFixed(1)}</span> — lower is calmer, higher means more fear.</p>
      </CardContent>
    </Card>
  );
}

function BeginnerSectors({ sectors }: { sectors: MarketPulseItem[] }) {
  return (
    <Card className="rounded-none border-border">
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold">Which sectors are moving today?</CardTitle>
        <p className="text-xs text-muted-foreground">Ordered from best to worst performance today.</p>
      </CardHeader>
      <CardContent className="space-y-2 pt-0">
        {sectors.map((s) => (
          <div key={s.symbol} className="flex items-center gap-3">
            <span className="text-base leading-none">{s.changePct >= 0 ? "✅" : "❌"}</span>
            <span className="flex-1 text-sm font-medium text-foreground">{s.label}</span>
            <span className={`text-sm font-mono font-bold ${pctColor(s.changePct)}`}>{pctFmt(s.changePct)}</span>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

function BeginnerIndices({ indices }: { indices: MarketPulseItem[] }) {
  const NICE: Record<string, string> = { SPY: "US Stock Market", QQQ: "Tech Stocks", DIA: "Big Companies", IWM: "Smaller Companies" };
  return (
    <Card className="rounded-none border-border">
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold">How are the main markets doing?</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 pt-0">
        {indices.map((idx) => (
          <div key={idx.symbol} className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium">{NICE[idx.symbol] ?? idx.label}</p>
              <p className="text-xs text-muted-foreground">{idx.symbol}</p>
            </div>
            <span className={`text-lg font-bold font-mono ${pctColor(idx.changePct)}`}>{idx.changePct >= 0 ? "▲" : "▼"} {Math.abs(idx.changePct).toFixed(1)}%</span>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

const CustomSectorTooltip = ({ active, payload }: { active?: boolean; payload?: Array<{ payload: MarketPulseItem }> }) => {
  if (!active || !payload?.length) return null;
  const d = payload[0].payload;
  return (
    <div className="bg-popover border border-border p-2 text-xs font-mono space-y-0.5">
      <p className="font-bold text-foreground">{d.label} ({d.symbol})</p>
      <p>Day: <span className={pctColor(d.changePct)}>{pctFmt(d.changePct)}</span></p>
      <p>1W: <span className={pctColor(d.change1wPct)}>{pctFmt(d.change1wPct)}</span></p>
      <p>1M: <span className={pctColor(d.change1mPct)}>{pctFmt(d.change1mPct)}</span></p>
    </div>
  );
};

function MoodGauge({ mood, vix }: { mood: string; vix: number }) {
  const idx = MOOD_ORDER.indexOf(mood);
  const cfg = MOOD_CONFIG[mood] ?? MOOD_CONFIG["Neutral"];
  const COLORS = ["#ef4444", "#f97316", "#eab308", "#22c55e", "#10b981"];
  return (
    <div className="space-y-2">
      <div className="flex justify-between items-center">
        <span className="text-xs text-muted-foreground uppercase tracking-widest">Market Mood</span>
        <span className="text-xs font-mono text-muted-foreground">VIX {vix.toFixed(1)}</span>
      </div>
      <div className="flex gap-1 h-3">
        {MOOD_ORDER.map((m, i) => (
          <div
            key={m}
            className="flex-1 relative"
            style={{ backgroundColor: COLORS[i], opacity: idx === i ? 1 : 0.25 }}
            title={m}
          >
            {idx === i && (
              <div className="absolute -bottom-4 left-1/2 -translate-x-1/2 w-0 h-0 border-l-4 border-r-4 border-t-4 border-transparent" style={{ borderTopColor: COLORS[i] }} />
            )}
          </div>
        ))}
      </div>
      <div className="flex justify-between text-[9px] text-muted-foreground pt-3">
        <span>Extreme Fear</span>
        <span className={`font-bold text-[11px] ${cfg.color}`}>{cfg.emoji} {mood}</span>
        <span>Extreme Greed</span>
      </div>
    </div>
  );
}

export default function Market() {
  const { mode, isAtLeast } = useProMode();
  const { data: pulse, isLoading, error, refetch, isFetching } = useGetMarketPulse({
    query: { queryKey: getGetMarketPulseQueryKey(), staleTime: 3 * 60 * 1000 }
  });

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-primary uppercase tracking-widest">Market Pulse</h1>
        </div>
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {Array(4).fill(0).map((_, i) => <Skeleton key={i} className="h-28 w-full" />)}
        </div>
        <Skeleton className="h-64 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (error || !pulse) {
    return (
      <div className="p-8 flex flex-col items-center gap-3 text-center">
        <AlertCircle className="h-8 w-8 text-destructive" />
        <p className="text-muted-foreground">Could not load market data. Check connection and try again.</p>
        <button onClick={() => refetch()} className="text-xs text-primary underline">Retry</button>
      </div>
    );
  }

  /* ── BEGINNER VIEW ─────────────────────────────────────────────────────── */
  if (mode === "beginner") {
    return (
      <div className="max-w-2xl mx-auto space-y-5">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-bold text-foreground">Today's Market Overview</h1>
          <button onClick={() => refetch()} disabled={isFetching} className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1.5 transition-colors">
            <RefreshCw className={`h-3.5 w-3.5 ${isFetching ? "animate-spin" : ""}`} />
            Refresh
          </button>
        </div>
        <BeginnerMoodCard mood={pulse.marketMood} vix={pulse.vix} rotationNote={pulse.rotationNote} />
        <BeginnerIndices indices={pulse.indices} />
        <BeginnerSectors sectors={pulse.sectors} />
        <Card className="rounded-none border-border">
          <CardHeader className="pb-2">
            <CardTitle className="text-base font-semibold">Other things to watch</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 pt-0">
            {pulse.macro.map((m) => (
              <div key={m.symbol} className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">{m.label}</span>
                <span className={`text-sm font-mono font-bold ${pctColor(m.changePct)}`}>
                  {m.symbol === "^TNX" ? `${m.price.toFixed(2)}%` : `$${priceFmt(m.price)}`}
                  <span className="text-xs ml-1.5">{pctFmt(m.changePct)}</span>
                </span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    );
  }

  /* ── STANDARD VIEW (Amateur / Pro / Master) ────────────────────────────── */
  const sectorMax = Math.max(...pulse.sectors.map((s) => Math.abs(s.changePct)), 0.5);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-primary uppercase tracking-widest flex items-center gap-3">
            <Activity className="h-6 w-6" /> Market Pulse
          </h1>
          <p className="text-xs text-muted-foreground mt-1">Live market overview — indices, sectors, macro & fear gauge</p>
        </div>
        <button
          onClick={() => refetch()}
          disabled={isFetching}
          className="flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground border border-border hover:border-primary/50 px-3 py-1.5 transition-colors self-start sm:self-auto"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${isFetching ? "animate-spin" : ""}`} />
          {isFetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>

      {/* Mood gauge */}
      <Card className="rounded-none border-border">
        <CardContent className="p-5">
          <MoodGauge mood={pulse.marketMood} vix={pulse.vix} />
          {pulse.rotationNote && (
            <p className="mt-4 text-xs text-muted-foreground border-l-2 border-primary/40 pl-3">{pulse.rotationNote}</p>
          )}
        </CardContent>
      </Card>

      {/* Major Indices */}
      <div>
        <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground mb-3">Major Indices</p>
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          {pulse.indices.map((idx) => <IndexCard key={idx.symbol} item={idx} />)}
        </div>
      </div>

      {/* Sector Heatmap + Macro side by side */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Sector bar chart */}
        <Card className="lg:col-span-2 rounded-none border-border">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">
              Sector Performance — Today
            </CardTitle>
            {isAtLeast("pro") && (
              <p className="text-[10px] text-muted-foreground">Hover a bar for 1W and 1M performance</p>
            )}
          </CardHeader>
          <CardContent>
            <div className="h-72">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={pulse.sectors} layout="vertical" margin={{ top: 0, right: 40, left: 90, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" horizontal={false} />
                  <XAxis
                    type="number"
                    domain={[-sectorMax * 1.2, sectorMax * 1.2]}
                    tickFormatter={(v) => `${v > 0 ? "+" : ""}${v.toFixed(1)}%`}
                    stroke="hsl(var(--muted-foreground))"
                    fontSize={10}
                    tickLine={false}
                    axisLine={false}
                  />
                  <YAxis
                    dataKey="label"
                    type="category"
                    width={88}
                    stroke="hsl(var(--muted-foreground))"
                    fontSize={10}
                    tickLine={false}
                    axisLine={false}
                  />
                  <ReferenceLine x={0} stroke="hsl(var(--border))" strokeWidth={1.5} />
                  <Tooltip content={<CustomSectorTooltip />} />
                  <Bar dataKey="changePct" radius={0} maxBarSize={18}>
                    {pulse.sectors.map((s) => (
                      <Cell key={s.symbol} fill={s.changePct >= 0 ? "#22c55e" : "#ef4444"} fillOpacity={0.8} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>

        {/* Macro panel */}
        <Card className="rounded-none border-border">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">Macro</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {pulse.macro.map((m, i) => {
              const isYield = m.symbol === "^TNX";
              const displayPrice = isYield ? `${m.price.toFixed(2)}%` : `$${priceFmt(m.price)}`;
              return (
                <div key={m.symbol} className={`flex items-center justify-between px-4 py-3 ${i < pulse.macro.length - 1 ? "border-b border-border/50" : ""}`}>
                  <div>
                    <p className="text-xs font-semibold text-foreground">{m.label}</p>
                    <p className="text-[10px] text-muted-foreground font-mono">{m.symbol}</p>
                  </div>
                  <div className="text-right">
                    <p className="text-sm font-mono font-bold text-foreground">{displayPrice}</p>
                    <p className={`text-xs font-mono ${pctColor(m.changePct)}`}>{pctFmt(m.changePct)}</p>
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>
      </div>

      {/* Pro: Sector detail table */}
      {isAtLeast("pro") && (
        <Card className="rounded-none border-primary/30">
          <CardHeader className="pb-2 bg-primary/5">
            <CardTitle className="text-sm font-medium text-primary uppercase tracking-widest">Sector Detail — 1D / 1W / 1M</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b border-border text-muted-foreground">
                    <th className="text-left px-4 py-2 font-semibold uppercase tracking-widest">Sector</th>
                    <th className="text-right px-4 py-2 font-semibold uppercase tracking-widest">ETF</th>
                    <th className="text-right px-4 py-2 font-semibold uppercase tracking-widest">Price</th>
                    <th className="text-right px-4 py-2 font-semibold uppercase tracking-widest">Day</th>
                    <th className="text-right px-4 py-2 font-semibold uppercase tracking-widest">1 Week</th>
                    <th className="text-right px-4 py-2 font-semibold uppercase tracking-widest">1 Month</th>
                  </tr>
                </thead>
                <tbody>
                  {pulse.sectors.map((s, i) => (
                    <tr key={s.symbol} className={`border-b border-border/50 hover:bg-muted/20 transition-colors ${i === 0 ? "bg-green-500/5" : i === pulse.sectors.length - 1 ? "bg-destructive/5" : ""}`}>
                      <td className="px-4 py-2.5 font-medium text-foreground">{s.label}</td>
                      <td className="px-4 py-2.5 font-mono text-muted-foreground text-right">{s.symbol}</td>
                      <td className="px-4 py-2.5 font-mono text-right">${priceFmt(s.price)}</td>
                      <td className={`px-4 py-2.5 font-mono text-right font-bold ${pctColor(s.changePct)}`}>{pctFmt(s.changePct)}</td>
                      <td className={`px-4 py-2.5 font-mono text-right ${pctColor(s.change1wPct)}`}>{pctFmt(s.change1wPct)}</td>
                      <td className={`px-4 py-2.5 font-mono text-right ${pctColor(s.change1mPct)}`}>{pctFmt(s.change1mPct)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
