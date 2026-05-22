import { useTicker } from "@/lib/ticker-context";
import { useLabels } from "@/lib/pro-mode-context";
import { useGetStockAnalyst, getGetStockAnalystQueryKey } from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency } from "@/lib/format";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { TrendingUp, TrendingDown, Minus } from "lucide-react";

function gradeColor(grade: string | null | undefined) {
  if (!grade) return "text-muted-foreground";
  const g = grade.toLowerCase();
  if (g.includes("strong buy") || g.includes("outperform") || g.includes("overweight") || g.includes("buy")) return "text-green-500";
  if (g.includes("underperform") || g.includes("sell") || g.includes("underweight")) return "text-destructive";
  return "text-foreground";
}

function TargetChangeChip({ action, current, prior }: { action: string | null | undefined; current: number | null | undefined; prior: number | null | undefined }) {
  if (!current) return null;
  const a = (action || "").toLowerCase();
  const raised = a === "raises" || a === "raised";
  const lowered = a === "lowers" || a === "lowered" || a === "lowered";
  const change = prior && current ? current - prior : null;

  return (
    <span className={`inline-flex items-center gap-1 font-mono text-xs px-2 py-0.5 ${raised ? "text-green-500 bg-green-500/10" : lowered ? "text-destructive bg-destructive/10" : "text-muted-foreground bg-muted/40"}`}>
      {raised ? <TrendingUp className="h-3 w-3" /> : lowered ? <TrendingDown className="h-3 w-3" /> : <Minus className="h-3 w-3" />}
      {formatCurrency(current)}
      {change !== null && prior && (
        <span className="opacity-70">({change > 0 ? "+" : ""}{formatCurrency(change)})</span>
      )}
    </span>
  );
}

