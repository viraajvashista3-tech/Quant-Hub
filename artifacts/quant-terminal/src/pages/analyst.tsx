import { useTicker } from "@/lib/ticker-context";
import { useGetStockAnalyst, getGetStockAnalystQueryKey } from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency } from "@/lib/format";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

export default function Analyst() {
  const { activeTicker } = useTicker();

  const { data: analyst, isLoading } = useGetStockAnalyst(activeTicker, {
    query: { enabled: !!activeTicker, queryKey: getGetStockAnalystQueryKey(activeTicker) }
  });

  if (!activeTicker) return <div className="p-8 text-center text-muted-foreground">Select a ticker to begin analysis.</div>;

  const minTarget = analyst?.targetLow || 0;
  const maxTarget = analyst?.targetHigh || 0;
  const currentPrice = analyst?.currentPrice || 0;
  
  // Calculate positions for target bar
  let pricePos = 0;
  if (maxTarget > minTarget && currentPrice) {
    const range = maxTarget - minTarget;
    pricePos = Math.max(0, Math.min(100, ((currentPrice - minTarget) / range) * 100));
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight text-primary uppercase">Analyst Coverage: {activeTicker}</h1>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <Skeleton className="h-48 w-full" />
          <Skeleton className="h-48 w-full" />
          <Skeleton className="h-96 w-full md:col-span-2" />
        </div>
      ) : analyst ? (
        <div className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <Card className="bg-card rounded-none border-border">
              <CardHeader>
                <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">Consensus Rating</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col items-center justify-center py-6">
                <div className={`text-5xl font-bold uppercase tracking-widest ${
                  analyst.consensusRating.includes('Buy') ? 'text-green-500' :
                  analyst.consensusRating.includes('Sell') ? 'text-destructive' :
                  'text-primary'
                }`}>
                  {analyst.consensusRating}
                </div>
                <div className="text-sm text-muted-foreground mt-4">
                  Based on {analyst.numAnalysts || 0} analysts
                </div>
              </CardContent>
            </Card>

            <Card className="bg-card rounded-none border-border">
              <CardHeader>
                <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">Price Targets</CardTitle>
              </CardHeader>
              <CardContent className="space-y-8 py-6">
                <div className="flex justify-between text-sm font-mono">
                  <div>
                    <div className="text-muted-foreground uppercase text-xs">Low</div>
                    <div className="text-lg">{formatCurrency(analyst.targetLow)}</div>
                  </div>
                  <div className="text-center">
                    <div className="text-muted-foreground uppercase text-xs">Mean</div>
                    <div className="text-xl text-primary font-bold">{formatCurrency(analyst.targetMean)}</div>
                  </div>
                  <div className="text-right">
                    <div className="text-muted-foreground uppercase text-xs">High</div>
                    <div className="text-lg">{formatCurrency(analyst.targetHigh)}</div>
                  </div>
                </div>

                {maxTarget > minTarget && (
                  <div className="relative h-2 bg-muted rounded-full w-full mx-auto">
                    {/* Mean line */}
                    {analyst.targetMean && (
                       <div 
                         className="absolute top-0 bottom-0 w-1 bg-primary/50 z-0" 
                         style={{ left: `${Math.max(0, Math.min(100, ((analyst.targetMean - minTarget) / (maxTarget - minTarget)) * 100))}%`, transform: 'translateX(-50%)' }}
                       />
                    )}
                    {/* Current Price Marker */}
                    {currentPrice && (
                      <div 
                        className="absolute w-4 h-4 bg-primary rounded-full shadow border-2 border-background z-10"
                        style={{ left: `${pricePos}%`, top: '50%', transform: 'translate(-50%, -50%)' }}
                        title={`Current: ${formatCurrency(currentPrice)}`}
                      />
                    )}
                  </div>
                )}
                
                <div className="text-center text-sm">
                  <span className="text-muted-foreground">Current Price: </span>
                  <span className="font-mono">{formatCurrency(currentPrice)}</span>
                </div>
              </CardContent>
            </Card>
          </div>

          <Card className="bg-card rounded-none border-border">
            <CardHeader>
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">Recent Actions</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {analyst.recentActions && analyst.recentActions.length > 0 ? (
                <Table>
                  <TableHeader className="bg-muted/30">
                    <TableRow className="border-border">
                      <TableHead className="w-[120px]">Date</TableHead>
                      <TableHead>Firm</TableHead>
                      <TableHead>Action</TableHead>
                      <TableHead>From</TableHead>
                      <TableHead>To</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {analyst.recentActions.map((action, i) => (
                      <TableRow key={i} className="border-border">
                        <TableCell className="font-mono text-xs">{action.date ? new Date(action.date).toLocaleDateString() : '-'}</TableCell>
                        <TableCell className="font-medium">{action.firm}</TableCell>
                        <TableCell>
                          <span className={`px-2 py-1 text-xs uppercase tracking-wider ${
                            action.action.toLowerCase().includes('up') ? 'text-green-500 bg-green-500/10' :
                            action.action.toLowerCase().includes('down') ? 'text-destructive bg-destructive/10' :
                            'bg-muted'
                          }`}>
                            {action.action}
                          </span>
                        </TableCell>
                        <TableCell className="text-muted-foreground">{action.fromGrade || '-'}</TableCell>
                        <TableCell className="font-bold">{action.toGrade || '-'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              ) : (
                <div className="p-8 text-center text-muted-foreground">No recent actions available.</div>
              )}
            </CardContent>
          </Card>
        </div>
      ) : (
        <div className="text-center py-12 text-muted-foreground">No analyst data available.</div>
      )}
    </div>
  );
}
