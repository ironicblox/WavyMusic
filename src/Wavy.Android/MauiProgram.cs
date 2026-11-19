using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Wavy.Android.Services;
using Wavy.Android.ViewModels;
using Wavy.Core.Abstractions;
using Wavy.Core.Services;

namespace Wavy.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("SegoeUI.ttf", "SegoeUI");
                fonts.AddFont("SegoeUI-Bold.ttf", "SegoeUIBold");
            });

        // Core services (platform-independent)
        builder.Services.AddSingleton<AndroidAppPaths>();
        builder.Services.AddSingleton<AndroidSettingsService>(sp =>
            new AndroidSettingsService(sp.GetRequiredService<AndroidAppPaths>().DataRoot));
        builder.Services.AddSingleton<AndroidCacheService>(sp =>
            new AndroidCacheService(sp.GetRequiredService<AndroidAppPaths>()));
        builder.Services.AddSingleton<AndroidPlaylistService>(sp =>
            new AndroidPlaylistService(sp.GetRequiredService<AndroidAppPaths>().DataRoot));
        builder.Services.AddSingleton<AndroidFavoritesService>(sp =>
            new AndroidFavoritesService(sp.GetRequiredService<AndroidAppPaths>().DataRoot));

        // yt-dlp service (pure HTTP fast path - no subprocess needed on Android)
        builder.Services.AddSingleton<IYtDlpService>(sp =>
        {
            var settings = sp.GetRequiredService<AndroidSettingsService>();
            var svc = new YtDlpService(new NoOpProcessRunner(), new NoOpToolLocator());
            svc.QualityProvider = () => settings.AudioQuality;
            return svc;
        });

        builder.Services.AddSingleton<ListeningHistoryService>(sp =>
            new ListeningHistoryService(sp.GetRequiredService<AndroidAppPaths>().DataRoot));
        builder.Services.AddSingleton<SpotifyImportService>(sp =>
            new SpotifyImportService(sp.GetRequiredService<IYtDlpService>()));
        builder.Services.AddSingleton<RadioFeedbackService>();

        // Android-specific playback
        builder.Services.AddSingleton<AndroidPlaybackService>();

        // ViewModels
        builder.Services.AddSingleton<PlayerAndroidViewModel>();
        builder.Services.AddSingleton<HomeAndroidViewModel>();
        builder.Services.AddSingleton<SearchAndroidViewModel>();
        builder.Services.AddSingleton<PlaylistsAndroidViewModel>();
        builder.Services.AddSingleton<LyricsAndroidViewModel>();
        builder.Services.AddSingleton<SettingsAndroidViewModel>();

        // Artist / Album detail ViewModels
        builder.Services.AddTransient<ArtistAndroidViewModel>();
        builder.Services.AddTransient<AlbumAndroidViewModel>();

        // Pages
        builder.Services.AddTransient<Views.HomePage>();
        builder.Services.AddTransient<Views.SearchPage>();
        builder.Services.AddTransient<Views.PlayerPage>();
        builder.Services.AddTransient<Views.PlaylistsPage>();
        builder.Services.AddTransient<Views.LyricsPage>();
        builder.Services.AddTransient<Views.SettingsPage>();
        builder.Services.AddTransient<Views.ArtistPage>();
        builder.Services.AddTransient<Views.AlbumPage>();

        return builder.Build();
    }
}
