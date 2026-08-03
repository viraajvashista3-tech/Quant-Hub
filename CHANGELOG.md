# Changelog

All notable changes to this project are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] — 2026-08-03

First release of the native Windows desktop version.

### Added
- Full migration from WPF to Avalonia 11 + FluentAvaloniaUI — same native Windows app, modern
  rendering/theming stack.
- Quant scoring engine v2: seven continuous signals (trend, momentum, MACD, volatility, mean
  reversion, short-term reversal, sector-relative strength), auto-recalibrated weekly against a
  walk-forward backtest across the full 138-ticker universe.
- Walk-forward backtesting engine (`QuantHub.Core/Backtesting`) — OLS regression, out-of-sample
  validation, and excess-return-over-S&P-500 labeling so market drift can't masquerade as edge.
- Live, forward-tested prediction log: every Terminal view logs a timestamped prediction that
  can't benefit from hindsight; matured entries are scored automatically.
- **Track Record page** — the app's honesty page, showing the live hit rate for every signal
  type plus a plain-English explanation of the methodology.
- Full-universe rankings (Universe page) across 11 sectors.
- Analyst consensus, sector peer comparison, insider transaction, and market pulse pages.
- Earnings surprise chart (Fundamentals page).
- Watchlist with JSON export/import backup.
- Light/dark theme with configurable accent color; Beginner/Intermediate/Pro difficulty modes.
- Version metadata, an About card (Settings page), and crash logging to
  `%LOCALAPPDATA%\QuantHub\crash.log`.
- CI (GitHub Actions): build + full test suite on every push/PR to `main`.

### Changed
- Repositioned the app around transparency rather than prediction confidence: hero copy is
  descriptive ("indicators lean bullish") rather than directive ("consider buying"), and a
  permanent "for research only" disclaimer is always visible.

### Removed
- The Claude-based AI Research chat page (removed at the same time as the transparency
  repositioning, to keep the app's surface area limited to things it can back up with evidence).
- The legacy pre-Avalonia web prototype (React + Express + Python), which had been sitting
  untouched in the repo since the desktop pivot.
