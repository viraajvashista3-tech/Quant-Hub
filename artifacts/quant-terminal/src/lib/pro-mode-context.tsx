import { createContext, useContext, useState, useEffect, ReactNode } from "react";

export type Mode = "beginner" | "amateur" | "pro" | "master";

const MODE_ORDER: Mode[] = ["beginner", "amateur", "pro", "master"];

export const MODE_META: Record<Mode, { emoji: string; label: string; desc: string }> = {
  beginner: { emoji: "🌱", label: "Beginner", desc: "Plain English, no jargon" },
  amateur:  { emoji: "📊", label: "Amateur",  desc: "Charts & key metrics" },
  pro:      { emoji: "⚡", label: "Pro",      desc: "Full technical analysis" },
  master:   { emoji: "🔬", label: "Master",   desc: "Raw data & all signals" },
};

interface ModeContextValue {
  mode: Mode;
  setMode: (m: Mode) => void;
  isAtLeast: (m: Mode) => boolean;
  isPro: boolean;
}

const ModeContext = createContext<ModeContextValue>({
  mode: "amateur",
  setMode: () => {},
  isAtLeast: () => false,
  isPro: false,
});

export function ProModeProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<Mode>(() => {
    try {
      const saved = localStorage.getItem("viewMode") as Mode | null;
      if (saved && MODE_ORDER.includes(saved)) return saved;
      return "amateur";
    } catch { return "amateur"; }
  });

  useEffect(() => {
    try { localStorage.setItem("viewMode", mode); } catch {}
  }, [mode]);

  const setMode = (m: Mode) => setModeState(m);
  const isAtLeast = (m: Mode) => MODE_ORDER.indexOf(mode) >= MODE_ORDER.indexOf(m);

  return (
    <ModeContext.Provider value={{ mode, setMode, isAtLeast, isPro: isAtLeast("pro") }}>
      {children}
    </ModeContext.Provider>
  );
}

export function useProMode() {
  return useContext(ModeContext);
}

type LabelKey =
  | "quantScore" | "signal" | "rsi" | "macd" | "beta" | "annVol"
  | "volume" | "avgVolume" | "pe" | "forwardPe" | "peg" | "pb"
  | "evEbitda" | "debtEquity" | "roe" | "roa" | "profitMargin"
  | "opMargin" | "revGrowth" | "epsGrowth" | "currentRatio"
  | "quickRatio" | "shortRatio" | "shortFloat" | "institutionalOwn"
  | "dividendYield" | "eps" | "marketCap" | "consensusRating"
  | "priceTargets" | "recentActions" | "correlationMatrix" | "fundamentalsComparison";

const SIMPLE: Record<LabelKey, string> = {
  quantScore: "Signal Score",
  signal: "Recommendation",
  rsi: "Momentum (0–100)",
  macd: "Trend Direction",
  beta: "Market Sensitivity",
  annVol: "Price Volatility",
  volume: "Shares Traded",
  avgVolume: "Avg Daily Volume",
  pe: "Price vs Earnings (P/E)",
  forwardPe: "Expected P/E",
  peg: "Growth-Adjusted Value (PEG)",
  pb: "Price vs Book Value",
  evEbitda: "Company Value Score (EV/EBITDA)",
  debtEquity: "Debt Load (D/E)",
  roe: "Profit on Equity (ROE)",
  roa: "Profit on Assets (ROA)",
  profitMargin: "How Much It Keeps (Net Margin)",
  opMargin: "Operating Profit %",
  revGrowth: "Revenue Growth (YoY)",
  epsGrowth: "Earnings Growth (YoY)",
  currentRatio: "Short-Term Safety (Current Ratio)",
  quickRatio: "Liquid Safety (Quick Ratio)",
  shortRatio: "Days to Cover Shorts",
  shortFloat: "Short Sellers %",
  institutionalOwn: "Big Funds Ownership",
  dividendYield: "Dividend Payout %",
  eps: "Earnings Per Share",
  marketCap: "Company Size",
  consensusRating: "Analyst Verdict",
  priceTargets: "Where Analysts See It Going",
  recentActions: "Recent Analyst Calls",
  correlationMatrix: "How Stocks Move Together",
  fundamentalsComparison: "Side-by-Side Comparison",
};

const PRO: Record<LabelKey, string> = {
  quantScore: "Quant Score",
  signal: "Signal",
  rsi: "RSI (14)",
  macd: "MACD / Signal",
  beta: "Beta",
  annVol: "Ann. Volatility",
  volume: "Volume",
  avgVolume: "Avg Volume",
  pe: "P/E Ratio (TTM)",
  forwardPe: "Forward P/E",
  peg: "PEG Ratio",
  pb: "Price / Book",
  evEbitda: "EV / EBITDA",
  debtEquity: "Debt / Equity",
  roe: "Return on Equity",
  roa: "Return on Assets",
  profitMargin: "Net Profit Margin",
  opMargin: "Operating Margin",
  revGrowth: "Revenue Growth (YoY)",
  epsGrowth: "Earnings Growth (YoY)",
  currentRatio: "Current Ratio",
  quickRatio: "Quick Ratio",
  shortRatio: "Short Ratio",
  shortFloat: "Short % of Float",
  institutionalOwn: "Institutional Ownership",
  dividendYield: "Dividend Yield",
  eps: "Diluted EPS",
  marketCap: "Market Cap",
  consensusRating: "Consensus Rating",
  priceTargets: "Price Targets",
  recentActions: "Recent Actions",
  correlationMatrix: "Correlation Matrix",
  fundamentalsComparison: "Fundamentals Comparison",
};

export function useLabels() {
  const { isAtLeast } = useProMode();
  return (key: LabelKey) => (isAtLeast("pro") ? PRO[key] : SIMPLE[key]);
}
