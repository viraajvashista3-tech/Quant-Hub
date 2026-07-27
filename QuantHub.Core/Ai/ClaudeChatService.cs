using System.Runtime.CompilerServices;
using Anthropic;
using Anthropic.Models.Messages;

namespace QuantHub.Core.Ai;

/// <summary>Thin streaming wrapper over the official Anthropic SDK - replaces the original web app's
/// OpenAI GPT-4.1 streaming chat with Claude, keeping the same "system prompt + running history"
/// shape so the caller only needs to supply the stock-context system prompt and prior turns.</summary>
public sealed class ClaudeChatService
{
    public async IAsyncEnumerable<string> StreamReplyAsync(
        string apiKey,
        string systemPrompt,
        IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var client = new AnthropicClient { ApiKey = apiKey };

        var messages = history
            .Select(m => new MessageParam
            {
                Role = m.Role == ChatRole.User ? Role.User : Role.Assistant,
                Content = m.Text
            })
            .ToList();

        var parameters = new MessageCreateParams
        {
            Model = "claude-opus-5",
            MaxTokens = 2048,
            System = systemPrompt,
            Messages = messages
        };

        await foreach (var streamEvent in client.Messages.CreateStreaming(parameters, cancellationToken: ct))
        {
            if (streamEvent.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text))
            {
                yield return text.Text;
            }
        }
    }
}
