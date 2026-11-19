using Wavy.Core.Abstractions;

namespace Wavy.Android.ViewModels;

public sealed partial class ArtistAndroidViewModel : ObservableObject
{
    private readonly IYtDlpService _catalogue;
    private readonly PlayerAndroidViewModel _player;

    [ObservableProperty] private string? _browseId;
    [ObservableProperty] private string  _artistName  = string.Empty;
    [ObservableProperty] private string? _thumbnailUrl;
    [ObservableProperty] private bool    _isLoading;
    [ObservableProperty] private string? _message;

    public ObservableCollection<Track> Tracks { get; } = new();

    public ArtistAndroidViewModel(IYtDlpService catalogue, PlayerAndroidViewModel player)
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
            var details = await _catalogue.GetArtistAsync(BrowseId!);
            if (details is null) { Message = "Artist not found."; return; }
            ArtistName   = details.Name ?? string.Empty;
            ThumbnailUrl = details.BannerUrl;
            foreach (var t in details.TopSongs) Tracks.Add(t);
        }
        catch (Exception ex) { Message = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task PlayTrackAsync(Track track) => await _player.PlayRadioAsync(track);
}
