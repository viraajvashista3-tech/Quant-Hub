import React, { useState } from "react";
import { useTicker } from "@/lib/ticker-context";
import { useLabels } from "@/lib/pro-mode-context";
import { useGetStockPeers, getGetStockPeersQueryKey } from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, formatLargeNumber, formatPercent } from "@/lib/format";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { FileText } from "lucide-react";

function cellColor(val: number): { bg: string; text: string } {
  if (val === 1) return { bg: "bg-primary/60", text: "text-primary-foreground font-bold" };
  if (val >= 0.8) return { bg: "bg-primary/40", text: "text-foreground" };
  if (val >= 0.5) return { bg: "bg-primary/20", text: "text-foreground" };
  if (val >= 0.2) return { bg: "bg-primary/10", text: "text-foreground" };
  if (val <= -0.5) return { bg: "bg-destructive/40", text: "text-foreground" };
  if (val <= -0.2) return { bg: "bg-destructive/20", text: "text-foreground" };
  return { bg: "bg-transparent", text: "text-foreground" };
}

export default function Peers() {
  const { activeTicker, setActiveTicker } = useTicker();
  const [period, setPeriod] = useState<"1y" | "5y">("1y");
  const label = useLabels();

  const { data: peersData, isLoading } = useGetStockPeers(activeTicker, { period }, {
    query: { enabled: !!activeTicker, queryKey: getGetStockPeersQueryKey(activeTicker, { period }) },
  });

  if (!activeTicker) return <div className="p-8 text-center text-muted-foreground">Select a ticker to begin analysis.</div>;

  const matrix = peersData?.correlationMatrix ?? {};
  const matrixTickers = Object.keys(matrix);

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-end">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-primary uppercase">Peer Analysis: {activeTicker}</h1>
          <p className="text-muted-foreground mt-1">Sector: {peersData?.sector || "Loading..."}</p>
        </div>
        <Tabs value={period} onValueChange={(v) => setPeriod(v as "1y" | "5y")} className="h-8">
          <TabsList className="h-8 rounded-none">
            <TabsTrigger value="1y" className="rounded-none text-xs">1Y</TabsTrigger>
            <TabsTrigger value="5y" className="rounded-none text-xs">5Y</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {isLoading ? (
        <div className="space-y-6">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-[400px] w-full" />
          <Skeleton className="h-[300px] w-full" />
        </div>
      ) : peersData ? (
        <div className="space-y-6">

          {/* Competitive Summary */}
          {peersData.summary && (
            <Card className="bg-card rounded-none border-l-2 border-l-primary border-border">
              <CardContent className="p-4 flex gap-3 items-start">
                <FileText className="h-4 w-4 text-primary mt-0.5 shrink-0" />
                <p className="text-sm leading-relaxed text-foreground/90">{peersData.summary}</p>
              </CardContent>
            </Card>
          )}

          {/* Fundamentals Comparison */}
          <Card className="bg-card rounded-none border-border">
            <CardHeader className="pb-2 bg-muted/30">
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">
                {label("fundamentalsComparison")}
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow className="border-border hover:bg-transparent">
                      <TableHead className="w-[100px]">Ticker</TableHead>
                      <TableHead>Price</TableHead>
                      <TableHead>{label("marketCap")}</TableHead>
                      <TableHead>{label("pe")}</TableHead>
                      <TableHead>{label("forwardPe")}</TableHead>
                      <TableHead>{label("dividendYield")}</TableHead>
                      <TableHead>{label("beta")}</TableHead>
                      <TableHead>{label("profitMargin")}</TableHead>
                      <TableHead>{label("roe")}</TableHead>
                      <TableHead>{label("debtEquity")}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {peersData.peers.map((peer) => (
                      <TableRow
                        key={peer.ticker}
                        className={`border-border cursor-pointer transition-colors ${peer.ticker === activeTicker ? "bg-primary/10 hover:bg-primary/20" : "hover:bg-muted/50"}`}
                        onClick={() => setActiveTicker(peer.ticker)}
                      >
                        <TableCell className="font-mono font-bold text-primary">{peer.ticker}</TableCell>
                        <TableCell className="font-mono text-sm">{formatCurrency(peer.price)}</TableCell>
                        <TableCell className="font-mono text-sm text-muted-foreground">{formatLargeNumber(peer.marketCap)}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.pe ? peer.pe.toFixed(1) : "-"}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.forwardPe ? peer.forwardPe.toFixed(1) : "-"}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.dividendYield ? formatPercent(peer.dividendYield * 100) : "-"}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.beta ? peer.beta.toFixed(2) : "-"}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.profitMargins ? formatPercent(peer.profitMargins * 100) : "-"}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.returnOnEquity ? formatPercent(peer.returnOnEquity * 100) : "-"}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.debtToEquity ? peer.debtToEquity.toFixed(2) : "-"}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>

          {/* Correlation Matrix */}
          {matrixTickers.length > 0 && (
            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">
                  {label("correlationMatrix")}
                </CardTitle>
              </CardHeader>
              <CardContent className="p-6">
                <div className="overflow-x-auto">
                  <div
                    className="grid"
                    style={{ gridTemplateColumns: `auto ${"minmax(60px, 1fr) ".repeat(matrixTickers.length)}` }}
                  >
                    {/* Header row */}
                    <div className="p-2" />
                    {matrixTickers.map((t) => (
                      <div key={t} className="p-2 font-mono text-xs font-bold text-center border-b border-border">{t}</div>
                    ))}

                    {/* Data rows */}
                    {Object.entries(matrix).map(([rowTicker, cols]) => (
                      <React.Fragment key={rowTicker}>
                        <div className="p-2 font-mono text-xs font-bold text-right border-r border-border pr-4 flex items-center justify-end">
                          {rowTicker}
                        </div>
                        {matrixTickers.map((colTicker) => {
                          const val = (cols as Record<string, number>)[colTicker];
                          const { bg, text } = cellColor(val ?? 0);
                          return (
                            <div
                              key={colTicker}
                              className={`p-2 font-mono text-xs text-center border-b border-r border-border/30 flex items-center justify-center ${bg} ${text}`}
                            >
                              {val !== undefined ? val.toFixed(2) : "-"}
                            </div>
                          );
                        })}
                      </React.Fragment>
                    ))}
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

        </div>
      ) : null}
    </div>
  );
}
