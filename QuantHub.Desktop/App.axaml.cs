using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuantHub.Core.Backtesting;
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
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<ScoreWeightsService>();
                    services.AddSingleton<BacktestEngine>();
                    services.AddSingleton<AutoBacktestService>();
                    services.AddSingleton<PredictionLogService>();
                    services.AddSingleton<WatchlistService>();
                    services.AddSingleton<PortfolioService>();
                    services.AddSingleton<UniverseRankingService>();
                    services.AddSingleton<SessionBriefingService>();
                    services.AddSingleton(_ => new UpdateCheckService(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }));
                    services.AddSingleton<AppState>();
                    services.AddSingleton<TerminalViewModel>();
                    services.AddSingleton<StockWorkspaceViewModel>();
                    services.AddSingleton<UniverseViewModel>();
                    services.AddSingleton<FundamentalsViewModel>();
                    services.AddSingleton<AnalystViewModel>();
                    services.AddSingleton<PeersViewModel>();
                    services.AddSingleton<PortfolioViewModel>();
                    services.AddSingleton<InsiderViewModel>();
                    services.AddSingleton<MarketPulseViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<TrackRecordViewModel>();
                    services.AddSingleton<ShellViewModel>();
                    services.AddSingleton<ShellWindow>();
                })
                .Build();

            _host.Start();
            _host.Services.GetRequiredService<SettingsService>().ApplyTheme();

            // Fire-and-forget: recalibrates Quant Score weights against the last week's price
            // history if it's been at least a week since the last check. No-ops (near-instantly)
            // if not due yet, so this never delays startup.
            _host.Services.GetRequiredService<AutoBacktestService>().RunInBackgroundIfDue();

            // Fire-and-forget: scores any live predictions logged 14+ days ago against what SPY and
            // the ticker actually did since. No-ops (near-instantly) if nothing is due yet.
            _host.Services.GetRequiredService<PredictionLogService>().EvaluateMaturedInBackground();

            // Fire-and-forget: re-sweeps the full universe for the Universe page's Top 20 rankings if
            // the cached sweep is more than ~20 hours old. No-ops (near-instantly) if not due yet.
            _host.Services.GetRequiredService<UniverseRankingService>().RunInBackgroundIfDue();

            // Fire-and-forget: checks GitHub Releases for a newer version once a day, surfaced as a
            // quiet banner on the Settings page - never an interrupting popup.
            _host.Services.GetRequiredService<UpdateCheckService>().RunInBackgroundIfDue();

            var window = _host.Services.GetRequiredService<ShellWindow>();
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                _host.StopAsync().GetAwaiter().GetResult();
                _host.Dispose();
            };

#if DEBUG
            // Dev-only screenshot hook (F12): dumps a ground-truth RenderTargetBitmap render to
            // disk - PrintWindow/CopyFromScreen can both misrender Avalonia's Skia surface, so this
            // is the reliable way to verify layout during migration. Strip before shipping.
            window.KeyDown += (_, e) =>
            {
                if (e.Key != Key.F12) return;
                var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
                using var bmp = new RenderTargetBitmap(size);
                bmp.Render(window);
                bmp.Save(Path.Combine(Path.GetTempPath(), "quantterminal_screenshot.png"));
            };
#endif
        }

        base.OnFrameworkInitializationCompleted();
    }
}
