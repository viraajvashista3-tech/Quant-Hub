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


def safe_int(val, default=None):
    if val is None:
        return default
    try:
        return int(val)
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

    # Bollinger Bands (20-day, 2σ)
    out["BB_MA20"] = close.rolling(20).mean()
    out["BB_STD20"] = close.rolling(20).std()
    out["BB_Upper"] = out["BB_MA20"] + 2 * out["BB_STD20"]
    out["BB_Lower"] = out["BB_MA20"] - 2 * out["BB_STD20"]

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

    # Sharpe ratio (risk-free rate ~4.5%)
    rf_daily = 0.045 / 252
    excess = daily_rets - rf_daily
    sharpe = float(excess.mean() / excess.std() * np.sqrt(252)) if excess.std() != 0 else 0.0

    # Max drawdown
    cumulative = (1 + daily_rets).cumprod()
    rolling_max = cumulative.cummax()
    drawdown = (cumulative - rolling_max) / rolling_max
    max_drawdown = float(drawdown.min() * 100)

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
        "sharpeRatio": round(sharpe, 3),
        "maxDrawdown": round(max_drawdown, 2),
        "trendScore": round(trend_score, 2),
        "momentumScore": round(momentum_score, 2),
        "sentimentContrib": round(sent_score * 40, 2),
        "volScore": round(vol_score, 2),
    }
    print(json.dumps(result))


def cmd_history(ticker, period="1y"):
    period_map = {"ytd": "ytd", "6mo": "6mo", "1y": "1y", "2y": "2y", "5y": "5y"}
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
            "bbUpper": sv("BB_Upper"),
            "bbLower": sv("BB_Lower"),
            "bbMa20": sv("BB_MA20"),
        })

    print(json.dumps({"ticker": ticker.upper(), "bars": bars}))


def cmd_fundamentals(ticker):
    tk = yf.Ticker(ticker)
    info = tk.info

    if not info:
        print(json.dumps({"error": f"No data for {ticker}"}))
        return

    eps = safe_float(info.get("trailingEps"))
    bvps = safe_float(info.get("bookValue"))
    graham_number = None
    if eps and bvps and eps > 0 and bvps > 0:
        graham_number = round(float(np.sqrt(22.5 * eps * bvps)), 2)

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
        "eps": eps,
        "bookValuePerShare": bvps,
        "grahamNumber": graham_number,
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
        "totalRevenue": safe_float(info.get("totalRevenue")),
        "freeCashflow": safe_float(info.get("freeCashflow")),
        "totalDebt": safe_float(info.get("totalDebt")),
        "totalCash": safe_float(info.get("totalCash")),
        "sharesOutstanding": safe_float(info.get("sharesOutstanding")),
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


