import { ReactNode, useState } from "react";
import { Link, useLocation } from "wouter";
import { useTicker } from "@/lib/ticker-context";
import { useProMode, MODE_META, type Mode } from "@/lib/pro-mode-context";
import { useTheme, ACCENT_COLORS } from "@/lib/theme-context";
import { Input } from "@/components/ui/input";
import { Search, Activity, BookOpen, Users, BarChart2, Compass, Sun, Moon, Bot, Settings2, X, Eye, Globe } from "lucide-react";
import { Sidebar, SidebarContent, SidebarHeader, SidebarMenu, SidebarMenuItem, SidebarMenuButton, SidebarProvider, SidebarFooter } from "@/components/ui/sidebar";

const MODES: Mode[] = ["beginner", "amateur", "pro", "master"];

function SettingsPanel({ onClose }: { onClose: () => void }) {
  const { theme, toggleTheme, accentColor, setAccentColor } = useTheme();
  const { mode, setMode } = useProMode();

  return (
    <div className="fixed inset-0 z-50 flex">
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />
      <div className="relative ml-64 w-80 bg-card border-r border-border h-full overflow-y-auto p-5 space-y-6 shadow-2xl">
        <div className="flex items-center justify-between">
          <span className="text-xs font-bold uppercase tracking-widest text-muted-foreground">Customise</span>
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground transition-colors">
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* View Mode */}
        <div>
          <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground mb-3">View Mode</p>
          <div className="space-y-2">
            {MODES.map((m) => {
              const meta = MODE_META[m];
              const active = mode === m;
              return (
                <button
                  key={m}
                  onClick={() => setMode(m)}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 border transition-colors text-left ${
                    active
                      ? "border-primary bg-primary/10 text-primary"
                      : "border-border hover:border-primary/40 hover:bg-primary/5 text-muted-foreground hover:text-foreground"
                  }`}
                >
                  <span className="text-xl leading-none">{meta.emoji}</span>
                  <div className="flex-1 min-w-0">
                    <div className={`text-xs font-bold uppercase tracking-widest ${active ? "text-primary" : "text-foreground"}`}>
                      {meta.label}
                    </div>
                    <div className="text-[10px] text-muted-foreground">{meta.desc}</div>
                  </div>
                  {active && (
                    <div className="w-2 h-2 rounded-full bg-primary flex-shrink-0" />
                  )}
                </button>
              );
            })}
          </div>
        </div>

        {/* Theme */}
        <div>
          <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground mb-3">Appearance</p>
          <button
            onClick={toggleTheme}
            className="w-full flex items-center justify-between px-3 py-2.5 border border-border hover:border-primary/60 bg-background hover:bg-primary/5 transition-colors text-sm"
          >
            <div className="flex items-center gap-2">
              {theme === "dark" ? <Moon className="h-4 w-4 text-primary" /> : <Sun className="h-4 w-4 text-amber-500" />}
              <span>{theme === "dark" ? "Dark mode" : "Light mode"}</span>
            </div>
            <span className="text-xs text-muted-foreground">Toggle</span>
          </button>
        </div>

        {/* Accent colour */}
        <div>
          <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground mb-3">Accent Colour</p>
          <div className="grid grid-cols-3 gap-2">
            {ACCENT_COLORS.map((c) => (
              <button
                key={c.name}
                onClick={() => setAccentColor(c)}
                className={`flex flex-col items-center gap-1.5 px-2 py-2 border transition-colors text-xs ${
                  accentColor.name === c.name
                    ? "border-primary bg-primary/10 text-primary"
                    : "border-border hover:border-border/80 text-muted-foreground hover:text-foreground"
                }`}
              >
                <span className="w-5 h-5 rounded-full block" style={{ background: `hsl(${c.hsl})` }} />
                {c.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

export function AppLayout({ children }: { children: ReactNode }) {
  const [location] = useLocation();
  const { activeTicker, setActiveTicker } = useTicker();
  const { theme, toggleTheme } = useTheme();
  const [settingsOpen, setSettingsOpen] = useState(false);

  return (
    <SidebarProvider>
      <div className={`flex h-screen w-full overflow-hidden bg-background text-foreground ${theme === "dark" ? "dark" : "light-mode"}`}>
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

              <SidebarMenuItem>
                <SidebarMenuButton asChild isActive={location === "/insider"}>
                  <Link href="/insider" className="flex items-center gap-3">
                    <Eye className="h-4 w-4" />
                    <span>Insider</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
              <SidebarMenuItem>
                <SidebarMenuButton asChild isActive={location === "/market"}>
                  <Link href="/market" className="flex items-center gap-3">
                    <Globe className="h-4 w-4" />
                    <span>Market Pulse</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>

              {/* Divider */}
              <div className="my-2 border-t border-border/50" />

              <SidebarMenuItem>
                <SidebarMenuButton asChild isActive={location === "/ai"}>
                  <Link href="/ai" className="flex items-center gap-3">
                    <Bot className="h-4 w-4" />
                    <span className="flex items-center gap-2">
                      AI Research
                      <span className="text-[9px] font-bold uppercase tracking-widest px-1.5 py-0.5 bg-primary/20 text-primary border border-primary/30 leading-none">
                        BETA
                      </span>
                    </span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarContent>

          <SidebarFooter className="p-4 border-t border-border flex flex-row gap-2">
            <button
              onClick={toggleTheme}
              title={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
              className="flex items-center justify-center w-9 h-9 border border-border hover:border-primary/60 hover:bg-primary/5 text-muted-foreground hover:text-foreground transition-colors"
            >
              {theme === "dark" ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
            </button>
            <button
              onClick={() => setSettingsOpen(true)}
              className="flex-1 flex items-center justify-center gap-2 h-9 border border-border hover:border-primary/60 hover:bg-primary/5 text-muted-foreground hover:text-foreground transition-colors text-xs font-semibold uppercase tracking-widest"
            >
              <Settings2 className="h-4 w-4" />
              Customise
            </button>
          </SidebarFooter>
        </Sidebar>

        <main className="flex-1 flex flex-col overflow-hidden relative">
          <div className="absolute inset-0 pointer-events-none opacity-[0.03]" style={{ backgroundImage: 'linear-gradient(to right, currentColor 1px, transparent 1px), linear-gradient(to bottom, currentColor 1px, transparent 1px)', backgroundSize: '40px 40px' }} />
          <div className="flex-1 overflow-auto p-6 relative z-10">
            {children}
          </div>
        </main>
      </div>

      {settingsOpen && <SettingsPanel onClose={() => setSettingsOpen(false)} />}
    </SidebarProvider>
  );
}
