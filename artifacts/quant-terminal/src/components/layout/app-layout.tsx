import { ReactNode } from "react";
import { Link, useLocation } from "wouter";
import { useTicker } from "@/lib/ticker-context";
import { Input } from "@/components/ui/input";
import { Search, Activity, BookOpen, Users, BarChart2, Compass } from "lucide-react";
import { Sidebar, SidebarContent, SidebarHeader, SidebarMenu, SidebarMenuItem, SidebarMenuButton, SidebarProvider } from "@/components/ui/sidebar";

export function AppLayout({ children }: { children: ReactNode }) {
  const [location] = useLocation();
  const { activeTicker, setActiveTicker } = useTicker();

  return (
    <SidebarProvider>
      <div className="flex h-screen w-full overflow-hidden bg-background text-foreground dark">
        <Sidebar className="border-r border-border bg-card w-64 flex-shrink-0">
          <SidebarHeader className="p-4 border-b border-border">
            <div className="flex items-center gap-2 mb-4">
              <Activity className="h-6 w-6 text-primary" />
              <span className="font-bold text-lg tracking-tight">QUANT TERM</span>
            </div>
            <div className="relative">
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Ticker (e.g. AAPL)"
                className="pl-9 bg-background border-border font-mono uppercase focus-visible:ring-primary"
                value={activeTicker}
                onChange={(e) => setActiveTicker(e.target.value.toUpperCase())}
              />
            </div>
          </SidebarHeader>
          <SidebarContent className="p-2">
            <SidebarMenu>
              <SidebarMenuItem>
                <SidebarMenuButton asChild isActive={location === "/"}>
                  <Link href="/" className="flex items-center gap-3">
                    <Activity className="h-4 w-4" />
                    <span>Terminal</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
              <SidebarMenuItem>
                <SidebarMenuButton asChild isActive={location === "/browse"}>
                  <Link href="/browse" className="flex items-center gap-3">
                    <Compass className="h-4 w-4" />
                    <span>Universe</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
              <SidebarMenuItem>
                <SidebarMenuButton asChild isActive={location === "/analyst"}>
                  <Link href="/analyst" className="flex items-center gap-3">
                    <Users className="h-4 w-4" />
                    <span>Analyst</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
              <SidebarMenuItem>
                <SidebarMenuButton asChild isActive={location === "/peers"}>
                  <Link href="/peers" className="flex items-center gap-3">
                    <BarChart2 className="h-4 w-4" />
                    <span>Peers</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
              <SidebarMenuItem>
                <SidebarMenuButton asChild isActive={location === "/fundamentals"}>
                  <Link href="/fundamentals" className="flex items-center gap-3">
                    <BookOpen className="h-4 w-4" />
                    <span>Fundamentals</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarContent>
        </Sidebar>
        
        <main className="flex-1 flex flex-col overflow-hidden relative">
          {/* Subtle grid background pattern */}
          <div className="absolute inset-0 pointer-events-none opacity-5" style={{ backgroundImage: 'linear-gradient(to right, #ffffff 1px, transparent 1px), linear-gradient(to bottom, #ffffff 1px, transparent 1px)', backgroundSize: '40px 40px' }} />
          
          <div className="flex-1 overflow-auto p-6 relative z-10">
            {children}
          </div>
        </main>
      </div>
    </SidebarProvider>
  );
}
