using System.Runtime.InteropServices;

namespace SiriAI.HarnessHost;

internal static class StartupCancellation
{
    internal static async Task<bool> RunAsync(Func<CancellationToken, Task> operation)
    {
        using var stopping = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stopping.Cancel(); };
        Console.CancelKeyPress += cancel;
        using var terminate = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(PosixSignal.SIGTERM, e => { e.Cancel = true; stopping.Cancel(); });
        try { await operation(stopping.Token); return true; }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { return false; }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
