using System.Text.Json;
using QuantHub.Core.Models;
using QuantHub.Core.Yahoo;

namespace QuantHub.Core.Analysis;

/// <summary>Ports the fundamentals command (stock_data.py lines 298-349), including the Graham
/// Number guard clause (both EPS and BVPS must be present and strictly positive).</summary>
public static class FundamentalsAnalyzer
{
    public static readonly string[] Modules =
        ["summaryDetail", "price", "defaultKeyStatistics", "financialData", "assetProfile"];

    public static FundamentalsData Build(string ticker, JsonElement result)
    {
        double? Raw(string field) => YahooJson.RawAny(result, Modules, field);
        string? Str(string field) => YahooJson.StrAny(result, Modules, field);

        var name = Str("shortName") ?? Str("longName") ?? ticker.ToUpperInvariant();
        var eps = Raw("trailingEps");
        var bvps = Raw("bookValue");
        double? graham = eps is { } e && bvps is { } b && e > 0 && b > 0
            ? Math.Round(Math.Sqrt(22.5 * e * b), 2)
            : null;

        return new FundamentalsData
        {
            Ticker = ticker.ToUpperInvariant(),
            Name = name,
            MarketCap = Raw("marketCap"),
            Pe = Raw("trailingPE"),
            ForwardPe = Raw("forwardPE"),
            Peg = Raw("pegRatio"),
            PriceToBook = Raw("priceToBook"),
            EvToEbitda = Raw("enterpriseToEbitda"),
            DebtToEquity = Raw("debtToEquity"),
            ReturnOnEquity = Raw("returnOnEquity"),
            ReturnOnAssets = Raw("returnOnAssets"),
            OperatingMargins = Raw("operatingMargins"),
            ProfitMargins = Raw("profitMargins"),
            Beta = Raw("beta"),
            DividendYield = Raw("dividendYield"),
            DividendRate = Raw("dividendRate"),
            PayoutRatio = Raw("payoutRatio"),
            Eps = eps,
            BookValuePerShare = bvps,
            GrahamNumber = graham,
            Sector = Str("sector"),
            Industry = Str("industry"),
            Description = Str("longBusinessSummary"),
            FiftyTwoWeekHigh = Raw("fiftyTwoWeekHigh"),
            FiftyTwoWeekLow = Raw("fiftyTwoWeekLow"),
            ShortRatio = Raw("shortRatio"),
            InstitutionalOwnership = Raw("heldPercentInstitutions"),
            ShortPercentOfFloat = Raw("shortPercentOfFloat"),
            RevenueGrowth = Raw("revenueGrowth"),
            EarningsGrowth = Raw("earningsGrowth"),
            CurrentRatio = Raw("currentRatio"),
            QuickRatio = Raw("quickRatio"),
            TotalRevenue = Raw("totalRevenue"),
            FreeCashflow = Raw("freeCashflow"),
            TotalDebt = Raw("totalDebt"),
            TotalCash = Raw("totalCash"),
            SharesOutstanding = Raw("sharesOutstanding")
        };
    }
}