export default function Analyst() {
  const { activeTicker } = useTicker();
  const label = useLabels();

  const { data: analyst, isLoading } = useGetStockAnalyst(activeTicker, {
    query: { enabled: !!activeTicker, queryKey: getGetStockAnalystQueryKey(activeTicker) }
  });

  if (!activeTicker) return <div className="p-8 text-center text-muted-foreground">Select a ticker to begin analysis.</div>;

  const minTarget = analyst?.targetLow || 0;
  const maxTarget = analyst?.targetHigh || 0;
  const currentPrice = analyst?.currentPrice || 0;

  let pricePos = 0;
  if (maxTarget > minTarget && currentPrice) {
    pricePos = Math.max(0, Math.min(100, ((currentPrice - minTarget) / (maxTarget - minTarget)) * 100));
  }

  // Count raises vs lowers for summary
  const actions = analyst?.recentActions || [];
  const raises = actions.filter(a => (a.priceTargetAction || "").toLowerCase() === "raises").length;
  const lowers = actions.filter(a => (a.priceTargetAction || "").toLowerCase() === "lowers").length;
  const upgrades = actions.filter(a => (a.action || "").toLowerCase() === "up").length;
  const downgrades = actions.filter(a => (a.action || "").toLowerCase() === "down").length;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight text-primary uppercase">Analyst Coverage: {activeTicker}</h1>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <Skeleton className="h-48 w-full" />
          <Skeleton className="h-48 w-full" />
          <Skeleton className="h-16 w-full md:col-span-2" />
          <Skeleton className="h-96 w-full md:col-span-2" />
        </div>
      ) : analyst ? (
        <div className="space-y-6">
          {/* Top cards */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <Card className="bg-card rounded-none border-border">
              <CardHeader>
                <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">{label("consensusRating")}</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col items-center justify-center py-4 gap-3">
                <div className={`text-5xl font-bold uppercase tracking-widest ${
                  analyst.consensusRating.toLowerCase().includes('buy') ? 'text-green-500' :
                  analyst.consensusRating.toLowerCase().includes('sell') ? 'text-destructive' :
                  'text-primary'
                }`}>
                  {analyst.consensusRating}
                </div>
                <div className="text-sm text-muted-foreground">
                  Based on {analyst.numAnalysts || 0} analysts
                </div>
                {(raises > 0 || lowers > 0 || upgrades > 0 || downgrades > 0) && (
                  <div className="flex gap-4 text-xs mt-1">
                    {raises > 0 && <span className="text-green-500">{raises} target raised{raises !== 1 ? 's' : ''}</span>}
                    {lowers > 0 && <span className="text-destructive">{lowers} target cut{lowers !== 1 ? 's' : ''}</span>}
                    {upgrades > 0 && <span className="text-green-500/70">{upgrades} upgrade{upgrades !== 1 ? 's' : ''}</span>}
                    {downgrades > 0 && <span className="text-destructive/70">{downgrades} downgrade{downgrades !== 1 ? 's' : ''}</span>}
                  </div>
                )}
              </CardContent>
            </Card>

            <Card className="bg-card rounded-none border-border">
              <CardHeader>
                <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">{label("priceTargets")}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-6 py-4">
                <div className="flex justify-between text-sm font-mono">
                  <div>
                    <div className="text-muted-foreground uppercase text-xs mb-1">Low</div>
                    <div className="text-lg">{formatCurrency(analyst.targetLow)}</div>
                  </div>
                  <div className="text-center">
                    <div className="text-muted-foreground uppercase text-xs mb-1">Mean</div>
                    <div className="text-xl text-primary font-bold">{formatCurrency(analyst.targetMean)}</div>
                    {analyst.targetMean && currentPrice && (
                      <div className={`text-xs mt-1 ${analyst.targetMean > currentPrice ? 'text-green-500' : 'text-destructive'}`}>
                        {analyst.targetMean > currentPrice ? '▲' : '▼'} {Math.abs(((analyst.targetMean - currentPrice) / currentPrice) * 100).toFixed(1)}% from current
                      </div>
                    )}
                  </div>
                  <div className="text-right">
                    <div className="text-muted-foreground uppercase text-xs mb-1">High</div>
                    <div className="text-lg">{formatCurrency(analyst.targetHigh)}</div>
                  </div>
                </div>

                {maxTarget > minTarget && (
                  <div className="relative h-2 bg-muted rounded-full w-full">
                    {analyst.targetMean && (
                      <div
                        className="absolute top-0 bottom-0 w-0.5 bg-primary/50 z-0"
                        style={{ left: `${Math.max(0, Math.min(100, ((analyst.targetMean - minTarget) / (maxTarget - minTarget)) * 100))}%`, transform: 'translateX(-50%)' }}
                      />
                    )}
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

          {/* Broker price target cards */}
          {actions.filter(a => a.currentPriceTarget).length > 0 && (
            <div>
              <h2 className="text-xs font-semibold uppercase tracking-widest text-muted-foreground mb-3">Recent Broker Targets</h2>
              <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3">
                {actions.filter(a => a.currentPriceTarget).slice(0, 10).map((action, i) => {
                  const ta = (action.priceTargetAction || "").toLowerCase();
                  const raised = ta === "raises";
                  const lowered = ta === "lowers";
                  return (
                    <div key={i} className={`p-3 border rounded-none bg-card flex flex-col gap-1.5 ${
                      raised ? 'border-green-500/30' : lowered ? 'border-destructive/30' : 'border-border'
                    }`}>
                      <div className="flex items-start justify-between gap-1">
                        <span className="text-xs font-semibold text-foreground leading-tight">{action.firm}</span>
                        {raised ? (
                          <TrendingUp className="h-3.5 w-3.5 text-green-500 shrink-0 mt-0.5" />
                        ) : lowered ? (
                          <TrendingDown className="h-3.5 w-3.5 text-destructive shrink-0 mt-0.5" />
                        ) : (
                          <Minus className="h-3.5 w-3.5 text-muted-foreground shrink-0 mt-0.5" />
                        )}
                      </div>
                      <div className={`text-lg font-bold font-mono ${raised ? 'text-green-500' : lowered ? 'text-destructive' : 'text-foreground'}`}>
                        {formatCurrency(action.currentPriceTarget)}
                      </div>
                      {action.priorPriceTarget && action.currentPriceTarget && (
                        <div className="text-xs text-muted-foreground font-mono">
                          from {formatCurrency(action.priorPriceTarget)}
                        </div>
                      )}
                      <div className={`text-xs font-medium ${gradeColor(action.toGrade)}`}>{action.toGrade || '—'}</div>
                      <div className="text-[10px] text-muted-foreground">{action.date || ''}</div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Full actions table */}
          <Card className="bg-card rounded-none border-border">
            <CardHeader className="bg-muted/30">
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-widest">{label("recentActions")}</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {actions.length > 0 ? (
                <Table>
                  <TableHeader>
                    <TableRow className="border-border">
                      <TableHead className="w-[110px]">Date</TableHead>
                      <TableHead>Firm</TableHead>
                      <TableHead>Rating</TableHead>
                      <TableHead>Price Target</TableHead>
                      <TableHead className="hidden md:table-cell">From</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {actions.map((action, i) => (
                      <TableRow key={i} className="border-border">
                        <TableCell className="font-mono text-xs text-muted-foreground">{action.date || '-'}</TableCell>
                        <TableCell className="font-semibold text-sm">{action.firm}</TableCell>
                        <TableCell>
                          <span className={`text-sm font-medium ${gradeColor(action.toGrade)}`}>
                            {action.toGrade || '—'}
                          </span>
                        </TableCell>
                        <TableCell>
                          <TargetChangeChip
                            action={action.priceTargetAction}
                            current={action.currentPriceTarget}
                            prior={action.priorPriceTarget}
                          />
                        </TableCell>
                        <TableCell className="text-muted-foreground text-xs hidden md:table-cell">{action.fromGrade || '—'}</TableCell>
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
