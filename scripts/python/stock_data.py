#!/usr/bin/env python3
"""
Quant Terminal - Stock Data Engine
Multi-tool financial data fetcher using yfinance + VADER sentiment
"""

import sys
import json
import argparse
import numpy as np
import pandas as pd
import feedparser
import yfinance as yf
from vaderSentiment.vaderSentiment import SentimentIntensityAnalyzer

UNIVERSE = {
    "Basic Materials": ["BHP", "VALE", "FCX", "NEM", "LIN", "APD", "CTVA", "SHW", "ECL", "SCCO", "STLD", "NUE", "AA", "RIO"],
    "Energy": ["XOM", "CVX", "SHEL", "BP", "TTE", "COP", "EOG", "SLB", "PBR", "ENB", "MPC", "PSX", "VLO", "WDS"],
    "Technology": ["AAPL", "MSFT", "NVDA", "AVGO", "ORCL", "CRM", "AMD", "QCOM", "TXN", "NOW", "INTU", "IBM", "AMAT", "MU", "ADI"],
    "Financial Services": ["JPM", "BAC", "WFC", "MS", "GS", "HSBC", "RY", "TD", "C", "BLK", "BX", "UBS", "SAN", "AXP"],
    "Healthcare": ["LLY", "UNH", "JNJ", "ABBV", "MRK", "TMO", "PFE", "ABT", "AMGN", "DHR", "ISRG", "BMY", "GILD", "VRTX"],
    "Consumer Cyclical": ["AMZN", "TSLA", "HD", "NKE", "MCD", "LOW", "SBUX", "BKNG", "TJX", "TM", "MAR"],
    "Consumer Defensive": ["PG", "KO", "PEP", "COST", "WMT", "PM", "UL", "ABEV", "MO", "TGT", "DG", "KMB"],
    "Communication Services": ["GOOGL", "META", "NFLX", "DIS", "TMUS", "VZ", "T", "CMCSA", "CHTR", "AMX"],
    "Industrials": ["CAT", "HON", "GE", "UNP", "UPS", "LMT", "BA", "RTX", "DE", "MMM", "ADP", "CP", "ETN"],
    "Utilities": ["NEE", "DUK", "SO", "EXC", "AEP", "SRE", "D", "ED", "PEG", "PCG", "NGG"],
    "Real Estate": ["PLD", "AMT", "EQIX", "O", "CCI", "WY", "PSA", "DLR", "VICI", "CBRE"],
}

analyzer = SentimentIntensityAnalyzer()


def safe_float(val, default=None):
    if val is None:
        return default
    try:
        f = float(val)
        if np.isnan(f) or np.isinf(f):
            return default
        return f
    except (TypeError, ValueError):
        return default


def flatten_df(df):
    """Flatten multi-level columns from yfinance (e.g. ('Close','AAPL') → 'Close')."""
    if isinstance(df.columns, pd.MultiIndex):
        df = df.copy()
        df.columns = [col[0] if isinstance(col, tuple) else col for col in df.columns]
    return df


def download_single(ticker, period="1y", auto_adjust=True):
    """Download a single ticker and return a flat-column DataFrame."""
    df = yf.download(ticker, period=period, interval="1d", progress=False, auto_adjust=auto_adjust)
    return flatten_df(df)


def calculate_indicators(df):
    """Compute MA50, MA200, MACD, Signal, RSI. Expects flat-column DataFrame."""
    out = df.copy()
    close = out["Close"]

    out["MA50"] = close.rolling(50).mean()
    out["MA200"] = close.rolling(200).mean()

    ema12 = close.ewm(span=12, adjust=False).mean()
    ema26 = close.ewm(span=26, adjust=False).mean()
    out["MACD"] = ema12 - ema26
    out["Signal"] = out["MACD"].ewm(span=9, adjust=False).mean()

    delta = close.diff()
    gain = delta.clip(lower=0).ewm(alpha=1 / 14, adjust=False).mean()
    loss = (-delta.clip(upper=0)).ewm(alpha=1 / 14, adjust=False).mean()
    out["RSI"] = 100 - (100 / (1 + (gain / (loss + 1e-10))))

    return out


