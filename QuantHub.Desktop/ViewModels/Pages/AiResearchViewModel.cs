using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuantHub.Core.Ai;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed partial class ChatMessageVm : ObservableObject
{
    public bool IsUser { get; init; }

    [ObservableProperty]
    private string _text = "";
}

/// <summary>AI Research chat page - streams Claude replies grounded in the active ticker's live
/// quant data (price/score/technicals), replacing the original web app's OpenAI GPT-4.1 chat with
/// the same "system prompt + stock context" shape. Requires a Claude API key configured in Settings.</summary>
public sealed partial class AiResearchViewModel : ObservableObject
{
    private readonly AppState _appState;
    private readonly StockAnalysisService _stockAnalysis;
    private readonly SettingsService _settings;
    private readonly ClaudeChatService _chat;

    public ObservableCollection<ChatMessageVm> Messages { get; } = [];

    public IReadOnlyList<string> SuggestedQuestions { get; } =
    [
        "What's the bull case for this stock?",
        "What's the bear case for this stock?",
        "How does the technical picture look right now?",
        "What are the biggest risks here?"
    ];

    [ObservableProperty]
    private string _userInput = "";

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_settings.ClaudeApiKey);

    public bool HasMessages => Messages.Count > 0;

    public AiResearchViewModel(AppState appState, StockAnalysisService stockAnalysis, SettingsService settings, ClaudeChatService chat)
    {
        _appState = appState;
        _stockAnalysis = stockAnalysis;
        _settings = settings;
        _chat = chat;

        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.ClaudeApiKey)) OnPropertyChanged(nameof(HasApiKey));
        };
        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMessages));
    }

    [RelayCommand]
    private void AskSuggested(string question)
    {
        UserInput = question;
        SendCommand.Execute(null);
    }

    private bool CanSend() => !IsSending && HasApiKey && !string.IsNullOrWhiteSpace(UserInput);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var question = UserInput.Trim();
        UserInput = "";
        ErrorMessage = null;

        Messages.Add(new ChatMessageVm { IsUser = true, Text = question });
        var reply = new ChatMessageVm { IsUser = false };
        Messages.Add(reply);

        IsSending = true;
        try
        {
            var overview = await _stockAnalysis.GetOverviewAsync(_appState.ActiveTicker);
            var systemPrompt = BuildSystemPrompt(overview);

            var history = Messages
                .Where(m => m != reply)
                .Select(m => new ChatMessage(m.IsUser ? ChatRole.User : ChatRole.Assistant, m.Text))
                .ToList();

            await foreach (var delta in _chat.StreamReplyAsync(_settings.ClaudeApiKey, systemPrompt, history))
            {
                reply.Text += delta;
            }

            if (string.IsNullOrEmpty(reply.Text)) reply.Text = "(No response received.)";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            Messages.Remove(reply);
        }
        finally
        {
            IsSending = false;
        }
    }

    partial void OnUserInputChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    partial void OnIsSendingChanged(bool value) => SendCommand.NotifyCanExecuteChanged();

    private static string BuildSystemPrompt(StockOverview? o)
    {
        var context = o is null
            ? "No specific stock is currently loaded in the terminal."
            : $"""
              Current stock context (live data from the terminal):
              - Ticker: {o.Ticker} ({o.Name})
              - Sector: {o.Sector ?? "Unknown"}
              - Price: {o.Price:0.00} ({o.ChangePercent:+0.00;-0.00}%)
              - Quant Score: {o.QuantScore:0.0} (range -100 to +100) / Signal: {o.Signal}
              - RSI(14): {o.Rsi:0.0}, MACD: {o.Macd:0.0000} vs Signal {o.MacdSignal:0.0000}
              - MA50: {o.Ma50?.ToString("0.00") ?? "N/A"}, MA200: {o.Ma200?.ToString("0.00") ?? "N/A"}
              - Beta: {o.Beta?.ToString("0.00") ?? "N/A"}, Annualized Volatility: {o.AnnualizedVolatility?.ToString("0.0") ?? "N/A"}%
              - Sharpe Ratio: {o.SharpeRatio?.ToString("0.000") ?? "N/A"}, Max Drawdown: {o.MaxDrawdown?.ToString("0.0") ?? "N/A"}%
              - Recent news sentiment score: {o.SentimentScore:0.0000} (-1 to +1 scale)
              """;

        return $"""
                You are an experienced equity research analyst embedded in a stock analysis terminal called Quant Terminal.

                {context}

                Ground substantive analysis in the data above where it's relevant to the question. For questions asking for
                an opinion or outlook, structure your answer around a bull case, a bear case, and a base case. Never tell the
                user to directly buy or sell - present balanced analysis and let them draw their own conclusions. Keep
                responses focused and readable, typically 300-600 words for substantive analysis, shorter for simple
                factual questions. End any substantive analysis with a brief reminder that this is not financial advice.
                """;
    }
}
