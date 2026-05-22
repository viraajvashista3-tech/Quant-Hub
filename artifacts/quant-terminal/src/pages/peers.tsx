import { useState } from "react";
import { useTicker } from "@/lib/ticker-context";
import { useGetStockPeers, getGetStockPeersQueryKey } from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, formatLargeNumber, formatPercent } from "@/lib/format";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

export default function Peers() {
  const { activeTicker, setActiveTicker } = useTicker();
  const [period, setPeriod] = useState<'1y'|'5y'>('1y');

  const { data: peersData, isLoading } = useGetStockPeers(activeTicker, { period }, {
    query: { enabled: !!activeTicker, queryKey: getGetStockPeersQueryKey(activeTicker, { period }) }
  });

  if (!activeTicker) return <div className="p-8 text-center text-muted-foreground">Select a ticker to begin analysis.</div>;

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-end">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-primary uppercase">Peer Analysis: {activeTicker}</h1>
          <p className="text-muted-foreground mt-1">Sector: {peersData?.sector || "Loading..."}</p>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-6">
          <Skeleton className="h-[400px] w-full" />
          <Skeleton className="h-[400px] w-full" />
        </div>
      ) : peersData ? (
        <div className="space-y-6">
          
          <Card className="bg-card rounded-none border-border">
            <CardHeader className="pb-2 bg-muted/30">
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">
                Fundamentals Comparison
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow className="border-border hover:bg-transparent">
                      <TableHead className="w-[100px]">Ticker</TableHead>
                      <TableHead>Price</TableHead>
                      <TableHead>Market Cap</TableHead>
                      <TableHead>P/E</TableHead>
                      <TableHead>Fwd P/E</TableHead>
                      <TableHead>Div Yield</TableHead>
                      <TableHead>Beta</TableHead>
                      <TableHead>Margins</TableHead>
                      <TableHead>ROE</TableHead>
                      <TableHead>D/E</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {peersData.peers.map((peer) => (
                      <TableRow 
                        key={peer.ticker} 
                        className={`border-border cursor-pointer transition-colors ${peer.ticker === activeTicker ? 'bg-primary/10 hover:bg-primary/20' : 'hover:bg-muted/50'}`}
                        onClick={() => setActiveTicker(peer.ticker)}
                      >
                        <TableCell className="font-mono font-bold text-primary">{peer.ticker}</TableCell>
                        <TableCell className="font-mono text-sm">{formatCurrency(peer.price)}</TableCell>
                        <TableCell className="font-mono text-sm text-muted-foreground">{formatLargeNumber(peer.marketCap)}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.pe ? peer.pe.toFixed(1) : '-'}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.forwardPe ? peer.forwardPe.toFixed(1) : '-'}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.dividendYield ? formatPercent(peer.dividendYield * 100) : '-'}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.beta ? peer.beta.toFixed(2) : '-'}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.profitMargins ? formatPercent(peer.profitMargins * 100) : '-'}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.returnOnEquity ? formatPercent(peer.returnOnEquity * 100) : '-'}</TableCell>
                        <TableCell className="font-mono text-sm">{peer.debtToEquity ? peer.debtToEquity.toFixed(2) : '-'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>

          {peersData.correlationMatrix && (
            <Card className="bg-card rounded-none border-border">
              <CardHeader className="pb-2 bg-muted/30">
                <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">
                  Correlation Matrix
                </CardTitle>
              </CardHeader>
              <CardContent className="p-6">
                <div className="overflow-x-auto">
                  <div className="inline-block min-w-full align-middle">
                    <div className="grid" style={{ gridTemplateColumns: `auto ${'minmax(60px, 1fr) '.repeat(Object.keys(peersData.correlationMatrix).length)}` }}>
                      {/* Header Row */}
                      <div className="p-2"></div>
                      {Object.keys(peersData.correlationMatrix).map(ticker => (
                        <div key={ticker} className="p-2 font-mono text-xs font-bold text-center border-b border-border">{ticker}</div>
                      ))}
                      
                      {/* Data Rows */}
                      {Object.entries(peersData.correlationMatrix).map(([rowTicker, cols]) => (
                        <React.Fragment key={rowTicker}>
                          <div className="p-2 font-mono text-xs font-bold text-right border-r border-border pr-4 flex items-center justify-end">{rowTicker}</div>
                          {Object.keys(peersData.correlationMatrix).map(colTicker => {
                            const val = cols[colTicker];
                            // Color logic: 1 is cyan, 0 is dark/background, -1 is red
                            let bgClass = "bg-transparent";
                            let textClass = "text-foreground";
                            
                            if (val >= 0.8 && val < 1) bgClass = "bg-primary/40";
                            else if (val >= 0.5) bgClass = "bg-primary/20";
                            else if (val >= 0.2) bgClass = "bg-primary/10";
                            else if (val <= -0.5) bgClass = "bg-destructive/40";
                            else if (val <= -0.2) bgClass = "bg-destructive/20";
                            
                            if (val === 1) {
                              bgClass = "bg-primary/60";
                              textClass = "text-primary-foreground font-bold";
                            }
                            
                            return (
                              <div key={colTicker} className={`p-2 font-mono text-xs text-center border-b border-r border-border/30 flex items-center justify-center ${bgClass} ${textClass}`}>
                                {val !== undefined ? val.toFixed(2) : '-'}
                              </div>
                            );
                          })}
                        </React.Fragment>
                      ))}
                    </div>
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
import React from 'react';