def generate_peers_summary(ticker, peer_data, sector):
    """Generate a plain-English paragraph comparing the ticker to its sector peers."""
    try:
        subject = next((p for p in peer_data if p["ticker"] == ticker.upper()), None)
        if not subject:
            return None
        peers_only = [p for p in peer_data if p["ticker"] != ticker.upper()]

        def peer_median(key):
            vals = [p[key] for p in peers_only if p.get(key) is not None]
            return float(np.median(vals)) if vals else None

        name = subject.get("name") or ticker.upper()
        pe = subject.get("pe")
        med_pe = peer_median("pe")
        margins = subject.get("profitMargins")
        med_margins = peer_median("profitMargins")
        beta = subject.get("beta")
        med_beta = peer_median("beta")
        roe = subject.get("returnOnEquity")
        med_roe = peer_median("returnOnEquity")
        de = subject.get("debtToEquity")
        med_de = peer_median("debtToEquity")

        parts = []

        if pe is not None and med_pe is not None:
            diff_pct = ((pe - med_pe) / med_pe) * 100 if med_pe else 0
            if abs(diff_pct) > 10:
                direction = "commands a premium" if diff_pct > 0 else "trades at a discount"
                parts.append(
                    f"{name} {direction} valuation vs its {sector} peers "
                    f"(P/E {pe:.1f}x vs sector median {med_pe:.1f}x)"
                )
            else:
                parts.append(f"{name} trades in line with {sector} peers on valuation (P/E {pe:.1f}x)")

        if margins is not None and med_margins is not None:
            if margins > med_margins * 1.1:
                parts.append(
                    f"it leads the group on profitability with {margins*100:.1f}% net margins "
                    f"(sector median {med_margins*100:.1f}%)"
                )
            elif margins < med_margins * 0.9:
                parts.append(
                    f"its {margins*100:.1f}% net margins trail the sector median of {med_margins*100:.1f}%"
                )
            else:
                parts.append(f"profit margins are in line with the sector at {margins*100:.1f}%")

        if beta is not None and med_beta is not None:
            if beta > med_beta * 1.15:
                parts.append(
                    f"the stock carries above-average market risk (beta {beta:.2f} vs sector {med_beta:.2f})"
                )
            elif beta < med_beta * 0.85:
                parts.append(
                    f"it is less volatile than its peers (beta {beta:.2f} vs sector {med_beta:.2f})"
                )

        if roe is not None and med_roe is not None:
            if roe > med_roe * 1.1:
                parts.append(
                    f"and generates stronger returns on equity ({roe*100:.1f}% vs sector {med_roe*100:.1f}%)"
                )
            elif roe < med_roe * 0.9:
                parts.append(
                    f"though return on equity lags peers ({roe*100:.1f}% vs {med_roe*100:.1f}%)"
                )

        if de is not None and med_de is not None:
            if de > med_de * 1.3:
                parts.append(
                    f"Debt levels are elevated relative to peers (D/E {de:.1f} vs {med_de:.1f})"
                )
            elif de < med_de * 0.7:
                parts.append(
                    f"The balance sheet is relatively clean with lower debt than peers (D/E {de:.1f} vs {med_de:.1f})"
                )

        if not parts:
            return f"{name} shows broadly similar characteristics to its {sector} sector peers."

        summary = ". ".join(p.capitalize() for p in parts) + "."
        return summary
    except Exception:
        return None


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

    corr_dict = {}
    try:
        raw = yf.download(compare_list, period=yf_period, progress=False, auto_adjust=True)
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

    peer_data = []
    for t in compare_list[:7]:
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

    summary = generate_peers_summary(ticker, peer_data, sector or "sector")

    result = {
        "ticker": ticker.upper(),
        "sector": sector or "Unknown",
        "summary": summary,
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
            for _, row in upgrades.head(40).iterrows():
                date_val = None
                if "GradeDate" in row and row["GradeDate"] is not None:
                    try:
                        date_val = str(pd.Timestamp(row["GradeDate"]).date())
                    except Exception:
                        date_val = str(row["GradeDate"])

                current_target = safe_float(row.get("currentPriceTarget"))
                prior_target = safe_float(row.get("priorPriceTarget"))
                target_action = str(row.get("priceTargetAction", "")) or None

                recent_actions.append({
                    "firm": str(row.get("Firm", "")),
                    "toGrade": str(row.get("ToGrade", "")) or None,
                    "fromGrade": str(row.get("FromGrade", "")) or None,
                    "date": date_val,
                    "action": str(row.get("Action", "reiterated")),
                    "priceTargetAction": target_action,
                    "currentPriceTarget": current_target,
                    "priorPriceTarget": prior_target,
                })
    except Exception:
        pass

    # Recommendation trend (last 4 months)
    rec_trend = []
    try:
        rs = tk.recommendations_summary
        if rs is not None and not rs.empty:
            for _, row in rs.iterrows():
                period_label = str(row.get("period", ""))
                if period_label.startswith("-"):
                    months_ago = int(period_label.replace("m", ""))
                    label = f"{abs(months_ago)}mo ago" if months_ago != 0 else "Current"
                else:
                    label = period_label
                rec_trend.append({
                    "period": label,
                    "strongBuy": safe_int(row.get("strongBuy"), 0),
                    "buy": safe_int(row.get("buy"), 0),
                    "hold": safe_int(row.get("hold"), 0),
                    "sell": safe_int(row.get("sell"), 0),
                    "strongSell": safe_int(row.get("strongSell"), 0),
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
        "recommendationTrend": rec_trend,
    }
    print(json.dumps(result))


def cmd_insider(ticker):
    tk = yf.Ticker(ticker)
    info = tk.info

    transactions = []
    try:
        it = tk.insider_transactions
        if it is not None and not it.empty:
            it = it.reset_index() if "Start Date" not in it.columns else it
            for _, row in it.head(50).iterrows():
                text = str(row.get("Text", ""))
                transaction_type = "Unknown"
                text_lower = text.lower()
                if "sale" in text_lower or "sell" in text_lower:
                    transaction_type = "Sale"
                elif "purchase" in text_lower or "buy" in text_lower or "bought" in text_lower:
                    transaction_type = "Purchase"
                elif "gift" in text_lower or "donated" in text_lower:
                    transaction_type = "Gift"
                elif "option" in text_lower or "exercise" in text_lower:
                    transaction_type = "Option Exercise"
                elif "award" in text_lower or "grant" in text_lower:
                    transaction_type = "Award/Grant"

                date_val = None
                try:
                    sd = row.get("Start Date")
                    if sd is not None:
                        date_val = str(pd.Timestamp(sd).date())
                except Exception:
                    pass

                shares = safe_int(row.get("Shares"))
                value = safe_float(row.get("Value"))

                transactions.append({
                    "insider": str(row.get("Insider", "")),
                    "position": str(row.get("Position", "")),
                    "transactionType": transaction_type,
                    "shares": shares,
                    "value": value,
                    "text": text,
                    "date": date_val,
                    "ownership": str(row.get("Ownership", "D")),
                })
    except Exception:
        pass

    # Insider purchase summary (last 6 months)
    purchases_6m = {"purchaseShares": None, "purchaseTrans": None,
                    "saleShares": None, "saleTrans": None}
    try:
        ip = tk.insider_purchases
        if ip is not None and not ip.empty:
            for _, row in ip.iterrows():
                label = str(row.get("Insider Purchases Last 6m", "")).lower()
                if "purchase" in label:
                    purchases_6m["purchaseShares"] = safe_int(row.get("Shares"))
                    purchases_6m["purchaseTrans"] = safe_int(row.get("Trans"))
                elif "sale" in label or "sell" in label:
                    purchases_6m["saleShares"] = safe_int(row.get("Shares"))
                    purchases_6m["saleTrans"] = safe_int(row.get("Trans"))
    except Exception:
        pass

    # Ownership breakdown
    insider_pct = safe_float(info.get("heldPercentInsiders"))
    institution_pct = safe_float(info.get("heldPercentInstitutions"))

    # Net sentiment
    buys = sum(1 for t in transactions if t["transactionType"] == "Purchase")
    sells = sum(1 for t in transactions if t["transactionType"] == "Sale")
    net_sentiment = "Net Buyers" if buys > sells else "Net Sellers" if sells > buys else "Neutral"

    result = {
        "ticker": ticker.upper(),
        "name": info.get("shortName", ticker.upper()),
        "insiderOwnership": insider_pct,
        "institutionalOwnership": institution_pct,
        "netSentiment": net_sentiment,
        "buyCount": buys,
        "sellCount": sells,
        "purchases6m": purchases_6m,
        "transactions": transactions,
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

    p_insider = subparsers.add_parser("insider")
    p_insider.add_argument("ticker")

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
        elif args.command == "insider":
            cmd_insider(args.ticker)
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