def fetch_sentiment(ticker):
    """Fetch RSS news for ticker and return (score, headlines_list)."""
    try:
        feed = feedparser.parse(
            f"https://news.google.com/rss/search?q={ticker}+stock+news"
        )
        entries = feed.entries[:12]
        headlines = []
        scores = []
        for e in entries:
            title = e.get("title", "")
            url = e.get("link", "")
            published = e.get("published", None)
            score = analyzer.polarity_scores(title)["compound"]
            scores.append(score)
            headlines.append(
                {"title": title, "url": url, "publishedAt": published, "sentiment": round(score, 4)}
            )
        avg = float(np.mean(scores)) if scores else 0.0
        return avg, headlines
    except Exception:
        return 0.0, []


def sentiment_label(score):
    if score >= 0.3:
        return "Bullish"
    elif score >= 0.05:
        return "Mildly Bullish"
    elif score <= -0.3:
        return "Bearish"
    elif score <= -0.05:
        return "Mildly Bearish"
    return "Neutral"


def get_peers_for_ticker(ticker):
    for sector, tickers in UNIVERSE.items():
        if ticker.upper() in tickers:
            return sector, [t for t in tickers if t != ticker.upper()]
    return None, []


# ── Commands ─────────────────────────────────────────────────────────────────

def cmd_overview(ticker):
    tk = yf.Ticker(ticker)
    info = tk.info

    df = download_single(ticker, period="1y")
    if df.empty:
        print(json.dumps({"error": f"No data found for {ticker}"}))
        return

    df = calculate_indicators(df)
    close = df["Close"]
    vol = df["Volume"]

    latest_close = float(close.iloc[-1])
    prev_close = float(close.iloc[-2]) if len(close) > 1 else latest_close
    change = latest_close - prev_close
    change_pct = (change / prev_close) * 100 if prev_close != 0 else 0.0

    latest_rsi = safe_float(df["RSI"].iloc[-1], 50.0)
    ma50 = safe_float(df["MA50"].iloc[-1])
    ma200 = safe_float(df["MA200"].iloc[-1])
    latest_macd = safe_float(df["MACD"].iloc[-1], 0.0)
    latest_signal = safe_float(df["Signal"].iloc[-1], 0.0)
    latest_vol = int(vol.iloc[-1])
    avg_vol = int(vol.mean())

    daily_rets = close.pct_change().dropna()
    ann_vol = float(daily_rets.std() * np.sqrt(252) * 100)

    sent_score, _ = fetch_sentiment(ticker)

    trend_score = 20.0 if (ma200 and latest_close > ma200) else -20.0
    momentum_score = 20.0 if 40 < latest_rsi < 70 else (-10.0 if latest_rsi >= 70 else -20.0)
    vol_score = 10.0 if latest_vol > avg_vol * 0.5 else 0.0
    quant_score = (sent_score * 40) + trend_score + momentum_score + vol_score

    signal = "BUY" if quant_score > 15 else "HOLD" if quant_score > -10 else "AVOID"

    result = {
        "ticker": ticker.upper(),
        "name": info.get("shortName", ticker.upper()),
        "price": round(latest_close, 4),
        "change": round(change, 4),
        "changePercent": round(change_pct, 4),
        "quantScore": round(quant_score, 2),
        "signal": signal,
        "sentimentScore": round(sent_score, 4),
        "volume": latest_vol,
        "avgVolume": avg_vol,
        "rsi": round(latest_rsi, 2),
        "ma50": round(ma50, 4) if ma50 is not None else None,
        "ma200": round(ma200, 4) if ma200 is not None else None,
        "macd": round(latest_macd, 4),
        "macdSignal": round(latest_signal, 4),
        "sector": info.get("sector"),
        "beta": safe_float(info.get("beta")),
        "annualizedVolatility": round(ann_vol, 2),
    }
    print(json.dumps(result))


