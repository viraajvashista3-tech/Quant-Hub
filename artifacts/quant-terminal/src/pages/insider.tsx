import { useTicker } from "@/lib/ticker-context";
import { useProMode } from "@/lib/pro-mode-context";
import { useGetStockInsider, getGetStockInsiderQueryKey } from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { formatCurrency, formatLargeNumber } from "@/lib/format";
import { TrendingUp, TrendingDown, Building2, User, AlertTriangle } from "lucide-react";

function txColor(type: string) {
  if (type === "Purchase") return "text-green-500";
  if (type === "Sale") return "text-destructive";
  if (type === "Option Exercise") return "text-amber-500";
  if (type === "Award/Grant") return "text-blue-400";
  return "text-muted-foreground";
}

function txBadgeClass(type: string) {
  if (type === "Purchase") return "border-green-500/40 text-green-500 bg-green-500/10";
  if (type === "Sale") return "border-destructive/40 text-destructive bg-destructive/10";
  if (type === "Option Exercise") return "border-amber-500/40 text-amber-500 bg-amber-500/10";
  if (type === "Award/Grant") return "border-blue-400/40 text-blue-400 bg-blue-400/10";
  return "border-border text-muted-foreground bg-muted/30";
}

export default function Insider() {
  const { activeTicker } = useTicker();
  const { isAtLeast } = useProMode();
  const isPro = isAtLeast("pro");

  const { data: insider, isLoading } = useGetStockInsider(activeTicker, {
    query: { enabled: !!activeTicker, queryKey: getGetStockInsiderQueryKey(activeTicker) }
  });

  if (!activeTicker) return <div className="p-8 text-center text-muted-foreground">Select a ticker to begin analysis.</div>;

  const buys = insider?.transactions.filter(t => t.transactionType === "Purchase") || [];
  const sells = insider?.transactions.filter(t => t.transactionType === "Sale") || [];
  const buyValue = buys.reduce((s, t) => s + (t.value || 0), 0);
  const sellValue = sells.reduce((s, t) => s + (t.value || 0), 0);
  const netFlow = buyValue - sellValue;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight text-primary uppercase">Insider Activity: {activeTicker}</h1>
        {!isLoading && insider && (
          <p className="text-muted-foreground mt-1">{insider.name} · Form 4 SEC filings · Last 6 months</p>
        )}
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {Array(4).fill(0).map((_, i) => <Skeleton key={i} className="h-28 w-full" />)}
          <Skeleton className="h-96 w-full md:col-span-2 lg:col-span-4" />
        </div>
      ) : insider ? (
        <div className="space-y-6">
          {/* Summary cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {/* Net Sentiment */}
            <Card className={`bg-card rounded-none border-2 ${insider.netSentiment === "Net Buyers" ? "border-green-500/50" : insider.netSentiment === "Net Sellers" ? "border-destructive/50" : "border-border"}`}>
              <CardHeader className="pb-2 pt-4 px-4">
                <CardTitle className="text-xs font-medium text-muted-foreground uppercase tracking-widest">Net Insider Activity</CardTitle>
              </CardHeader>
              <CardContent className="px-4 pb-4">
                <div className={`text-2xl font-bold flex items-center gap-2 ${insider.netSentiment === "Net Buyers" ? "text-green-500" : insider.netSentiment === "Net Sellers" ? "text-destructive" : "text-foreground"}`}>
                  {insider.netSentiment === "Net Buyers" ? <TrendingUp className="h-5 w-5" /> : insider.netSentiment === "Net Sellers" ? <TrendingDown className="h-5 w-5" /> : null}
                  {insider.netSentiment}
                </div>
                <p className="text-xs text-muted-foreground mt-1">{insider.buyCount} purchases · {insider.sellCount} sales (recent)</p>
              </CardContent>
            </Card>

            {/* Net dollar flow */}
            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 pt-4 px-4">
                <CardTitle className="text-xs font-medium text-muted-foreground uppercase tracking-widest">Net Dollar Flow</CardTitle>
              </CardHeader>
              <CardContent className="px-4 pb-4">
                <div className={`text-2xl font-bold font-mono ${netFlow > 0 ? "text-green-500" : netFlow < 0 ? "text-destructive" : "text-foreground"}`}>
                  {netFlow >= 0 ? "+" : ""}{formatLargeNumber(netFlow)}
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  Buys {formatLargeNumber(buyValue)} · Sells {formatLargeNumber(sellValue)}
                </p>
              </CardContent>
            </Card>

            {/* Insider ownership */}
            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 pt-4 px-4">
                <CardTitle className="text-xs font-medium text-muted-foreground uppercase tracking-widest flex items-center gap-2"><User className="h-3.5 w-3.5" />Insider Ownership</CardTitle>
              </CardHeader>
              <CardContent className="px-4 pb-4">
                <div className="text-2xl font-bold font-mono">
                  {insider.insiderOwnership != null ? (insider.insiderOwnership * 100).toFixed(2) + "%" : "—"}
                </div>
                <p className="text-xs text-muted-foreground mt-1">% of shares held by insiders</p>
              </CardContent>
            </Card>

            {/* Institutional ownership */}
            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 pt-4 px-4">
                <CardTitle className="text-xs font-medium text-muted-foreground uppercase tracking-widest flex items-center gap-2"><Building2 className="h-3.5 w-3.5" />Institutional Hold.</CardTitle>
              </CardHeader>
              <CardContent className="px-4 pb-4">
                <div className="text-2xl font-bold font-mono">
                  {insider.institutionalOwnership != null ? (insider.institutionalOwnership * 100).toFixed(1) + "%" : "—"}
                </div>
                <p className="text-xs text-muted-foreground mt-1">% held by institutions</p>
              </CardContent>
            </Card>
          </div>

          {/* 6-month purchase summary */}
          {insider.purchases6m && (insider.purchases6m.purchaseTrans || insider.purchases6m.saleTrans) ? (
            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-3 bg-muted/30">
                <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">6-Month Insider Purchase Summary</CardTitle>
              </CardHeader>
              <CardContent className="p-4">
                <div className="grid grid-cols-2 gap-6">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <TrendingUp className="h-4 w-4 text-green-500" />
                      <span className="text-xs text-muted-foreground uppercase tracking-widest">Purchases</span>
                    </div>
                    <div className="text-xl font-bold font-mono text-green-500">{insider.purchases6m.purchaseShares ? formatLargeNumber(insider.purchases6m.purchaseShares) : "—"} shares</div>
                    <div className="text-xs text-muted-foreground">{insider.purchases6m.purchaseTrans ?? 0} transactions</div>
                  </div>
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <TrendingDown className="h-4 w-4 text-destructive" />
                      <span className="text-xs text-muted-foreground uppercase tracking-widest">Sales</span>
                    </div>
                    <div className="text-xl font-bold font-mono text-destructive">{insider.purchases6m.saleShares ? formatLargeNumber(insider.purchases6m.saleShares) : "—"} shares</div>
                    <div className="text-xs text-muted-foreground">{insider.purchases6m.saleTrans ?? 0} transactions</div>
                  </div>
                </div>
              </CardContent>
            </Card>
          ) : null}

          {/* Pro Mode warning note */}
          {!isPro && (
            <div className="flex gap-2 items-start px-3 py-2.5 border border-amber-500/30 bg-amber-500/5 text-amber-400/80 text-xs">
              <AlertTriangle className="h-3.5 w-3.5 mt-0.5 shrink-0" />
              Enable <strong className="mx-0.5">Pro Mode</strong> (via Customise) for full transaction detail including share prices and extended filing descriptions.
            </div>
          )}

          {/* Transaction table */}
          <Card className="bg-card rounded-none border-border">
            <CardHeader className="bg-muted/30">
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">
                Recent Transactions {insider.transactions.length > 0 && <span className="ml-2 text-primary/60">({insider.transactions.length})</span>}
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {insider.transactions.length === 0 ? (
                <div className="p-8 text-center text-muted-foreground">No recent insider transactions found.</div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-border">
                        <th className="text-left py-3 px-4 text-xs font-semibold uppercase tracking-widest text-muted-foreground w-28">Date</th>
                        <th className="text-left py-3 px-4 text-xs font-semibold uppercase tracking-widest text-muted-foreground">Insider</th>
                        <th className="text-left py-3 px-4 text-xs font-semibold uppercase tracking-widest text-muted-foreground w-24">Role</th>
                        <th className="text-left py-3 px-4 text-xs font-semibold uppercase tracking-widest text-muted-foreground w-36">Type</th>
                        <th className="text-right py-3 px-4 text-xs font-semibold uppercase tracking-widest text-muted-foreground">Shares</th>
                        <th className="text-right py-3 px-4 text-xs font-semibold uppercase tracking-widest text-muted-foreground">Value</th>
                        {isPro && <th className="text-left py-3 px-4 text-xs font-semibold uppercase tracking-widest text-muted-foreground">Description</th>}
                      </tr>
                    </thead>
                    <tbody>
                      {insider.transactions.map((tx, i) => (
                        <tr key={i} className="border-b border-border/50 hover:bg-muted/20 transition-colors">
                          <td className="py-3 px-4 font-mono text-xs text-muted-foreground">{tx.date || "—"}</td>
                          <td className="py-3 px-4 font-semibold text-sm">{tx.insider || "—"}</td>
                          <td className="py-3 px-4 text-xs text-muted-foreground">{tx.position || "—"}</td>
                          <td className="py-3 px-4">
                            <span className={`inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 border rounded-none ${txBadgeClass(tx.transactionType)}`}>
                              {tx.transactionType === "Purchase" && <TrendingUp className="h-3 w-3" />}
                              {tx.transactionType === "Sale" && <TrendingDown className="h-3 w-3" />}
                              {tx.transactionType}
                            </span>
                          </td>
                          <td className={`py-3 px-4 font-mono text-sm text-right ${txColor(tx.transactionType)}`}>
                            {tx.shares ? formatLargeNumber(tx.shares) : "—"}
                          </td>
                          <td className={`py-3 px-4 font-mono text-sm text-right ${txColor(tx.transactionType)}`}>
                            {tx.value && tx.value > 0 ? formatCurrency(tx.value) : "—"}
                          </td>
                          {isPro && (
                            <td className="py-3 px-4 text-xs text-muted-foreground max-w-xs truncate">{tx.text || "—"}</td>
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </CardContent>
          </Card>

          <p className="text-[10px] text-muted-foreground/50">Data sourced from SEC Form 4 filings via Yahoo Finance. Transactions may have a 2-business-day reporting delay.</p>
        </div>
      ) : (
        <div className="text-center py-12 text-muted-foreground">No insider data available for {activeTicker}.</div>
      )}
    </div>
  );
}
