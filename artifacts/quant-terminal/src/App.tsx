import { Switch, Route, Router as WouterRouter } from "wouter";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Toaster } from "@/components/ui/toaster";
import { TooltipProvider } from "@/components/ui/tooltip";
import { TickerProvider } from "@/lib/ticker-context";
import { ProModeProvider } from "@/lib/pro-mode-context";
import { ThemeProvider } from "@/lib/theme-context";
import { AppLayout } from "@/components/layout/app-layout";

import Terminal from "@/pages/terminal";
import Browse from "@/pages/browse";
import Analyst from "@/pages/analyst";
import Peers from "@/pages/peers";
import Fundamentals from "@/pages/fundamentals";
import AiChat from "@/pages/ai-chat";
import Insider from "@/pages/insider";
import Market from "@/pages/market";
import NotFound from "@/pages/not-found";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      refetchOnWindowFocus: false,
      staleTime: 5 * 60 * 1000,
      gcTime: 10 * 60 * 1000,
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
        <Route path="/ai" component={AiChat} />
        <Route path="/insider" component={Insider} />
        <Route path="/market" component={Market} />
        <Route component={NotFound} />
      </Switch>
    </AppLayout>
  );
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <TickerProvider>
          <ProModeProvider>
            <TooltipProvider>
              <WouterRouter base={import.meta.env.BASE_URL.replace(/\/$/, "")}>
                <Router />
              </WouterRouter>
              <Toaster />
            </TooltipProvider>
          </ProModeProvider>
        </TickerProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

export default App;
