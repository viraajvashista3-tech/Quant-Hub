namespace QuantHub.Desktop.Messages;

/// <summary>Sent when a page (e.g. Universe, Peers) wants the shell to jump to the Terminal page
/// for a specific ticker, without holding a direct reference back to ShellViewModel.</summary>
public sealed record NavigateToTickerMessage(string Ticker);
