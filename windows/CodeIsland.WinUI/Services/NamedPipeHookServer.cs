using System.IO.Pipes;
using CodeIsland.WinUI.Models;
using Microsoft.UI.Dispatching;

namespace CodeIsland.WinUI.Services;

public sealed class NamedPipeHookServer : IAsyncDisposable
{
    public const string PipeName = "codeisland";

    private readonly SessionStore store;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly CancellationTokenSource cts = new();
    private Task? acceptLoop;

    public bool IsRunning => acceptLoop is { IsCompleted: false };

    public NamedPipeHookServer(SessionStore store, DispatcherQueue dispatcherQueue)
    {
        this.store = store;
        this.dispatcherQueue = dispatcherQueue;
    }

    public void Start()
    {
        if (IsRunning) return;
        acceptLoop = Task.Run(() => RunAsync(cts.Token));
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(token);
                await HandleClientAsync(pipe, token);
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
        using var memory = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        do
        {
            read = await pipe.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read > 0) memory.Write(buffer, 0, read);
            if (memory.Length > 1_048_576)
            {
                await WriteResponseAsync(pipe, "{\"error\":\"payload_too_large\"}", token);
                return;
            }
        } while (!pipe.IsMessageComplete && read > 0);

        var hookEvent = HookEvent.TryParse(memory.ToArray());
        if (hookEvent is null)
        {
            await WriteResponseAsync(pipe, "{\"error\":\"parse_failed\"}", token);
            return;
        }

        var normalizedSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcherQueue.TryEnqueue(() => normalizedSource.SetResult(store.Apply(hookEvent)));
        var normalized = await normalizedSource.Task.ConfigureAwait(false);
        var response = await store.HandleBlockingAsync(hookEvent, normalized, token).ConfigureAwait(false);
        await WriteResponseAsync(pipe, response, token);
    }

    private static async Task WriteResponseAsync(NamedPipeServerStream pipe, string response, CancellationToken token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(response);
        await pipe.WriteAsync(bytes, token);
        await pipe.FlushAsync(token);
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        if (acceptLoop is not null)
        {
            try { await acceptLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        cts.Dispose();
    }
}