import { Switch, Route, Router as WouterRouter } from "wouter";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Toaster } from "@/components/ui/toaster";
import { TooltipProvider } from "@/components/ui/tooltip";
import { TickerProvider } from "@/lib/ticker-context";
import { AppLayout } from "@/components/layout/app-layout";
import { useEffect } from "react";

import Terminal from "@/pages/terminal";
import Browse from "@/pages/browse";
import Analyst from "@/pages/analyst";
import Peers from "@/pages/peers";
import Fundamentals from "@/pages/fundamentals";
import NotFound from "@/pages/not-found";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      refetchOnWindowFocus: false,
    },
  },
});

function Router() {
  return (
    <AppLayout>
      <Switch>
        <Route path="/" component={Terminal} />
        <Route path="/browse" component={Browse} />
        <Route path="/analyst" component={Analyst} />
        <Route path="/peers" component={Peers} />
        <Route path="/fundamentals" component={Fundamentals} />
        <Route component={NotFound} />
      </Switch>
    </AppLayout>
  );
}

function App() {
  useEffect(() => {
    document.documentElement.classList.add('dark');
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <TickerProvider>
        <TooltipProvider>
          <WouterRouter base={import.meta.env.BASE_URL.replace(/\/$/, "")}>
            <Router />
          </WouterRouter>
          <Toaster />
        </TooltipProvider>
      </TickerProvider>
    </QueryClientProvider>
  );
}

export default App;
