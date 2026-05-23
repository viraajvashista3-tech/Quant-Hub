import { createContext, useContext, useState, useEffect, ReactNode } from "react";

type Theme = "dark" | "light";

type AccentColor = {
  name: string;
  hsl: string;
  label: string;
};

export const ACCENT_COLORS: AccentColor[] = [
  { name: "cyan",   hsl: "190 100% 50%", label: "Cyan" },
  { name: "green",  hsl: "142 70% 45%",  label: "Green" },
  { name: "amber",  hsl: "40 90% 55%",   label: "Amber" },
  { name: "violet", hsl: "270 80% 60%",  label: "Violet" },
  { name: "rose",   hsl: "345 85% 60%",  label: "Rose" },
  { name: "blue",   hsl: "210 90% 55%",  label: "Blue" },
];

interface ThemeContextValue {
  theme: Theme;
  toggleTheme: () => void;
  accentColor: AccentColor;
  setAccentColor: (c: AccentColor) => void;
}

const ThemeContext = createContext<ThemeContextValue>({
  theme: "dark",
  toggleTheme: () => {},
  accentColor: ACCENT_COLORS[0],
  setAccentColor: () => {},
});

function applyTheme(theme: Theme, accent: AccentColor) {
  const root = document.documentElement;
  if (theme === "dark") {
    root.classList.add("dark");
    root.classList.remove("light-mode");
    root.style.setProperty("--primary", accent.hsl);
    root.style.setProperty("--ring", accent.hsl);
    root.style.setProperty("--sidebar-primary", accent.hsl);
    root.style.setProperty("--sidebar-ring", accent.hsl);
  } else {
    root.classList.remove("dark");
    root.classList.add("light-mode");
    root.style.setProperty("--primary", accent.hsl);
    root.style.setProperty("--ring", accent.hsl);
    root.style.setProperty("--sidebar-primary", accent.hsl);
    root.style.setProperty("--sidebar-ring", accent.hsl);
  }
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>(() => {
    return (localStorage.getItem("qt-theme") as Theme) ?? "dark";
  });
  const [accentColor, setAccentColorState] = useState<AccentColor>(() => {
    const saved = localStorage.getItem("qt-accent");
    return ACCENT_COLORS.find((c) => c.name === saved) ?? ACCENT_COLORS[0];
  });

  useEffect(() => {
    applyTheme(theme, accentColor);
  }, [theme, accentColor]);

  const toggleTheme = () => {
    setTheme((t) => {
      const next = t === "dark" ? "light" : "dark";
      localStorage.setItem("qt-theme", next);
      return next;
    });
  };

  const setAccentColor = (c: AccentColor) => {
    localStorage.setItem("qt-accent", c.name);
    setAccentColorState(c);
  };

  return (
    <ThemeContext.Provider value={{ theme, toggleTheme, accentColor, setAccentColor }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  return useContext(ThemeContext);
}
