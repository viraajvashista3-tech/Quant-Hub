using System.ServiceModel.Syndication;
using System.Xml;
using VaderSharp2;

namespace QuantHub.Core.Sentiment;

public sealed class Headline
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? PublishedAt { get; init; }
    public double Sentiment { get; init; }
}

public sealed record SentimentResult(double AverageScore, IReadOnlyList<Headline> Headlines);

/// <summary>
/// Ports fetch_sentiment/sentiment_label from stock_data.py: Google News RSS headlines scored
/// with VADER's compound score. Only the first 12 entries are scored; any network/parse failure
/// is swallowed and yields a neutral (0.0, empty) result, matching the Python original's silent
/// failure mode exactly.
/// </summary>
public sealed class SentimentService(HttpClient httpClient)
{
    private readonly SentimentIntensityAnalyzer _analyzer = new();

    public async Task<SentimentResult> FetchSentimentAsync(string ticker, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://news.google.com/rss/search?q={Uri.EscapeDataString(ticker)}+stock+news";
            await using var stream = await httpClient.GetStreamAsync(url, ct);
            using var reader = XmlReader.Create(stream);
            var feed = SyndicationFeed.Load(reader);

            var headlines = new List<Headline>();
            var scores = new List<double>();
            foreach (var item in feed.Items.Take(12))
            {
                var title = item.Title?.Text ?? "";
                var link = item.Links.FirstOrDefault()?.Uri.ToString() ?? "";
                var published = item.PublishDate == default ? null : item.PublishDate.UtcDateTime.ToString("R");
                var score = _analyzer.PolarityScores(title).Compound;
                scores.Add(score);
                headlines.Add(new Headline
                {
                    Title = title,
                    Url = link,
                    PublishedAt = published,
                    Sentiment = Math.Round(score, 4)
                });
            }

            var avg = scores.Count > 0 ? scores.Average() : 0.0;
            return new SentimentResult(avg, headlines);
        }
        catch
        {
            return new SentimentResult(0.0, []);
        }
    }

    public static string SentimentLabel(double score) => score switch
    {
        >= 0.3 => "Bullish",
        >= 0.05 => "Mildly Bullish",
        <= -0.3 => "Bearish",
        <= -0.05 => "Mildly Bearish",
        _ => "Neutral"
    };
}
