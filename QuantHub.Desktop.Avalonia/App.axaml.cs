using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuantHub.Core.Ai;
using QuantHub.Core.MarketPulse;
using QuantHub.Core.Sentiment;
using QuantHub.Core.Services;
using QuantHub.Core.Yahoo;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.ViewModels;
using QuantHub.Desktop.ViewModels.Pages;
using QuantHub.Desktop.Views;

namespace QuantHub.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    // Dedicated singleton HttpClient with its own cookie container - the Yahoo crumb/cookie
                    // handshake depends on session persistence across calls, which IHttpClientFactory's
                    // pooled/recycled handlers don't guarantee.
                    services.AddSingleton(_ => new YahooFinanceClient(YahooFinanceClient.CreateDefaultHttpClient()));
                    services.AddSingleton(_ => new SentimentService(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }));
                    services.AddSingleton<StockAnalysisService>();
                    services.AddSingleton<MarketPulseService>();
                    services.AddSingleton<ClaudeChatService>();
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<AppState>();
                    services.AddSingleton<TerminalViewModel>();
                    services.AddSingleton<UniverseViewModel>();
                    services.AddSingleton<FundamentalsViewModel>();
                    services.AddSingleton<AnalystViewModel>();
                    services.AddSingleton<PeersViewModel>();
                    services.AddSingleton<InsiderViewModel>();
                    services.AddSingleton<MarketPulseViewModel>();
                    services.AddSingleton<AiResearchViewModel>();
                    services.AddSingleton<ComingSoonViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<ShellViewModel>();
                    services.AddSingleton<ShellWindow>();
                })
                .Build();

            _host.Start();
            _host.Services.GetRequiredService<SettingsService>().ApplyTheme();

            desktop.MainWindow = _host.Services.GetRequiredService<ShellWindow>();
            desktop.Exit += (_, _) =>
            {
                _host.StopAsync().GetAwaiter().GetResult();
                _host.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