def cmd_history(ticker, period="1y"):
    period_map = {"6mo": "6mo", "1y": "1y", "2y": "2y", "5y": "5y"}
    yf_period = period_map.get(period, "1y")

    df = download_single(ticker, period=yf_period)
    if df.empty:
        print(json.dumps({"error": f"No data found for {ticker}"}))
        return

    df = calculate_indicators(df)

    bars = []
    for idx, row in df.iterrows():
        def sv(col):
            v = row.get(col)
            if v is None or (isinstance(v, float) and (np.isnan(v) or np.isinf(v))):
                return None
            return round(float(v), 4)

        bars.append({
            "date": str(idx.date()),
            "open": sv("Open"),
            "high": sv("High"),
            "low": sv("Low"),
            "close": sv("Close"),
            "volume": int(row["Volume"]) if row.get("Volume") is not None else 0,
            "ma50": sv("MA50"),
            "ma200": sv("MA200"),
            "macd": sv("MACD"),
            "macdSignal": sv("Signal"),
            "rsi": sv("RSI"),
        })

    print(json.dumps({"ticker": ticker.upper(), "bars": bars}))


def cmd_fundamentals(ticker):
    tk = yf.Ticker(ticker)
    info = tk.info

    if not info:
        print(json.dumps({"error": f"No data for {ticker}"}))
        return

    result = {
        "ticker": ticker.upper(),
        "name": info.get("shortName", ticker.upper()),
        "marketCap": safe_float(info.get("marketCap")),
        "pe": safe_float(info.get("trailingPE")),
        "forwardPe": safe_float(info.get("forwardPE")),
        "peg": safe_float(info.get("pegRatio")),
        "priceToBook": safe_float(info.get("priceToBook")),
        "evToEbitda": safe_float(info.get("enterpriseToEbitda")),
        "debtToEquity": safe_float(info.get("debtToEquity")),
        "returnOnEquity": safe_float(info.get("returnOnEquity")),
        "returnOnAssets": safe_float(info.get("returnOnAssets")),
        "operatingMargins": safe_float(info.get("operatingMargins")),
        "profitMargins": safe_float(info.get("profitMargins")),
        "beta": safe_float(info.get("beta")),
        "dividendYield": safe_float(info.get("dividendYield")),
        "eps": safe_float(info.get("trailingEps")),
        "sector": info.get("sector"),
        "industry": info.get("industry"),
        "description": info.get("longBusinessSummary"),
        "fiftyTwoWeekHigh": safe_float(info.get("fiftyTwoWeekHigh")),
        "fiftyTwoWeekLow": safe_float(info.get("fiftyTwoWeekLow")),
        "shortRatio": safe_float(info.get("shortRatio")),
        "institutionalOwnership": safe_float(info.get("heldPercentInstitutions")),
        "shortPercentOfFloat": safe_float(info.get("shortPercentOfFloat")),
        "revenueGrowth": safe_float(info.get("revenueGrowth")),
        "earningsGrowth": safe_float(info.get("earningsGrowth")),
        "currentRatio": safe_float(info.get("currentRatio")),
        "quickRatio": safe_float(info.get("quickRatio")),
    }
    print(json.dumps(result))


def cmd_news(ticker):
    sent_score, headlines = fetch_sentiment(ticker)
    result = {
        "ticker": ticker.upper(),
        "sentimentScore": round(sent_score, 4),
        "sentimentLabel": sentiment_label(sent_score),
        "headlines": headlines,
    }
    print(json.dumps(result))


def cmd_peers(ticker, period="1y"):
    tk = yf.Ticker(ticker)
    info = tk.info
    sector, peers = get_peers_for_ticker(ticker)

    if not sector:
        sector = info.get("sector", "Unknown")
        peers = [t for t in UNIVERSE.get(sector, []) if t != ticker.upper()]

    compare_list = [ticker.upper()] + peers[:6]
    period_map = {"1y": "1y", "5y": "5y"}
    yf_period = period_map.get(period, "1y")

    # Correlation via price data
    corr_dict = {}
    try:
        raw = yf.download(compare_list, period=yf_period, progress=False, auto_adjust=True)
        # Extract Close prices
        if isinstance(raw.columns, pd.MultiIndex):
            price_data = raw["Close"]
        else:
            price_data = raw[["Close"]] if "Close" in raw.columns else raw
        price_data = price_data.dropna(axis=1, how="all").ffill().bfill()
        daily_rets = price_data.pct_change().dropna()
        corr = daily_rets.corr().round(4)
        for k in corr.index:
            corr_dict[str(k)] = {}
            for v in corr.columns:
                val = corr.loc[k, v]
                if not np.isnan(float(val)):
                    corr_dict[str(k)][str(v)] = float(val)
    except Exception:
        pass

    # Peer fundamentals
    peer_data = []
    for t in compare_list[:6]:
        try:
            ti = yf.Ticker(t).info
            peer_data.append({
                "ticker": t,
                "name": ti.get("shortName"),
                "price": safe_float(ti.get("currentPrice") or ti.get("regularMarketPrice")),
                "pe": safe_float(ti.get("trailingPE")),
                "forwardPe": safe_float(ti.get("forwardPE")),
                "dividendYield": safe_float(ti.get("dividendYield")),
                "beta": safe_float(ti.get("beta")),
                "marketCap": safe_float(ti.get("marketCap")),
                "profitMargins": safe_float(ti.get("profitMargins")),
                "debtToEquity": safe_float(ti.get("debtToEquity")),
                "returnOnEquity": safe_float(ti.get("returnOnEquity")),
            })
        except Exception:
            peer_data.append({"ticker": t})

    result = {
        "ticker": ticker.upper(),
        "sector": sector or "Unknown",
        "peers": peer_data,
        "correlationMatrix": corr_dict,
    }
    print(json.dumps(result))


