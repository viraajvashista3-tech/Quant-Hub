export function formatLargeNumber(num: number | null | undefined): string {
  if (num == null) return "-";
  if (Math.abs(num) >= 1.0e12) return (num / 1.0e12).toFixed(2) + "T";
  if (Math.abs(num) >= 1.0e9) return (num / 1.0e9).toFixed(2) + "B";
  if (Math.abs(num) >= 1.0e6) return (num / 1.0e6).toFixed(2) + "M";
  return num.toLocaleString(undefined, { maximumFractionDigits: 2 });
}

export function formatPercent(num: number | null | undefined): string {
  if (num == null) return "-";
  return (num > 0 ? "+" : "") + num.toFixed(2) + "%";
}

export function formatCurrency(num: number | null | undefined): string {
  if (num == null) return "-";
  return "$" + num.toFixed(2);
}

export function formatValue(num: number | null | undefined, type: "currency" | "percent" | "number" = "number"): string {
  if (num == null) return "-";
  switch (type) {
    case "currency": return formatCurrency(num);
    case "percent": return num.toFixed(2) + "%";
    case "number": return formatLargeNumber(num);
  }
}
