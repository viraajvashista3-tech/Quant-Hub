namespace QuantHub.Core.Models;

public sealed class NewsItem
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? PublishedAt { get; init; }
    public double? Sentiment { get; init; }
}

public sealed class NewsData
{
    public required string Ticker { get; init; }
    public double SentimentScore { get; init; }
    public string? SentimentLabel { get; init; }
    public required IReadOnlyList<NewsItem> Headlines { get; init; }
}