def cmd_analyst(ticker):
    tk = yf.Ticker(ticker)
    info = tk.info

    rec_key = info.get("recommendationKey", "N/A")
    if rec_key and rec_key != "N/A":
        rec_key = rec_key.replace("_", " ").title()

    recent_actions = []
    try:
        upgrades = tk.upgrades_downgrades
        if upgrades is not None and not upgrades.empty:
            upgrades = upgrades.reset_index()
            for _, row in upgrades.head(15).iterrows():
                date_val = str(row.get("GradeDate", "")) if "GradeDate" in row else None
                recent_actions.append({
                    "firm": str(row.get("Firm", "")),
                    "toGrade": str(row.get("ToGrade", "")) or None,
                    "fromGrade": str(row.get("FromGrade", "")) or None,
                    "date": date_val,
                    "action": str(row.get("Action", "reiterated")),
                })
    except Exception:
        pass

    result = {
        "ticker": ticker.upper(),
        "consensusRating": rec_key or "N/A",
        "numAnalysts": safe_float(info.get("numberOfAnalystOpinions")),
        "currentPrice": safe_float(info.get("currentPrice") or info.get("regularMarketPrice")),
        "targetLow": safe_float(info.get("targetLowPrice")),
        "targetMean": safe_float(info.get("targetMeanPrice")),
        "targetHigh": safe_float(info.get("targetHighPrice")),
        "recentActions": recent_actions,
    }
    print(json.dumps(result))


def cmd_universe():
    result = [{"sector": s, "tickers": t} for s, t in UNIVERSE.items()]
    print(json.dumps(result))


# ── Entry point ───────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command")

    p_overview = subparsers.add_parser("overview")
    p_overview.add_argument("ticker")

    p_history = subparsers.add_parser("history")
    p_history.add_argument("ticker")
    p_history.add_argument("--period", default="1y")

    p_fundamentals = subparsers.add_parser("fundamentals")
    p_fundamentals.add_argument("ticker")

    p_news = subparsers.add_parser("news")
    p_news.add_argument("ticker")

    p_peers = subparsers.add_parser("peers")
    p_peers.add_argument("ticker")
    p_peers.add_argument("--period", default="1y")

    p_analyst = subparsers.add_parser("analyst")
    p_analyst.add_argument("ticker")

    subparsers.add_parser("universe")

    args = parser.parse_args()

    try:
        if args.command == "overview":
            cmd_overview(args.ticker)
        elif args.command == "history":
            cmd_history(args.ticker, args.period)
        elif args.command == "fundamentals":
            cmd_fundamentals(args.ticker)
        elif args.command == "news":
            cmd_news(args.ticker)
        elif args.command == "peers":
            cmd_peers(args.ticker, args.period)
        elif args.command == "analyst":
            cmd_analyst(args.ticker)
        elif args.command == "universe":
            cmd_universe()
        else:
            print(json.dumps({"error": "Unknown command"}))
            sys.exit(1)
    except Exception as e:
        print(json.dumps({"error": str(e)}))
        sys.exit(1)


if __name__ == "__main__":
    main()
