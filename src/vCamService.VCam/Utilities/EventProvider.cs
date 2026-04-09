using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace vCamService.VCam.Utilities;

/// <summary>
/// ETW trace provider for diagnostics. Use WpfTraceSpy or similar ETW consumer
/// with provider GUID 964d4572-adb9-4f3a-8170-fcbecec27467 to view traces.
/// </summary>
public sealed class EventProvider : IDisposable
{
    public static Guid DefaultProviderId { get; } = new("964d4572-adb9-4f3a-8170-fcbecec27468");

    public static EventProvider? Current => _current.Value;
    private static readonly Lazy<EventProvider?> _current = new(() =>
    {
        try { return new EventProvider(DefaultProviderId); }
        catch { return null; }
    });

    private long _handle;

    public EventProvider(Guid id)
    {
        var hr = EventRegister(id, IntPtr.Zero, IntPtr.Zero, out _handle);
        if (hr != 0) throw new Win32Exception(hr);
    }

    public bool WriteMessageEvent(string? text, byte level = 0, long keywords = 0)
        => EventWriteString(_handle, level, keywords, text) == 0;

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0) EventUnregister(handle);
    }

    public static void LogError(string? message = null, [CallerMemberName] string? methodName = null, [CallerFilePath] string? filePath = null)
        => Log(TraceLevel.Error, message, methodName, filePath);

    public static void LogInfo(string? message = null, [CallerMemberName] string? methodName = null, [CallerFilePath] string? filePath = null)
        => Log(TraceLevel.Info, message, methodName, filePath);

    public static void Log(TraceLevel level, string? message = null, [CallerMemberName] string? methodName = null, [CallerFilePath] string? filePath = null)
    {
        var current = Current;
        if (current == null) return;
        var name = filePath != null ? Path.GetFileNameWithoutExtension(filePath) : null;
        current.WriteMessageEvent($"[{Environment.CurrentManagedThreadId}]{name}::{methodName}:{message}", (byte)level);
    }

    [DllImport("advapi32")]
    private static extern int EventRegister([MarshalAs(UnmanagedType.LPStruct)] Guid ProviderId, IntPtr EnableCallback, IntPtr CallbackContext, out long RegHandle);

    [DllImport("advapi32")]
    private static extern int EventUnregister(long RegHandle);

    [DllImport("advapi32")]
    private static extern int EventWriteString(long RegHandle, byte Level, long Keyword, [MarshalAs(UnmanagedType.LPWStr)] string? String);
}
