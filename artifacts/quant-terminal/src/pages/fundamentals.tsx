import { useTicker } from "@/lib/ticker-context";
import { useGetStockFundamentals, getGetStockFundamentalsQueryKey } from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, formatLargeNumber, formatPercent } from "@/lib/format";

export default function Fundamentals() {
  const { activeTicker } = useTicker();

  const { data: fund, isLoading } = useGetStockFundamentals(activeTicker, {
    query: { enabled: !!activeTicker, queryKey: getGetStockFundamentalsQueryKey(activeTicker) }
  });

  if (!activeTicker) return <div className="p-8 text-center text-muted-foreground">Select a ticker to begin analysis.</div>;

  const MetricRow = ({ label, value }: { label: string, value: React.ReactNode }) => (
    <div className="flex justify-between items-center py-2 border-b border-border/50 hover:bg-muted/20 transition-colors px-2">
      <span className="text-xs text-muted-foreground uppercase">{label}</span>
      <span className="font-mono text-sm">{value}</span>
    </div>
  );

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

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-foreground uppercase tracking-widest text-primary">Valuation</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <MetricRow label="Market Cap" value={formatLargeNumber(fund.marketCap)} />
                <MetricRow label="P/E Ratio (TTM)" value={fund.pe?.toFixed(2) || "-"} />
                <MetricRow label="Forward P/E" value={fund.forwardPe?.toFixed(2) || "-"} />
                <MetricRow label="PEG Ratio" value={fund.peg?.toFixed(2) || "-"} />
                <MetricRow label="Price / Book" value={fund.priceToBook?.toFixed(2) || "-"} />
                <MetricRow label="EV / EBITDA" value={fund.evToEbitda?.toFixed(2) || "-"} />
              </CardContent>
            </Card>

            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-foreground uppercase tracking-widest text-primary">Profitability & Growth</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <MetricRow label="Profit Margin" value={fund.profitMargins ? formatPercent(fund.profitMargins * 100) : "-"} />
                <MetricRow label="Operating Margin" value={fund.operatingMargins ? formatPercent(fund.operatingMargins * 100) : "-"} />
                <MetricRow label="Return on Assets" value={fund.returnOnAssets ? formatPercent(fund.returnOnAssets * 100) : "-"} />
                <MetricRow label="Return on Equity" value={fund.returnOnEquity ? formatPercent(fund.returnOnEquity * 100) : "-"} />
                <MetricRow label="Revenue Growth (YOY)" value={fund.revenueGrowth ? formatPercent(fund.revenueGrowth * 100) : "-"} />
                <MetricRow label="Earnings Growth (YOY)" value={fund.earningsGrowth ? formatPercent(fund.earningsGrowth * 100) : "-"} />
              </CardContent>
            </Card>

            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-foreground uppercase tracking-widest text-primary">Financial Health</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <MetricRow label="Total Debt / Equity" value={fund.debtToEquity?.toFixed(2) || "-"} />
                <MetricRow label="Current Ratio" value={fund.currentRatio?.toFixed(2) || "-"} />
                <MetricRow label="Quick Ratio" value={fund.quickRatio?.toFixed(2) || "-"} />
                <MetricRow label="Beta (5Y)" value={fund.beta?.toFixed(2) || "-"} />
                <MetricRow label="Dividend Yield" value={fund.dividendYield ? formatPercent(fund.dividendYield * 100) : "-"} />
                <MetricRow label="Diluted EPS" value={fund.eps ? formatCurrency(fund.eps) : "-"} />
              </CardContent>
            </Card>

            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-foreground uppercase tracking-widest text-primary">Trading Info</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <MetricRow label="52 Week High" value={formatCurrency(fund.fiftyTwoWeekHigh)} />
                <MetricRow label="52 Week Low" value={formatCurrency(fund.fiftyTwoWeekLow)} />
                <MetricRow label="Short Ratio" value={fund.shortRatio?.toFixed(2) || "-"} />
                <MetricRow label="Short % of Float" value={fund.shortPercentOfFloat ? formatPercent(fund.shortPercentOfFloat * 100) : "-"} />
                <MetricRow label="Institutional Ownership" value={fund.institutionalOwnership ? formatPercent(fund.institutionalOwnership * 100) : "-"} />
              </CardContent>
            </Card>
          </div>
        </>
      ) : (
        <div className="text-center py-12 text-muted-foreground">No fundamental data available.</div>
      )}
    </div>
  );
}
