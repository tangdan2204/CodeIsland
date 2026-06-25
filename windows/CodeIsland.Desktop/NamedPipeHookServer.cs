using System.IO.Pipes;
using System.Windows.Threading;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CodeIsland.Desktop;

public sealed class NamedPipeHookServer : IAsyncDisposable
{
    public const string PipeName = "codeisland";
    private readonly SessionStore store;
    private readonly Dispatcher dispatcher;
    private readonly CancellationTokenSource cts = new();
    private Task? acceptLoop;

    public NamedPipeHookServer(SessionStore store, Dispatcher dispatcher)
    {
        this.store = store;
        this.dispatcher = dispatcher;
    }

    public void Start() => acceptLoop ??= Task.Run(() => RunAsync(cts.Token));

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var security = new PipeSecurity();
                security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
                var pipe = NamedPipeServerStreamAcl.Create(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    security);
                await pipe.WaitForConnectionAsync(token);
                _ = Task.Run(() => HandleClientAsync(pipe, token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        try
        {
            var lengthBytes = await ReadExactAsync(pipe, 4, token);
            var length = BitConverter.ToInt32(lengthBytes, 0);
            if (length <= 0 || length > 1_048_576)
            {
                await WriteResponseAsync(pipe, "{\"error\":\"payload_too_large\"}", token);
                return;
            }

            var payload = await ReadExactAsync(pipe, length, token);
            var hookEvent = HookEvent.TryParse(payload);
            if (hookEvent is null)
            {
                await WriteResponseAsync(pipe, "{\"error\":\"parse_failed\"}", token);
                return;
            }

            var normalized = await dispatcher.InvokeAsync(() => store.Apply(hookEvent));
            if (normalized == "PermissionRequest" || (normalized == "Notification" && hookEvent.Question is not null))
            {
                var response = await store.HandleBlockingAsync(hookEvent, normalized, token).ConfigureAwait(false);
                await WriteResponseAsync(pipe, response, token);
            }
        }
        finally
        {
            pipe.Dispose();
        }
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken token)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), token);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
        return buffer;
    }

    private static async Task WriteResponseAsync(NamedPipeServerStream pipe, string response, CancellationToken token)
    {
        var body = System.Text.Encoding.UTF8.GetBytes(response);
        var length = BitConverter.GetBytes(body.Length);
        await pipe.WriteAsync(length, token);
        if (body.Length > 0) await pipe.WriteAsync(body, token);
        await pipe.FlushAsync(token);
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        if (acceptLoop is not null)
        {
            try { await acceptLoop.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        cts.Dispose();
    }
}
