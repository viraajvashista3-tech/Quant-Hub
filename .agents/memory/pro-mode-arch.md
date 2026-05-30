---
name: Pro Mode architecture
description: What Pro Mode actually does in the quant terminal — real content additions, not just label changes
---

## Rule
Pro Mode shows **genuinely new content and metrics**, not just label relabeling.

## What Pro Mode adds (as of May 2026)
- **Terminal**: Bollinger Bands (BB toggle button on chart), Sharpe Ratio + Max Drawdown in Key Metrics, Quant Score Breakdown card showing 4 component scores
- **Fundamentals**: Balance Sheet Snapshot card (revenue/FCF/debt/cash), Graham Number in Valuation table, BVPS + shares outstanding, FCF in Financial Health, Graham Valuation Analysis card
- **Analyst**: Recommendation Trend stacked bar chart (4 months of strongBuy/buy/hold/sell/strongSell), shows 40 broker actions instead of 20
- **Insider**: Full transaction description (Text field) shown in table
- **AI Chat**: Same for all modes (markdown rendering active for everyone)

## Labels
`useLabels()` still switches between plain-English (SIMPLE) and technical (PRO) label maps — but this is secondary to actual content gating.

## Persistence
`isPro` is read/written to `localStorage` via `useState` initializer + `useEffect`. Survives page refresh.

**Why:** User wanted "real new content/features" not just label relabeling — decided during May 2026 feature implementation.
