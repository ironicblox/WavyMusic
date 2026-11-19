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
            .ConfigureFonts(fonts =>
            {
                // No custom fonts — use system default to avoid missing-file crash
            });

        // ── Platform paths ───────────────────────────────────────────────────
        builder.Services.AddSingleton<AndroidAppPaths>();

        // ── Settings / Cache / Playlists / Favorites ─────────────────────────
        builder.Services.AddSingleton<AndroidSettingsService>(sp =>
            new AndroidSettingsService(sp.GetRequiredService<AndroidAppPaths>().DataRoot));

        builder.Services.AddSingleton<AndroidCacheService>(sp =>
            new AndroidCacheService(sp.GetRequiredService<AndroidAppPaths>()));

        builder.Services.AddSingleton<AndroidPlaylistService>(sp =>
            new AndroidPlaylistService(sp.GetRequiredService<AndroidAppPaths>().DataRoot));

        builder.Services.AddSingleton<AndroidFavoritesService>(sp =>
            new AndroidFavoritesService(sp.GetRequiredService<AndroidAppPaths>().DataRoot));

        // ── Listening history (needs a file path, not a folder) ───────────────
        builder.Services.AddSingleton<ListeningHistoryService>(sp =>
        {
            var root = sp.GetRequiredService<AndroidAppPaths>().DataRoot;
            return new ListeningHistoryService(System.IO.Path.Combine(root, "history.json"));
        });

        // ── YouTube / catalogue service (pure HTTP, no yt-dlp binary) ─────────
        builder.Services.AddSingleton<IYtDlpService>(sp =>
        {
            var settings = sp.GetRequiredService<AndroidSettingsService>();
            var svc = new YtDlpService(new NoOpProcessRunner(), new NoOpToolLocator());
            svc.QualityProvider = () => settings.AudioQuality;
            return svc;
        });

        builder.Services.AddSingleton<SpotifyImportService>(sp =>
            new SpotifyImportService(sp.GetRequiredService<IYtDlpService>()));

        builder.Services.AddSingleton<RadioFeedbackService>();

        // ── Playback ──────────────────────────────────────────────────────────
        builder.Services.AddSingleton<AndroidPlaybackService>();

        // ── ViewModels ────────────────────────────────────────────────────────
        builder.Services.AddSingleton<PlayerAndroidViewModel>();
        builder.Services.AddSingleton<HomeAndroidViewModel>();
        builder.Services.AddSingleton<SearchAndroidViewModel>();
        builder.Services.AddSingleton<PlaylistsAndroidViewModel>();
        builder.Services.AddSingleton<LyricsAndroidViewModel>();
        builder.Services.AddSingleton<SettingsAndroidViewModel>();
        builder.Services.AddTransient<ArtistAndroidViewModel>();
        builder.Services.AddTransient<AlbumAndroidViewModel>();

        // ── Pages ─────────────────────────────────────────────────────────────
        builder.Services.AddTransient<Views.HomePage>();
        builder.Services.AddTransient<Views.SearchPage>();
        builder.Services.AddTransient<Views.PlayerPage>();
        builder.Services.AddTransient<Views.PlaylistsPage>();
        builder.Services.AddTransient<Views.LyricsPage>();
        builder.Services.AddTransient<Views.SettingsPage>();
        builder.Services.AddTransient<Views.ArtistPage>();
        builder.Services.AddTransient<Views.AlbumPage>();

        var app = builder.Build();

        // Log unhandled exceptions so crashes show a message instead of silent close
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"[Wavy] CRASH: {e.ExceptionObject}");

        return app;
    }
}
