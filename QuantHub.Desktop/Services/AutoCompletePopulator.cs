using QuantHub.Core.Models;

namespace QuantHub.Desktop.Services;

/// <summary>Wraps a ticker-search delegate in a short debounce for use as an AutoCompleteBox
/// AsyncPopulator. Without this, every keystroke fired an immediate Yahoo search request, so the
/// suggestion dropdown would flicker/reorder as out-of-order responses landed while the user was
/// still typing - reported as "glitchy" autocomplete. AutoCompleteBox cancels the previous
/// CancellationToken on every text change, so the Task.Delay here is enough to skip the network
/// call entirely for keystrokes the user has already typed past.</summary>
internal static class AutoCompletePopulator
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(250);

    public static Func<string?, CancellationToken, Task<IEnumerable<object>>> Debounced(
        Func<string, CancellationToken, Task<IReadOnlyList<TickerSearchResult>>> search) =>
        async (text, ct) =>
        {
            try
            {
                await Task.Delay(Debounce, ct);
            }
            catch (OperationCanceledException)
            {
                return [];
            }

            return (await search(text ?? "", ct)).Cast<object>();
        };
}
