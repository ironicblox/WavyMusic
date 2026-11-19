using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Wavy.Core.Models;

namespace Wavy.Android.Services;

public sealed class AndroidFavoritesService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;

    public AndroidFavoritesService(string dataRoot)
    {
        _path = Path.Combine(dataRoot, "favorites.json");
        foreach (var a in Load()) Albums.Add(a);
    }

    public ObservableCollection<AlbumResult> Albums { get; } = new();

    public bool IsFavorite(string browseId) =>
        Albums.Any(a => string.Equals(a.BrowseId, browseId, StringComparison.Ordinal));

    public void Toggle(AlbumResult album)
    {
        var existing = Albums.FirstOrDefault(a =>
            string.Equals(a.BrowseId, album.BrowseId, StringComparison.Ordinal));
        if (existing is not null) Albums.Remove(existing);
        else Albums.Insert(0, album);
        Save();
    }

    private List<AlbumResult> Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<List<AlbumResult>>(File.ReadAllText(_path), Json) ?? new()
                : new();
        }
        catch { return new(); }
    }

    private void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(Albums.ToList(), Json)); }
        catch { }
    }
}
