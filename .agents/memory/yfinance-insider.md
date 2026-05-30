---
name: yfinance insider data shape
description: How yfinance returns insider transaction data and how to parse buy/sell type
---

## insider_transactions columns
`Shares, Value, URL, Text, Insider, Position, Transaction, Start Date, Ownership`

- `Text` field contains the transaction description — parse buy/sell from it:
  - "Sale at price..." → Sale
  - "Purchase at price..." → Purchase
  - "Stock Gift..." → Gift
  - "Option exercise..." → Option Exercise
- `Transaction` column is often empty; Text is the reliable source
- `Ownership`: "D" = direct, "I" = indirect

## insider_purchases (6-month summary)
Columns: `Insider Purchases Last 6m, Shares, Trans`
- Row with label "Purchases" = total purchase count/shares in last 6 months
- Row with label "Sales" = total sale count/shares

## major_holders
- `insidersPercentHeld` — better accessed via `tk.info["heldPercentInsiders"]`
- `institutionsPercentHeld` — via `tk.info["heldPercentInstitutions"]`

**Why:** yfinance API shape is undocumented and subject to change; tested against live data May 2026.
