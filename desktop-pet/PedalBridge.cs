using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace WobblyPet;

enum PedalContact { Unknown, Off, On }

readonly record struct PedalFoot(PedalContact Contact, double X, double Y, uint Stomps)
{
    public char Bit => Contact switch { PedalContact.On => '1', PedalContact.Off => '0', _ => '?' };
}

sealed record PedalFrame(long Session, long Received, bool HasMotion, PedalFoot Left, PedalFoot Right)
{
    public bool Ready => Left.Contact != PedalContact.Unknown && Right.Contact != PedalContact.Unknown;

    public static PedalFrame? Parse(string message, long session)
    {
        if (message.Length == 2 && TryContact(message[0], out var left) && TryContact(message[1], out var right))
            return new(session, Stopwatch.GetTimestamp(), false, new(left, 0, 0, 0), new(right, 0, 0, 0));

        var fields = message.Split(',');
        return fields.Length == 9 && fields[0] == "M1" && TryFoot(1, out var l) && TryFoot(5, out var r)
            ? new(session, Stopwatch.GetTimestamp(), true, l, r) : null;

        bool TryFoot(int index, out PedalFoot foot)
        {
            foot = default;
            if (fields[index].Length != 1 || !TryContact(fields[index][0], out var contact) ||
                !TryAxis(fields[index + 1], out var x) || !TryAxis(fields[index + 2], out var y) ||
                !uint.TryParse(fields[index + 3], NumberStyles.None, CultureInfo.InvariantCulture, out var stomps)) return false;
            foot = new(contact, contact == PedalContact.On ? x : 0, contact == PedalContact.On ? y : 0, stomps);
            return true;
        }
    }

    static bool TryAxis(string value, out double axis) =>
        double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out axis) && axis is >= -1 and <= 1;

    static bool TryContact(char bit, out PedalContact contact)
    {
        contact = bit switch { '1' => PedalContact.On, '0' => PedalContact.Off, _ => PedalContact.Unknown };
        return bit is '0' or '1' or '?';
    }
}

sealed class PedalBridge : IDisposable
{
    public const string SavedCalibrationOrigin = "http://127.0.0.1:5178";
    readonly string origin;
    readonly string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    readonly HttpListener listener = new();
    readonly CancellationTokenSource shutdown = new();
    readonly Dictionary<string, (string Type, byte[] Body)> resources = new();
    readonly Task server;
    PedalFrame? latest;
    long session;
    public string PairingUrl => $"{origin}/guard-pedals/index.html#pet={token}";

    public PedalBridge(int port = 5180)
    {
        origin = $"http://127.0.0.1:{port}";
        foreach (var (name, type) in new[] { ("index.html", "text/html"), ("style.css", "text/css"),
                     ("relay.js", "text/javascript"), ("MotionProtocol.js", "text/javascript"),
                     ("PedalContact.js", "text/javascript"), ("PlayablePedal.js", "text/javascript"),
                     ("PedalSerial.js", "text/javascript") })
        {
            using var stream = Application.GetResourceStream(new Uri("Assets/pedals/" + name, UriKind.Relative))!.Stream;
            using var content = new MemoryStream();
            stream.CopyTo(content);
            resources.Add("/guard-pedals/" + name, (type + "; charset=utf-8", content.ToArray()));
        }
        try
        {
            listener.Prefixes.Add(origin + "/");
            listener.Start();
        }
        catch
        {
            listener.Close();
            shutdown.Dispose();
            throw;
        }
        server = Listen(shutdown.Token);
    }

    public PedalFrame? Read()
    {
        var frame = Volatile.Read(ref latest);
        return frame is not null && Stopwatch.GetElapsedTime(frame.Received).TotalMilliseconds < 850 ? frame : null;
    }

    public async Task<string> PreferredPairingUrl()
    {
        // The optional same-origin copy can recover calibrations already saved by DESK AWAY.
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromMilliseconds(750) };
        try
        {
            var html = await client.GetStringAsync(SavedCalibrationOrigin + "/guard-pedals/index.html", shutdown.Token);
            if (html.Contains("name=\"desktop-pet-pedals\" content=\"M1\"", StringComparison.Ordinal))
                return SavedCalibrationOrigin + "/guard-pedals/index.html#pet=" + token;
        }
        catch (HttpRequestException) { }
        catch (OperationCanceledException) { }
        return PairingUrl;
    }

    async Task Listen(CancellationToken cancellation)
    {
        Task? active = null;
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                var request = context.Request;
                if (request.Url?.AbsolutePath == "/pedals/")
                {
                    var source = request.Headers["Origin"];
                    if (!request.IsWebSocketRequest || source != origin && source != SavedCalibrationOrigin ||
                        request.QueryString["token"] != token)
                        Finish(context, 403);
                    else if (active is { IsCompleted: false })
                        Finish(context, 409);
                    else
                        active = Receive(context, ++session, cancellation);
                }
                else if (request.HttpMethod == "GET" && request.Url is { } url && resources.TryGetValue(url.AbsolutePath, out var file))
                {
                    var response = context.Response;
                    response.ContentType = file.Type;
                    response.Headers["Cache-Control"] = "no-store";
                    response.Headers["Content-Security-Policy"] = "default-src 'self'; connect-src ws://127.0.0.1:5180; frame-ancestors 'none'";
                    try { response.Close(file.Body, willBlock: false); } catch (HttpListenerException) { }
                }
                else
                    Finish(context, 404);
            }
        }
        catch (HttpListenerException) when (cancellation.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { }
        finally { if (active is not null) await active.ConfigureAwait(false); }
    }

    static void Finish(HttpListenerContext context, int status)
    {
        context.Response.StatusCode = status;
        context.Response.Close();
    }

    async Task Receive(HttpListenerContext context, long currentSession, CancellationToken cancellation)
    {
        try
        {
            var connection = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
            using var socket = connection.WebSocket;
            var bytes = new byte[128];
            while (!cancellation.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(bytes.AsMemory(), cancellation).ConfigureAwait(false);
                if (result.MessageType != WebSocketMessageType.Text || !result.EndOfMessage) return;
                var frame = PedalFrame.Parse(Encoding.ASCII.GetString(bytes, 0, result.Count), currentSession);
                if (frame is null) return;
                Volatile.Write(ref latest, frame);
                bytes[0] = (byte)frame.Left.Bit;
                bytes[1] = (byte)frame.Right.Bit;
                await socket.SendAsync(bytes.AsMemory(0, 2), WebSocketMessageType.Text, true, cancellation).ConfigureAwait(false);
            }
        }
        catch (WebSocketException) { }
        catch (HttpListenerException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { Volatile.Write(ref latest, null); }
    }

    public void Dispose()
    {
        shutdown.Cancel();
        listener.Close();
        Volatile.Write(ref latest, null);
        _ = server.ContinueWith(_ => shutdown.Dispose(), TaskScheduler.Default);
    }
}
