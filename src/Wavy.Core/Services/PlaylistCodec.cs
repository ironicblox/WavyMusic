using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wavy.Core.Models;

namespace Wavy.Core.Services;

/// <summary>
/// Encodes a Wavy playlist to and from a compact, self-contained share code.
/// No server and no account are required: the code carries the playlist name and
/// every track's stable YouTube id plus enough metadata to render it, so a
/// recipient can import the playlist and resolve streams on demand at play time.
/// </summary>
public static class PlaylistCodec
{
    /// <summary>Human-readable scheme/version prefix so codes are recognizable and forward-compatible.</summary>
    public const string Scheme = "wavy1";

    private static readonly char[] TrimChars = { ' ', '\t', '\r', '\n' };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Builds a share code for the given playlist, or null if it has no tracks.
    /// </summary>
    public static string? Encode(Playlist playlist)
    {
        if (playlist is null)
            return null;

        var items = playlist.Tracks
            .Where(t => !string.IsNullOrWhiteSpace(t?.Id))
            .Select(t => new ShareItem
            {
                Id = t!.Id,
                Title = t.Title,
                Artist = t.Artist,
                Album = t.Album,
                ArtistBrowseId = t.ArtistBrowseId,
                DurationSeconds = t.Duration is { } d && d > TimeSpan.Zero ? d.TotalSeconds : null,
                ThumbnailUrl = t.ThumbnailUrl,
            })
            .ToList();

        if (items.Count == 0)
            return null;

        var envelope = new ShareEnvelope
        {
            V = 1,
            N = playlist.Name,
            T = items,
        };

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var payload = Base64UrlEncode(Gzip(json));
        return $"{Scheme}.{payload}";
    }

    /// <summary>
    /// Tries to decode a share code. Returns false (and a null share) for codes that
    /// are not Wavy codes or are corrupt, instead of throwing.
    /// </summary>
    public static bool TryDecode(string? code, out PlaylistShare? share)
    {
        share = null;
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var trimmed = code.Trim(TrimChars);
        var dot = trimmed.IndexOf('.');
        if (dot <= 0)
            return false;

        var scheme = trimmed[..dot];
        if (!string.Equals(scheme, Scheme, StringComparison.OrdinalIgnoreCase))
            return false;

        var payload = trimmed[(dot + 1)..];
        if (payload.Length == 0)
            return false;

        try
        {
            var json = Gunzip(Base64UrlDecode(payload));
            var envelope = JsonSerializer.Deserialize<ShareEnvelope>(json, JsonOptions);
            if (envelope?.T is null)
                return false;

            var tracks = new List<Track>();
            foreach (var item in envelope.T)
            {
                if (string.IsNullOrWhiteSpace(item?.Id))
                    continue;

                tracks.Add(new Track
                {
                    Id = item.Id!,
                    Title = string.IsNullOrWhiteSpace(item.Title) ? item.Id! : item.Title!,
                    Artist = item.Artist,
                    Album = item.Album,
                    ArtistBrowseId = item.ArtistBrowseId,
                    Duration = item.DurationSeconds is { } secs && secs > 0
                        ? TimeSpan.FromSeconds(secs)
                        : null,
                    ThumbnailUrl = item.ThumbnailUrl,
                });
            }

            if (tracks.Count == 0)
                return false;

            share = new PlaylistShare(
                string.IsNullOrWhiteSpace(envelope.N) ? "Imported playlist" : envelope.N!,
                tracks);
            return true;
        }
        catch
        {
            // Corrupt or truncated code: fail gracefully rather than throwing.
            return false;
        }
    }

    /// <summary>The decoded result of a share code: a playlist name plus the imported tracks in order.</summary>
    public sealed record PlaylistShare(string Name, IReadOnlyList<Track> Tracks);

    private static byte[] Gzip(string text)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8))
        {
            writer.Write(text);
        }
        return output.ToArray();
    }

    private static string Gunzip(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    private sealed class ShareEnvelope
    {
        public int V { get; set; }
        public string? N { get; set; }
        public List<ShareItem> T { get; set; } = new();
    }

    private sealed class ShareItem
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? ArtistBrowseId { get; set; }
        public double? DurationSeconds { get; set; }
        public string? ThumbnailUrl { get; set; }
    }
}
