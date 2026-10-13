using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace SoundDrought;

/// <summary>
/// Shows the current track as "Listening to …" in Discord via its local IPC pipe.
/// Fails silently when Discord isn't running and reconnects on its own later.
/// </summary>
public sealed class DiscordPresence : IDisposable
{
    // Application ID of the "SoundDrought" app in the Discord Developer Portal.
    private const string ClientId = "1559626994843525213";

    private const int OpHandshake = 0, OpFrame = 1, OpClose = 2, OpPing = 3, OpPong = 4;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(20);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private NamedPipeClientStream? _pipe;
    private DateTime _nextConnect = DateTime.MinValue;
    private string? _sentKey;
    private long _sentStart;

    public static bool IsConfigured => ClientId.Length > 0;

    public void Update(PlayerState state, bool enabled) => _ = UpdateAsync(state, enabled);

    private async Task UpdateAsync(PlayerState state, bool enabled)
    {
        if (!IsConfigured) return;

        JsonObject? activity = null;
        string key = "";
        long start = 0;
        if (enabled && state.HasTrack && state.Playing)
        {
            key = $"{state.Url}|{state.Title}|{state.Artist}";
            start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)(state.Position * 1000);
            activity = BuildActivity(state, start);
        }

        await _lock.WaitAsync();
        try
        {
            // Position ticks every second; only resend on a new track, pause/resume or a seek.
            if (key == _sentKey && Math.Abs(start - _sentStart) < 3000) return;
            if (!await EnsureConnectedAsync()) return;

            var payload = new JsonObject
            {
                ["cmd"] = "SET_ACTIVITY",
                ["nonce"] = Guid.NewGuid().ToString(),
                ["args"] = new JsonObject { ["pid"] = Environment.ProcessId, ["activity"] = activity },
            };
            var json = payload.ToJsonString();
            await WriteFrameAsync(OpFrame, json);
            Log.Write("Discord <- " + json);
            _sentKey = key;
            _sentStart = start;
        }
        catch (Exception ex)
        {
            Log.Write("Discord update failed: " + ex);
            Disconnect();
        }
        finally
        {
            _lock.Release();
        }
    }

    private static JsonObject BuildActivity(PlayerState state, long start)
    {
        var activity = new JsonObject
        {
            ["type"] = 2,                // "Listening to"
            ["status_display_type"] = 2, // member list shows the track instead of the app name
            ["details"] = Clip(state.Title),
            ["state"] = Clip(state.Artist),
            ["assets"] = new JsonObject
            {
                ["large_image"] = state.Artwork.StartsWith("https://") ? state.Artwork : "logo",
                ["large_text"] = Clip(state.Title),
            },
        };
        if (state.Duration > 0)
        {
            activity["timestamps"] = new JsonObject
            {
                ["start"] = start,
                ["end"] = start + (long)(state.Duration * 1000),
            };
        }
        if (IsSoundCloudUrl(state.Url))
        {
            // Clicking the track title or the artwork opens the track.
            activity["details_url"] = state.Url;
            activity["assets"]!["large_url"] = state.Url;
            activity["buttons"] = new JsonArray(new JsonObject { ["label"] = L.ListenOnSoundCloud, ["url"] = state.Url });
        }
        if (IsSoundCloudUrl(state.ArtistUrl))
        {
            activity["state_url"] = state.ArtistUrl;
        }
        return activity;
    }

    private static bool IsSoundCloudUrl(string url) => url.StartsWith("https://soundcloud.com/");

    // Discord rejects fields shorter than 2 or longer than 128 characters.
    private static string Clip(string s) => s.Length < 2 ? s.PadRight(2) : s.Length > 128 ? s[..127] + "…" : s;

    private async Task<bool> EnsureConnectedAsync()
    {
        if (_pipe is { IsConnected: true }) return true;
        if (DateTime.UtcNow < _nextConnect) return false;
        _nextConnect = DateTime.UtcNow + RetryDelay;

        for (int i = 0; i < 10; i++)
        {
            var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(200);
                _pipe = pipe;
                var handshake = new JsonObject { ["v"] = 1, ["client_id"] = ClientId };
                await WriteFrameAsync(OpHandshake, handshake.ToJsonString());

                // Discord ignores activities sent before it has answered the handshake with READY.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var (op, body) = await ReadFrameAsync(pipe, timeout.Token);
                if (op != OpFrame || !body.Contains("\"READY\""))
                    throw new IOException($"Unexpected handshake reply (op {op}): {body}");

                Log.Write($"Discord connected on discord-ipc-{i}");
                _ = ReadLoopAsync(pipe);
                return true;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException)
            {
                if (ex is not TimeoutException) Log.Write($"Discord connect on discord-ipc-{i} failed: {ex.Message}");
                pipe.Dispose();
                _pipe = null;
            }
        }
        return false;
    }

    /// <summary>Drains Discord's replies (so the pipe never fills up) and answers pings.</summary>
    private async Task ReadLoopAsync(NamedPipeClientStream pipe)
    {
        try
        {
            while (pipe.IsConnected)
            {
                var (op, body) = await ReadFrameAsync(pipe, CancellationToken.None);
                if (op == OpPing)
                {
                    await _lock.WaitAsync();
                    try { await WriteFrameAsync(OpPong, body); }
                    finally { _lock.Release(); }
                }
                else if (op == OpClose || body.Contains("\"evt\":\"ERROR\""))
                {
                    Log.Write("Discord -> " + body);
                    if (op == OpClose) break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or EndOfStreamException)
        {
            Log.Write("Discord connection closed: " + ex.Message);
        }

        await _lock.WaitAsync();
        try
        {
            if (_pipe == pipe) Disconnect();
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<(int Op, string Body)> ReadFrameAsync(NamedPipeClientStream pipe, CancellationToken ct)
    {
        var header = new byte[8];
        await pipe.ReadExactlyAsync(header, ct);
        var body = new byte[BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4))];
        await pipe.ReadExactlyAsync(body, ct);
        return (BinaryPrimitives.ReadInt32LittleEndian(header), Encoding.UTF8.GetString(body));
    }

    private async Task WriteFrameAsync(int op, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var frame = new byte[8 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, op);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), body.Length);
        body.CopyTo(frame, 8);
        await _pipe!.WriteAsync(frame);
        await _pipe.FlushAsync();
    }

    private void Disconnect()
    {
        _pipe?.Dispose();
        _pipe = null;
        _sentKey = null; // resend the current state once reconnected
    }

    public void Dispose() => _pipe?.Dispose(); // Discord clears the status when the pipe closes
}
