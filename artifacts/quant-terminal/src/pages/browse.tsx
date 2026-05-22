import { useGetUniverse, getGetUniverseQueryKey } from "@workspace/api-client-react";
import { useTicker } from "@/lib/ticker-context";
import { useLocation } from "wouter";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

export default function Browse() {
  const { data: universe, isLoading } = useGetUniverse({
    query: { queryKey: getGetUniverseQueryKey() }
  });
  const { setActiveTicker } = useTicker();
  const [, setLocation] = useLocation();

  const handleSelectTicker = (ticker: string) => {
    setActiveTicker(ticker);
    setLocation("/");
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight text-primary uppercase">Universe</h1>
        <p className="text-muted-foreground mt-1">Browse active coverage grouped by sector.</p>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {Array(6).fill(0).map((_, i) => (
            <Skeleton key={i} className="h-48 w-full rounded-none" />
          ))}
        </div>
      ) : universe?.length ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
          {universe.map((sector) => (
            <Card key={sector.sector} className="bg-card rounded-none border-border hover:border-primary/50 transition-colors">
              <CardHeader className="pb-3 border-b border-border bg-muted/20">
                <CardTitle className="text-sm uppercase tracking-widest text-muted-foreground">
                  {sector.sector || "Uncategorized"}
                </CardTitle>
              </CardHeader>
              <CardContent className="p-0">
                <div className="flex flex-wrap p-3 gap-2">
                  {sector.tickers.map((ticker) => (
                    <button
                      key={ticker}
                      onClick={() => handleSelectTicker(ticker)}
                      className="px-2 py-1 text-sm font-mono border border-border hover:border-primary hover:text-primary transition-colors bg-background"
                    >
                      {ticker}
                    </button>
                  ))}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : (
        <div className="text-center py-12 text-muted-foreground">
          No universe data available.
        </div>
      )}
    </div>
  );
}
