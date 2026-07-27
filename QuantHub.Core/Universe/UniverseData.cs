using QuantHub.Core.Models;

namespace QuantHub.Core.Universe;

/// <summary>Verbatim 11-sector/128-ticker hardcoded universe from stock_data.py lines 16-28.</summary>
public static class UniverseData
{
    public static readonly IReadOnlyList<(string Sector, string[] Tickers)> Sectors =
    [
        ("Basic Materials", ["BHP", "VALE", "FCX", "NEM", "LIN", "APD", "CTVA", "SHW", "ECL", "SCCO", "STLD", "NUE", "AA", "RIO"]),
        ("Energy", ["XOM", "CVX", "SHEL", "BP", "TTE", "COP", "EOG", "SLB", "PBR", "ENB", "MPC", "PSX", "VLO", "WDS"]),
        ("Technology", ["AAPL", "MSFT", "NVDA", "AVGO", "ORCL", "CRM", "AMD", "QCOM", "TXN", "NOW", "INTU", "IBM", "AMAT", "MU", "ADI"]),
        ("Financial Services", ["JPM", "BAC", "WFC", "MS", "GS", "HSBC", "RY", "TD", "C", "BLK", "BX", "UBS", "SAN", "AXP"]),
        ("Healthcare", ["LLY", "UNH", "JNJ", "ABBV", "MRK", "TMO", "PFE", "ABT", "AMGN", "DHR", "ISRG", "BMY", "GILD", "VRTX"]),
        ("Consumer Cyclical", ["AMZN", "TSLA", "HD", "NKE", "MCD", "LOW", "SBUX", "BKNG", "TJX", "TM", "MAR"]),
        ("Consumer Defensive", ["PG", "KO", "PEP", "COST", "WMT", "PM", "UL", "ABEV", "MO", "TGT", "DG", "KMB"]),
        ("Communication Services", ["GOOGL", "META", "NFLX", "DIS", "TMUS", "VZ", "T", "CMCSA", "CHTR", "AMX"]),
        ("Industrials", ["CAT", "HON", "GE", "UNP", "UPS", "LMT", "BA", "RTX", "DE", "MMM", "ADP", "CP", "ETN"]),
        ("Utilities", ["NEE", "DUK", "SO", "EXC", "AEP", "SRE", "D", "ED", "PEG", "PCG", "NGG"]),
        ("Real Estate", ["PLD", "AMT", "EQIX", "O", "CCI", "WY", "PSA", "DLR", "VICI", "CBRE"])
    ];

    public static IReadOnlyList<UniverseSector> AsSectors() =>
        Sectors.Select(s => new UniverseSector { Sector = s.Sector, Tickers = s.Tickers }).ToList();
}
