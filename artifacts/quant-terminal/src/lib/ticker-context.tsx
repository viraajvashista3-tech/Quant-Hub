import { createContext, useContext, useState, ReactNode } from "react";

interface TickerContextType {
  activeTicker: string;
  setActiveTicker: (ticker: string) => void;
}

const TickerContext = createContext<TickerContextType | undefined>(undefined);

export function TickerProvider({ children }: { children: ReactNode }) {
  const [activeTicker, setActiveTicker] = useState("AAPL");

  return (
    <TickerContext.Provider value={{ activeTicker, setActiveTicker }}>
      {children}
    </TickerContext.Provider>
  );
}

export function useTicker() {
  const context = useContext(TickerContext);
  if (context === undefined) {
    throw new Error("useTicker must be used within a TickerProvider");
  }
  return context;
}
