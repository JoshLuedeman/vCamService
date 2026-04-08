using System.Runtime.InteropServices;
using Microsoft.Win32;
using vCamService.Core.Services;
using vCamService.VCam.Interop;
using static vCamService.VCam.Interop.MFGuids;
using static vCamService.VCam.Interop.MFInterop;

namespace vCamService.VCam;

/// <summary>
/// Public API for the virtual camera device.
///
/// Typical usage:
/// <code>
///   using var mgr = new VirtualCameraManager();
///   mgr.Start();
///   // ...
///   mgr.SendFrame(bgraBytes, 1280, 720);
///   // ...
///   mgr.Stop();
/// </code>
/// </summary>
public sealed class VirtualCameraManager : IDisposable
{
    // ------------------------------------------------------------------
    // Public surface
    // ------------------------------------------------------------------

    /// <summary>Friendly name shown in Windows camera pickers.</summary>
    public string DeviceName => "vCamService Camera";

    /// <summary>True once <see cref="Start"/> completes successfully.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Write frames here via <see cref="SendFrame"/>.</summary>
    public FrameBuffer FrameBuffer { get; } = new();

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    private IMFVirtualCamera? _camera;
    private bool _mfInitialised;
    private bool _disposed;

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    /// <summary>
    /// Registers the COM class, initialises Media Foundation, creates the virtual
    /// camera device, and starts delivering frames.
    /// </summary>
    /// <param name="width">Frame width in pixels (default 1280).</param>
    /// <param name="height">Frame height in pixels (default 720).</param>
    /// <param name="fps">Target frame rate (default 30; advisory only for session cameras).</param>
    public void Start(int width = 1280, int height = 720, int fps = 30)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning) return;

        string diagLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "vCamService", "vcam-diag.log");
        void Diag(string msg) => File.AppendAllText(diagLog, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        Diag("Start() entered");

        // 1. Register the COM class so MF can CoCreateInstance it
        RegisterComClass();
        Diag("COM class registered");

        // 2. Point the shared frame buffer at our instance
        VirtualCameraSource.SharedFrameBuffer = FrameBuffer;

        // 3. Initialise Media Foundation
        int hr = MFStartup(MF_VERSION, 0);
        Diag($"MFStartup returned HR=0x{hr:X8}");
        ThrowIfFailed(hr, nameof(MFStartup));
        _mfInitialised = true;

        // 4. Create the virtual camera — get raw IntPtr to avoid CLR COM marshaling issues
        var category = KSCATEGORY_VIDEO_CAMERA;
        string clsidStr = $"{{{typeof(VirtualCameraSource).GUID.ToString().ToUpperInvariant()}}}";
        string comhostPath = Path.Combine(AppContext.BaseDirectory, "vCamService.VCam.comhost.dll");
        Diag($"CLSID={clsidStr}");
        Diag($"comhost path={comhostPath}, exists={File.Exists(comhostPath)}");

        hr = MFCreateVirtualCameraRaw(
            type: 0,
            lifetime: 1,
            access: 0,
            friendlyName: DeviceName,
            sourceId: clsidStr,
            categories: ref category,
            categoryCount: 1,
            virtualCamera: out IntPtr camPtr);
        Diag($"MFCreateVirtualCamera returned HR=0x{hr:X8}, ptr=0x{camPtr:X}");
        ThrowIfFailed(hr, nameof(MFCreateVirtualCamera));

        // Read vtable pointer and check Start slot
        IntPtr vtable = Marshal.ReadIntPtr(camPtr);
        // IMFVirtualCamera vtable: 3 IUnknown + 30 IMFAttributes + [AddDeviceSourceInfo, AddProperty, AddRegistryEntry, Start]
        // Start is at slot 36
        int startSlot = 3 + 30 + 3; // = 36
        IntPtr startFuncPtr = Marshal.ReadIntPtr(vtable, startSlot * IntPtr.Size);
        Diag($"vtable=0x{vtable:X}, Start slot={startSlot}, Start func=0x{startFuncPtr:X}");

        // Call Start(IMFVirtualCamera* this, IMFAsyncCallback* pCallback) via raw function pointer
        // HRESULT (__stdcall*)(void* pThis, void* pCallback)
        hr = RawStartCall(camPtr, startFuncPtr);
        Diag($"Raw Start returned HR=0x{hr:X8}");
        ThrowIfFailed(hr, "IMFVirtualCamera.Start");

        // Wrap the raw pointer in an RCW for later use (Stop, Remove)
        _camera = (IMFVirtualCamera)Marshal.GetObjectForIUnknown(camPtr);
        Marshal.Release(camPtr); // GetObjectForIUnknown AddRef'd it

        IsRunning = true;
        Diag("Start() completed successfully");
    }

    // Delegate matching HRESULT Start(void* pThis, void* pCallback)
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int StartFn(IntPtr pThis, IntPtr pCallback);

    private static int RawStartCall(IntPtr camPtr, IntPtr funcPtr)
    {
        var fn = Marshal.GetDelegateForFunctionPointer<StartFn>(funcPtr);
        return fn(camPtr, IntPtr.Zero);
    }

    /// <summary>Stops frame delivery and removes the virtual camera device.</summary>
    public void Stop()
    {
        if (!IsRunning) return;

        try
        {
            _camera?.Stop();
            _camera?.Remove();
        }
        finally
        {
            _camera = null;
            IsRunning = false;

            if (_mfInitialised)
            {
                MFShutdown();
                _mfInitialised = false;
            }
        }
    }

    /// <summary>
    /// Pushes a new BGRA frame into the shared buffer.  MF will pick it up on the next
    /// <c>RequestSample</c> call.
    /// </summary>
    /// <param name="bgraData">Raw pixel bytes in BGRA order, <c>width * height * 4</c> bytes.</param>
    /// <param name="width">Frame width.</param>
    /// <param name="height">Frame height.</param>
    public void SendFrame(byte[] bgraData, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(bgraData);
        FrameBuffer.Put(bgraData, width, height);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    // ------------------------------------------------------------------
    // COM registration
    // ------------------------------------------------------------------

    /// <summary>
    /// Copies COM hosting files to a system-accessible directory and writes
    /// machine-wide COM registration under HKLM so the Camera Frame Server
    /// service (NT AUTHORITY\LocalService) can activate the media source.
    /// Requires the application to run elevated (admin).
    /// </summary>
    private void RegisterComClass()
    {
        string clsid = typeof(VirtualCameraSource).GUID.ToString("B").ToUpperInvariant();
        string baseDir = AppContext.BaseDirectory;

        // Frame Server (LocalService) can't access user-profile directories.
        // Copy COM hosting files to ProgramData where all accounts can read.
        string comDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "vCamService", "com");
        Directory.CreateDirectory(comDir);

        string[] comFiles = [
            "vCamService.VCam.comhost.dll",
            "vCamService.VCam.dll",
            "vCamService.VCam.runtimeconfig.json",
            "vCamService.VCam.deps.json",
            "vCamService.Core.dll",
        ];
        foreach (string file in comFiles)
        {
            string src = Path.Combine(baseDir, file);
            if (File.Exists(src))
                File.Copy(src, Path.Combine(comDir, file), overwrite: true);
        }

        string comhostPath = Path.Combine(comDir, "vCamService.VCam.comhost.dll");

        // HKLM\Software\Classes\CLSID\{guid}
        using RegistryKey clsidKey = Registry.LocalMachine.CreateSubKey(
            $@"Software\Classes\CLSID\{clsid}", writable: true);
        clsidKey.SetValue(null, "vCamService Camera Source");

        // HKLM\Software\Classes\CLSID\{guid}\InProcServer32
        using RegistryKey inprocKey = clsidKey.CreateSubKey("InProcServer32");
        inprocKey.SetValue(null, comhostPath);
        inprocKey.SetValue("ThreadingModel", "Both");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void ThrowIfFailed(int hr, string context)
    {
        if (hr < 0) throw new COMException($"Media Foundation call failed in {context}", hr);
    }
}
