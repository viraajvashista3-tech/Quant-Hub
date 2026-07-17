import { useTicker } from "@/lib/ticker-context";
import { useLabels, useProMode } from "@/lib/pro-mode-context";
import { useGetStockFundamentals, getGetStockFundamentalsQueryKey } from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, formatLargeNumber, formatPercent } from "@/lib/format";
import { Zap } from "lucide-react";

export default function Fundamentals() {
  const { activeTicker } = useTicker();
  const label = useLabels();
  const { isAtLeast } = useProMode();
  const isPro = isAtLeast("pro");

  const { data: fund, isLoading } = useGetStockFundamentals(activeTicker, {
    query: { enabled: !!activeTicker, queryKey: getGetStockFundamentalsQueryKey(activeTicker) }
  });

  if (!activeTicker) return <div className="p-8 text-center text-muted-foreground">Select a ticker to begin analysis.</div>;

  const MetricRow = ({ lbl, value, highlight }: { lbl: string; value: React.ReactNode; highlight?: boolean }) => (
    <div className={`flex justify-between items-center py-2 border-b border-border/50 hover:bg-muted/20 transition-colors px-2 ${highlight ? "bg-primary/5" : ""}`}>
      <span className={`text-xs uppercase ${highlight ? "text-primary" : "text-muted-foreground"}`}>{lbl}</span>
      <span className="font-mono text-sm">{value}</span>
    </div>
  );

  // Graham Number status vs current price
  const grahamVsPrice = fund?.grahamNumber && fund?.fiftyTwoWeekLow
    ? ((fund.grahamNumber - (fund.fiftyTwoWeekHigh || 0)) / (fund.grahamNumber || 1)) * 100
    : null;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight text-primary uppercase">Fundamentals: {activeTicker}</h1>
        {isLoading ? <Skeleton className="h-5 w-64 mt-2" /> : (
          <p className="text-muted-foreground mt-1">{fund?.name} • {fund?.sector} • {fund?.industry}</p>
        )}
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <Skeleton className="h-96 w-full" />
          <Skeleton className="h-96 w-full" />
          <Skeleton className="h-96 w-full" />
        </div>
      ) : fund ? (
        <>
          {fund.description && (
            <Card className="bg-card rounded-none border-border">
              <CardContent className="p-6">
                <p className="text-sm leading-relaxed text-muted-foreground">{fund.description}</p>
              </CardContent>
            </Card>
          )}

          {/* Pro Mode: Key Stats bar */}
          {isPro && (
            <Card className="bg-card rounded-none border-primary/30">
              <CardHeader className="pb-2 pt-3 px-4 bg-primary/5">
                <CardTitle className="text-xs font-medium text-primary uppercase tracking-widest flex items-center gap-2">
                  <Zap className="h-3.5 w-3.5" /> Pro — Balance Sheet Snapshot
                </CardTitle>
              </CardHeader>
              <CardContent className="p-4 grid grid-cols-2 sm:grid-cols-4 gap-4">
                <div>
                  <div className="text-xs text-muted-foreground uppercase mb-1">Total Revenue</div>
                  <div className="font-mono font-bold">{fund.totalRevenue ? formatLargeNumber(fund.totalRevenue) : "—"}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground uppercase mb-1">Free Cash Flow</div>
                  <div className={`font-mono font-bold ${(fund.freeCashflow || 0) > 0 ? "text-green-500" : "text-destructive"}`}>
                    {fund.freeCashflow ? formatLargeNumber(fund.freeCashflow) : "—"}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground uppercase mb-1">Total Debt</div>
                  <div className="font-mono font-bold text-destructive">{fund.totalDebt ? formatLargeNumber(fund.totalDebt) : "—"}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground uppercase mb-1">Cash & Equivalents</div>
                  <div className="font-mono font-bold text-green-500">{fund.totalCash ? formatLargeNumber(fund.totalCash) : "—"}</div>
                </div>
              </CardContent>
            </Card>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-foreground uppercase tracking-widest text-primary">Valuation</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <MetricRow lbl={label("marketCap")} value={formatLargeNumber(fund.marketCap)} />
                <MetricRow lbl={label("pe")} value={fund.pe?.toFixed(2) || "-"} />
                <MetricRow lbl={label("forwardPe")} value={fund.forwardPe?.toFixed(2) || "-"} />
                <MetricRow lbl={label("peg")} value={fund.peg?.toFixed(2) || "-"} />
                <MetricRow lbl={label("pb")} value={fund.priceToBook?.toFixed(2) || "-"} />
                <MetricRow lbl={label("evEbitda")} value={fund.evToEbitda?.toFixed(2) || "-"} />
                {isPro && fund.grahamNumber && (
                  <MetricRow
                    lbl="Graham Number"
                    value={<span className="text-primary">{formatCurrency(fund.grahamNumber)}</span>}
                    highlight
                  />
                )}
                {isPro && fund.eps && fund.bookValuePerShare && (
                  <MetricRow lbl="Book Value / Share" value={formatCurrency(fund.bookValuePerShare)} />
                )}
              </CardContent>
            </Card>

            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-foreground uppercase tracking-widest text-primary">Profitability & Growth</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <MetricRow lbl={label("profitMargin")} value={fund.profitMargins ? formatPercent(fund.profitMargins * 100) : "-"} />
                <MetricRow lbl={label("opMargin")} value={fund.operatingMargins ? formatPercent(fund.operatingMargins * 100) : "-"} />
                <MetricRow lbl={label("roa")} value={fund.returnOnAssets ? formatPercent(fund.returnOnAssets * 100) : "-"} />
                <MetricRow lbl={label("roe")} value={fund.returnOnEquity ? formatPercent(fund.returnOnEquity * 100) : "-"} />
                <MetricRow lbl={label("revGrowth")} value={fund.revenueGrowth ? formatPercent(fund.revenueGrowth * 100) : "-"} />
                <MetricRow lbl={label("epsGrowth")} value={fund.earningsGrowth ? formatPercent(fund.earningsGrowth * 100) : "-"} />
                {isPro && fund.eps && (
                  <MetricRow lbl="Diluted EPS" value={fund.eps ? formatCurrency(fund.eps) : "-"} />
                )}
                {isPro && fund.sharesOutstanding && (
                  <MetricRow lbl="Shares Outstanding" value={formatLargeNumber(fund.sharesOutstanding)} />
                )}
              </CardContent>
            </Card>

            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-foreground uppercase tracking-widest text-primary">Financial Health</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <MetricRow lbl={label("debtEquity")} value={fund.debtToEquity?.toFixed(2) || "-"} />
                <MetricRow lbl={label("currentRatio")} value={fund.currentRatio?.toFixed(2) || "-"} />
                <MetricRow lbl={label("quickRatio")} value={fund.quickRatio?.toFixed(2) || "-"} />
                <MetricRow lbl={label("beta")} value={fund.beta?.toFixed(2) || "-"} />
                <MetricRow lbl={label("dividendYield")} value={fund.dividendYield ? formatPercent(fund.dividendYield * 100) : "-"} />
                <MetricRow lbl={label("eps")} value={fund.eps ? formatCurrency(fund.eps) : "-"} />
                {isPro && fund.freeCashflow && (
                  <MetricRow lbl="Free Cash Flow" value={<span className={(fund.freeCashflow || 0) > 0 ? "text-green-500" : "text-destructive"}>{formatLargeNumber(fund.freeCashflow)}</span>} />
                )}
              </CardContent>
            </Card>

            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-foreground uppercase tracking-widest text-primary">Trading Info</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <MetricRow lbl="52-Week High" value={formatCurrency(fund.fiftyTwoWeekHigh)} />
                <MetricRow lbl="52-Week Low" value={formatCurrency(fund.fiftyTwoWeekLow)} />
                <MetricRow lbl={label("shortRatio")} value={fund.shortRatio?.toFixed(2) || "-"} />
                <MetricRow lbl={label("shortFloat")} value={fund.shortPercentOfFloat ? formatPercent(fund.shortPercentOfFloat * 100) : "-"} />
                <MetricRow lbl={label("institutionalOwn")} value={fund.institutionalOwnership ? formatPercent(fund.institutionalOwnership * 100) : "-"} />
              </CardContent>
            </Card>

            {/* Pro-only: Valuation Analysis */}
            {isPro && fund.grahamNumber && (
              <Card className="bg-card rounded-none border-primary/30 md:col-span-2">
                <CardHeader className="pb-2 bg-primary/5">
                  <CardTitle className="text-sm font-medium text-primary uppercase tracking-widest flex items-center gap-2">
                    <Zap className="h-4 w-4" /> Pro — Graham Valuation Analysis
                  </CardTitle>
                </CardHeader>
                <CardContent className="p-4 space-y-3">
                  <div className="grid grid-cols-2 sm:grid-cols-3 gap-4 text-sm">
                    <div>
                      <div className="text-xs text-muted-foreground uppercase mb-1">Graham Number</div>
                      <div className="font-mono font-bold text-primary text-lg">{formatCurrency(fund.grahamNumber)}</div>
                      <div className="text-xs text-muted-foreground">√(22.5 × EPS × BVPS)</div>
                    </div>
                    <div>
                      <div className="text-xs text-muted-foreground uppercase mb-1">52W High</div>
                      <div className="font-mono font-bold">{formatCurrency(fund.fiftyTwoWeekHigh)}</div>
                      <div className={`text-xs mt-0.5 ${(fund.grahamNumber || 0) > (fund.fiftyTwoWeekHigh || 0) ? "text-green-500" : "text-destructive"}`}>
                        {(fund.grahamNumber || 0) > (fund.fiftyTwoWeekHigh || 0) ? "↑ Below Graham" : "↓ Above Graham"}
                      </div>
                    </div>
                    <div>
                      <div className="text-xs text-muted-foreground uppercase mb-1">EPS (TTM)</div>
                      <div className="font-mono font-bold">{fund.eps ? formatCurrency(fund.eps) : "—"}</div>
                    </div>
                  </div>
                  <p className="text-xs text-muted-foreground leading-relaxed border-t border-border pt-3">
                    The <strong className="text-foreground">Graham Number</strong> is a conservative intrinsic value estimate for defensive investors, based on earnings and book value. A stock trading well above its Graham Number may be overvalued by Benjamin Graham's standards, though growth stocks routinely exceed this threshold.
                  </p>
                </CardContent>
              </Card>
            )}
          </div>
        </>
      ) : (
        <div className="text-center py-12 text-muted-foreground">No fundamental data available.</div>
      )}
    </div>
  );
}
