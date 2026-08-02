namespace QuantHub.Desktop.Messages;

/// <summary>Sent by UniverseViewModel after a watchlist load finds signal changes since the last
/// time it checked (SessionBriefingService.RecordAndDiff), for ShellViewModel to show as a
/// dismissible banner - kept as a message rather than a direct reference so Universe doesn't need
/// to know Shell exists, same reasoning as NavigateToTickerMessage.</summary>
public sealed record WatchlistBriefingMessage(IReadOnlyList<string> Changes);
