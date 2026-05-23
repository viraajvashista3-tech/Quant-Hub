import { useState } from "react";
import { useTicker } from "@/lib/ticker-context";
import { useLabels } from "@/lib/pro-mode-context";
import {
  useGetStockOverview,
  useGetStockHistory,
  useGetStockNews,
  getGetStockOverviewQueryKey,
  getGetStockHistoryQueryKey,
  getGetStockNewsQueryKey
} from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, formatPercent, formatLargeNumber } from "@/lib/format";
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, AreaChart, Area, ReferenceLine } from "recharts";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Progress } from "@/components/ui/progress";

export default function Terminal() {
  const { activeTicker } = useTicker();
  const [period, setPeriod] = useState<'ytd'|'6mo'|'1y'|'2y'|'5y'>('1y');
  const label = useLabels();

  const { data: overview, isLoading: isLoadingOverview } = useGetStockOverview(activeTicker, {
    query: { enabled: !!activeTicker, queryKey: getGetStockOverviewQueryKey(activeTicker) }
  });

  const { data: history, isLoading: isLoadingHistory } = useGetStockHistory(activeTicker, { period }, {
    query: { enabled: !!activeTicker, queryKey: getGetStockHistoryQueryKey(activeTicker, { period }) }
  });

  const { data: news, isLoading: isLoadingNews } = useGetStockNews(activeTicker, {
    query: { enabled: !!activeTicker, queryKey: getGetStockNewsQueryKey(activeTicker) }
  });

  if (!activeTicker) return <div className="p-8 text-center text-muted-foreground">Select a ticker to begin analysis.</div>;

  return (
    <div className="space-y-6">
      {/* Header / Overview */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <h1 className="text-4xl font-bold tracking-tight text-primary flex items-center gap-4">
            {activeTicker}
            {isLoadingOverview ? <Skeleton className="h-8 w-24" /> : (
              <span className="text-foreground text-2xl font-normal">{overview?.name}</span>
            )}
          </h1>
          {isLoadingOverview ? <Skeleton className="h-6 w-48 mt-2" /> : overview && (
            <div className="flex items-baseline gap-3 mt-1">
              <span className="text-3xl font-mono">{formatCurrency(overview.price)}</span>
              <span className={`text-lg font-mono ${overview.change >= 0 ? "text-green-500" : "text-destructive"}`}>
                {overview.change >= 0 ? "+" : ""}{formatPercent(overview.changePercent)}
              </span>
            </div>
          )}
        </div>

        {isLoadingOverview ? <Skeleton className="h-16 w-32" /> : overview && (
          <div className="flex items-center gap-4">
            <div className="text-right">
              <div className="text-sm text-muted-foreground uppercase tracking-widest">{label("quantScore")}</div>
              <div className="text-3xl font-bold font-mono text-primary">{overview.quantScore.toFixed(1)}</div>
            </div>
            <Badge variant={
              overview.signal === "BUY" ? "default" :
              overview.signal === "AVOID" ? "destructive" :
              "secondary"
            } className={`text-lg px-4 py-2 uppercase tracking-widest rounded-none ${overview.signal === 'BUY' ? 'bg-green-600 text-black hover:bg-green-500' : ''}`}>
              {overview.signal}
            </Badge>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Main Chart */}
        <Card className="lg:col-span-2 bg-card rounded-none border-border">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">Price History & MA</CardTitle>
            <Tabs value={period} onValueChange={(v) => setPeriod(v as typeof period)} className="h-8">
              <TabsList className="h-8 rounded-none">
                <TabsTrigger value="ytd" className="rounded-none text-xs">YTD</TabsTrigger>
                <TabsTrigger value="6mo" className="rounded-none text-xs">6M</TabsTrigger>
                <TabsTrigger value="1y" className="rounded-none text-xs">1Y</TabsTrigger>
                <TabsTrigger value="2y" className="rounded-none text-xs">2Y</TabsTrigger>
                <TabsTrigger value="5y" className="rounded-none text-xs">5Y</TabsTrigger>
              </TabsList>
            </Tabs>
          </CardHeader>
          <CardContent>
            <div className="h-[350px] w-full">
              {isLoadingHistory ? <Skeleton className="w-full h-full" /> : history && (
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={history.bars} margin={{ top: 5, right: 0, left: -20, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" vertical={false} />
                    <XAxis dataKey="date" stroke="hsl(var(--muted-foreground))" fontSize={12} tickLine={false} axisLine={false} tickFormatter={(v) => new Date(v).toLocaleDateString(undefined, { month: 'short', year: '2-digit' })} />
                    <YAxis stroke="hsl(var(--muted-foreground))" fontSize={12} tickLine={false} axisLine={false} domain={['auto', 'auto']} tickFormatter={(v) => `$${v}`} />
                    <Tooltip
                      contentStyle={{ backgroundColor: 'hsl(var(--popover))', border: '1px solid hsl(var(--border))', borderRadius: 0 }}
                      itemStyle={{ fontFamily: 'var(--font-mono)' }}
                    />
                    <Line type="monotone" dataKey="close" stroke="hsl(var(--primary))" strokeWidth={2} dot={false} isAnimationActive={false} name="Price" />
                    <Line type="monotone" dataKey="ma50" stroke="#f59e0b" strokeWidth={1} dot={false} isAnimationActive={false} name="MA50" />
                    <Line type="monotone" dataKey="ma200" stroke="#8b5cf6" strokeWidth={1} dot={false} isAnimationActive={false} name="MA200" />
                  </LineChart>
                </ResponsiveContainer>
              )}
            </div>
            {/* RSI sub-chart */}
            <div className="h-[120px] w-full mt-4 border-t border-border pt-4">
              {isLoadingHistory ? <Skeleton className="w-full h-full" /> : history && (
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={history.bars} margin={{ top: 5, right: 0, left: -20, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" vertical={false} />
                    <XAxis dataKey="date" hide />
                    <YAxis stroke="hsl(var(--muted-foreground))" fontSize={10} tickLine={false} axisLine={false} domain={[0, 100]} ticks={[30, 70]} />
                    <Tooltip contentStyle={{ backgroundColor: 'hsl(var(--popover))', border: '1px solid hsl(var(--border))', borderRadius: 0 }} />
                    <ReferenceLine y={70} stroke="hsl(var(--destructive))" strokeDasharray="3 3" />
                    <ReferenceLine y={30} stroke="#10b981" strokeDasharray="3 3" />
                    <Area type="monotone" dataKey="rsi" stroke="hsl(var(--primary))" fill="hsl(var(--primary))" fillOpacity={0.1} isAnimationActive={false} name={label("rsi")} />
                  </AreaChart>
                </ResponsiveContainer>
              )}
            </div>
            <div className="flex gap-4 mt-3 text-xs text-muted-foreground">
              <span className="flex items-center gap-1.5"><span className="inline-block w-3 h-0.5 bg-primary" /> Price</span>
              <span className="flex items-center gap-1.5"><span className="inline-block w-3 h-0.5 bg-amber-500" /> MA50</span>
              <span className="flex items-center gap-1.5"><span className="inline-block w-3 h-0.5 bg-violet-500" /> MA200</span>
            </div>
          </CardContent>
        </Card>

        {/* Right Column */}
        <div className="space-y-6">
          {/* Key Metrics */}
          <Card className="bg-card rounded-none border-border">
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">Key Metrics</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {isLoadingOverview ? (
                Array(5).fill(0).map((_, i) => <Skeleton key={i} className="h-10 w-full" />)
              ) : overview && (
                <>
                  <div>
                    <div className="flex justify-between text-xs mb-1">
                      <span className="text-muted-foreground uppercase">{label("rsi")}</span>
                      <span className={`font-mono ${overview.rsi > 70 ? 'text-destructive' : overview.rsi < 30 ? 'text-green-500' : 'text-foreground'}`}>
                        {overview.rsi?.toFixed(2)}
                      </span>
                    </div>
                    <Progress
                      value={overview.rsi}
                      className="h-1.5 rounded-none bg-muted"
                      style={{ '--progress-color': overview.rsi > 70 ? 'hsl(var(--destructive))' : overview.rsi < 30 ? '#10b981' : 'hsl(var(--primary))' } as React.CSSProperties}
                    />
                  </div>
                  <div className="flex justify-between items-center py-2 border-b border-border">
                    <span className="text-xs text-muted-foreground uppercase">{label("macd")}</span>
                    <span className={`font-mono text-sm ${overview.macd > overview.macdSignal ? 'text-green-500' : 'text-destructive'}`}>
                      {overview.macd?.toFixed(2)} / {overview.macdSignal?.toFixed(2)}
                    </span>
                  </div>
                  <div className="flex justify-between items-center py-2 border-b border-border">
                    <span className="text-xs text-muted-foreground uppercase">{label("volume")}</span>
                    <span className="font-mono text-sm">
                      {formatLargeNumber(overview.volume)} <span className="text-muted-foreground">vs {formatLargeNumber(overview.avgVolume)}</span>
                    </span>
                  </div>
                  <div className="flex justify-between items-center py-2 border-b border-border">
                    <span className="text-xs text-muted-foreground uppercase">{label("beta")}</span>
                    <span className="font-mono text-sm">{overview.beta?.toFixed(2) || "-"}</span>
                  </div>
                  <div className="flex justify-between items-center py-2">
                    <span className="text-xs text-muted-foreground uppercase">{label("annVol")}</span>
                    <span className="font-mono text-sm">{overview.annualizedVolatility ? overview.annualizedVolatility.toFixed(1) + "%" : "-"}</span>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          {/* News Sentiment */}
          <Card className="bg-card rounded-none border-border overflow-hidden">
            <CardHeader className="pb-2 bg-muted/30">
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest flex justify-between">
                <span>Recent News</span>
                {news && <span className={news.sentimentScore > 0 ? "text-green-500" : news.sentimentScore < 0 ? "text-destructive" : ""}>{news.sentimentLabel}</span>}
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {isLoadingNews ? (
                <div className="p-4 space-y-4">
                  {Array(3).fill(0).map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}
                </div>
              ) : news?.headlines?.length ? (
                <div className="divide-y divide-border">
                  {news.headlines.slice(0, 5).map((item, idx) => (
                    <a key={idx} href={item.url} target="_blank" rel="noreferrer" className="block p-3 hover:bg-muted/50 transition-colors">
                      <div className="flex gap-2 items-start">
                        <div className={`w-1.5 h-1.5 mt-1.5 rounded-full shrink-0 ${(item.sentiment || 0) > 0 ? 'bg-green-500' : (item.sentiment || 0) < 0 ? 'bg-destructive' : 'bg-muted-foreground'}`} />
                        <div>
                          <p className="text-sm line-clamp-2">{item.title}</p>
                          <p className="text-xs text-muted-foreground mt-1">{item.publishedAt ? new Date(item.publishedAt).toLocaleDateString() : ''}</p>
                        </div>
                      </div>
                    </a>
                  ))}
                </div>
              ) : (
                <div className="p-4 text-center text-muted-foreground text-sm">No recent news found.</div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
