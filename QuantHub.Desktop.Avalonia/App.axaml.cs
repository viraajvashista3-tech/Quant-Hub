using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
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
