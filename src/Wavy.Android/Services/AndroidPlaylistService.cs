using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Wavy.Core.Models;
using Wavy.Core.Services;

namespace Wavy.Android.Services;

/// <summary>
/// Android port of the Windows PlaylistService.
/// Identical JSON format — playlists.json is fully cross-platform compatible.
/// Includes the new share/import by code feature via PlaylistCodec.
/// </summary>
public sealed class AndroidPlaylistService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private bool _loaded;

    public AndroidPlaylistService(string dataRoot)
    {
        _path = Path.Combine(dataRoot, "playlists.json");
        foreach (var p in Load()) Playlists.Add(p);
        _loaded = true;
        Playlists.CollectionChanged += (_, _) => Save();
    }

    public ObservableCollection<Playlist> Playlists { get; } = new();
    public event EventHandler<Playlist>? PlaylistChanged;

    public Playlist Create(string name)
    {
        var p = new Playlist { Id = Guid.NewGuid().ToString("n"), Name = Clean(name) };
        Playlists.Insert(0, p);
        Save();
        return p;
    }

    public void Rename(Playlist playlist, string name)
    {
        playlist.Name = Clean(name);
        playlist.NotifyTracksChanged();
        Save();
        PlaylistChanged?.Invoke(this, playlist);
    }

    public void Delete(Playlist playlist)
    {
        if (Playlists.Remove(playlist)) Save();
    }

    public bool TryAdd(Playlist playlist, Track track)
    {
        if (playlist.Tracks.Any(t => t.Id == track.Id)) return false;
        playlist.Tracks.Add(track);
        playlist.NotifyTracksChanged();
        Save();
        PlaylistChanged?.Invoke(this, playlist);
        return true;
    }

    public void Remove(Playlist playlist, Track track)
    {
        if (playlist.Tracks.RemoveAll(t => t.Id == track.Id) == 0) return;
        playlist.NotifyTracksChanged();
        Save();
        PlaylistChanged?.Invoke(this, playlist);
    }

    public void SetTracks(Playlist playlist, IEnumerable<Track> tracks)
    {
        var ordered = tracks.ToList();
        if (playlist.Tracks.Select(t => t.Id).SequenceEqual(ordered.Select(t => t.Id))) return;
        playlist.Tracks.Clear();
        playlist.Tracks.AddRange(ordered);
        playlist.NotifyTracksChanged();
        Save();
        PlaylistChanged?.Invoke(this, playlist);
    }

    // ── Share / Import by code (new feature) ─────────────────────────────────

    /// <summary>Returns a self-contained share code for the playlist, or null if it has no tracks.</summary>
    public string? GetShareCode(Playlist playlist) => PlaylistCodec.Encode(playlist);

    /// <summary>Imports a playlist from a share code. Returns the new playlist, or null if the code is invalid.</summary>
    public Playlist? ImportFromCode(string code)
    {
        if (!PlaylistCodec.TryDecode(code, out var share) || share is null) return null;
        var playlist = Create(share.Name);
        SetTracks(playlist, share.Tracks);
        return playlist;
    }

    public string SuggestName()
    {
        const string baseName = "New playlist";
        if (Playlists.All(p => p.Name != baseName)) return baseName;
        for (var n = 2; ; n++)
        {
            var candidate = $"{baseName} {n}";
            if (Playlists.All(p => p.Name != candidate)) return candidate;
        }
    }

    private static string Clean(string name) =>
        string.IsNullOrWhiteSpace(name) ? "New playlist" : name.Trim();

    private List<Playlist> Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<List<Playlist>>(File.ReadAllText(_path), JsonOptions) ?? new()
                : new();
        }
        catch { return new(); }
    }

    private void Save()
    {
        if (!_loaded) return;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(Playlists.ToList(), JsonOptions)); }
        catch { }
    }
}
