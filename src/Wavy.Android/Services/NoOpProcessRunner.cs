using System.Runtime.CompilerServices;
using Wavy.Core.Abstractions;
using Wavy.Core.Models;

namespace Wavy.Android.Services;

/// <summary>
/// No-op implementation of IProcessRunner for Android.
/// The yt-dlp subprocess cannot run on Android, so this is never actually called —
/// YtDlpService tries its pure-HTTP fast path first and only falls back to
/// IProcessRunner when that fails. If the fast path also fails we surface an error.
/// </summary>
public sealed class NoOpProcessRunner : IProcessRunner
{
    public Task<ProcessResult> RunAsync(string exePath, IReadOnlyList<string> args, CancellationToken ct = default)
        => Task.FromResult(new ProcessResult(false, string.Empty, "yt-dlp subprocess not available on Android.", 1));

    public async IAsyncEnumerable<string> StreamLinesAsync(string exePath, IReadOnlyList<string> args,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield break;
    }
}

/// <summary>No-op tool locator — Android never needs to find yt-dlp on disk.</summary>
public sealed class NoOpToolLocator : IToolLocator
{
    public Task<string> GetYtDlpPathAsync(CancellationToken ct = default)
        => Task.FromResult(string.Empty);
}
