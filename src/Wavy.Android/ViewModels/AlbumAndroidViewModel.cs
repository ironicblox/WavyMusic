using Wavy.Core.Abstractions;

namespace Wavy.Android.ViewModels;

public sealed partial class AlbumAndroidViewModel : ObservableObject
{
    private readonly IYtDlpService _catalogue;
    private readonly PlayerAndroidViewModel _player;

    [ObservableProperty] private string? _browseId;
    [ObservableProperty] private string  _albumTitle  = string.Empty;
    [ObservableProperty] private string  _artistName  = string.Empty;
    [ObservableProperty] private string? _thumbnailUrl;
    [ObservableProperty] private bool    _isLoading;
    [ObservableProperty] private string? _message;

    public ObservableCollection<Track> Tracks { get; } = new();

    public AlbumAndroidViewModel(IYtDlpService catalogue, PlayerAndroidViewModel player)
    {
        _catalogue = catalogue;
        _player    = player;
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(BrowseId)) return;
        IsLoading = true;
        Message   = null;
        Tracks.Clear();
        try
        {
            var details = await _catalogue.GetAlbumAsync(BrowseId!);
            if (details is null) { Message = "Album not found."; return; }
            AlbumTitle   = details.Title  ?? string.Empty;
            ArtistName   = details.Artist ?? string.Empty;
            ThumbnailUrl = details.ThumbnailUrl;
            foreach (var t in details.Tracks) Tracks.Add(t);
        }
        catch (Exception ex) { Message = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task PlayAlbumAsync()
    {
        if (Tracks.Count > 0) await _player.PlayFromAsync(Tracks.ToList(), 0);
    }

    [RelayCommand]
    private async Task PlayTrackAsync(Track t)
    {
        var i = Tracks.IndexOf(t);
        await _player.PlayFromAsync(Tracks.ToList(), i < 0 ? 0 : i);
    }
}
