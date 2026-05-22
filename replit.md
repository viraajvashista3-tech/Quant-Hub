# Quant Terminal

A professional-grade stock analysis terminal for quant traders and investors — dense, real-time, multi-language.

## Run & Operate

- `pnpm --filter @workspace/api-server run dev` — run the API server (port 8080, proxied at `/api`)
- `pnpm --filter @workspace/quant-terminal run dev` — run the React frontend (proxied at `/`)
- `pnpm run typecheck` — full typecheck across all packages
- `pnpm run build` — typecheck + build all packages
- `pnpm --filter @workspace/api-spec run codegen` — regenerate API hooks and Zod schemas from the OpenAPI spec
- No database needed — all data is fetched live via yfinance

## Stack

- pnpm workspaces, Node.js 24, TypeScript 5.9
- Frontend: React + Vite + TailwindCSS + Recharts + shadcn/ui
- API: Express 5 (Node.js)
- Data engine: Python 3 + yfinance + pandas + numpy + VADER sentiment + feedparser
- API codegen: Orval (from OpenAPI spec → React Query hooks + Zod validators)
- Build: esbuild (CJS bundle for server)

## Where things live

- `lib/api-spec/openapi.yaml` — OpenAPI spec (source of truth for all API contracts)
- `lib/api-client-react/src/generated/` — generated React Query hooks (do not hand-edit)
- `lib/api-zod/src/generated/` — generated Zod validators (do not hand-edit)
- `scripts/python/stock_data.py` — Python data engine (yfinance, pandas, VADER)
- `artifacts/api-server/src/routes/stock.ts` — Express routes that call Python via child_process
- `artifacts/api-server/src/lib/python-bridge.ts` — Node → Python subprocess bridge
- `artifacts/quant-terminal/src/` — React frontend (pages, components)

## Architecture decisions

- **Multi-language bridge**: The Express server spawns `python3 scripts/python/stock_data.py <command> <args>` via `child_process.spawn` and parses JSON from stdout. This keeps Python's financial library ecosystem (yfinance, pandas, VADER) while using Node for the API layer.
- **OpenAPI-first**: All API contracts are defined in `lib/api-spec/openapi.yaml` first, then codegen produces typed hooks and Zod validators. Never hand-write types that codegen produces.
- **No database**: Stock data is fetched live from Yahoo Finance via yfinance. Caching is handled at the yfinance/OS level; no DB is needed for this app.
- **VADER sentiment**: News headlines from Google News RSS are scored with VADER (Valence Aware Dictionary and sEntiment Reasoner) for compound sentiment scores used in the quant scoring algorithm.
- **Orval barrel fix**: `lib/api-spec/package.json` codegen script patches `lib/api-zod/src/index.ts` after orval runs to remove the `generated/types` re-export, which causes TS2308 collisions on query param schemas.

## Product

- **Terminal** (`/`): Active ticker analysis — BUY/HOLD/AVOID quant signal, price chart with MA50/MA200, RSI, MACD, volume, news sentiment
- **Browse** (`/browse`): Full stock universe (11 sectors, 130+ tickers) — click any to analyze
- **Fundamentals** (`/fundamentals`): Deep ratio analysis — P/E, EV/EBITDA, margins, balance sheet metrics
- **Peers** (`/peers`): Sector peer comparison — relative performance chart, fundamentals table, correlation matrix
- **Analyst** (`/analyst`): Wall Street consensus rating, price targets (low/mean/high), recent upgrades/downgrades

## User preferences

_Populate as you build — explicit user instructions worth remembering across sessions._

## Gotchas

- Always run `pnpm --filter @workspace/api-spec run codegen` after any changes to `lib/api-spec/openapi.yaml`
- Python 3 must be available in PATH for the Express routes to work (it is on Replit)
- yfinance rate limits can cause occasional timeouts for batch peer requests — retries are handled by React Query
- The codegen script patches `lib/api-zod/src/index.ts` via `echo` — do not hand-edit that file
- `scripts/python/stock_data.py` is spawned per-request — for production, consider adding an in-process cache

## Pointers

- See the `pnpm-workspace` skill for workspace structure, TypeScript setup, and package details
