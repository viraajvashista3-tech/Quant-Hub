namespace QuantHub.Core.Ai;

public enum ChatRole
{
    User,
    Assistant
}

public sealed record ChatMessage(ChatRole Role, string Text);
