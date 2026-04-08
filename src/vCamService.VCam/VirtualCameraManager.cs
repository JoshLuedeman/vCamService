using System.Reflection;
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

        // 1. Register the COM class so MF can CoCreateInstance it
        RegisterComClass();

        // 2. Point the shared frame buffer at our instance
        VirtualCameraSource.SharedFrameBuffer = FrameBuffer;

        // 3. Initialise Media Foundation
        int hr = MFStartup(MF_VERSION, 0);
        ThrowIfFailed(hr, nameof(MFStartup));
        _mfInitialised = true;

        // 4. Create the virtual camera
        //    type=0 (SoftwareCameraSource), lifetime=1 (Session), access=0 (CurrentUser)
        var category = KSCATEGORY_VIDEO_CAMERA;
        string clsidStr = $"{{{typeof(VirtualCameraSource).GUID.ToString().ToUpperInvariant()}}}";

        hr = MFCreateVirtualCamera(
            type: 0,
            lifetime: 1,
            access: 0,
            friendlyName: DeviceName,
            sourceId: clsidStr,
            categories: ref category,
            categoryCount: 1,
            virtualCamera: out IMFVirtualCamera cam);
        ThrowIfFailed(hr, nameof(MFCreateVirtualCamera));
        _camera = cam;

        // 5. Start — null causes MF to use CoCreateInstance on the registered sourceId
        hr = _camera.Start(null);
        ThrowIfFailed(hr, "IMFVirtualCamera.Start");

        IsRunning = true;
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
    /// Writes per-user COM registration entries under
    /// <c>HKCU\Software\Classes\CLSID\{clsid}\InProcServer32</c>
    /// so that MF can locate <c>vCamService.VCam.comhost.dll</c>.
    /// </summary>
    private void RegisterComClass()
    {
        string clsid = typeof(VirtualCameraSource).GUID.ToString("B").ToUpperInvariant();

        // The comhost.dll sits alongside the managed assembly.
        string assemblyPath = Assembly.GetExecutingAssembly().Location;
        string comhostPath = Path.ChangeExtension(assemblyPath, null) + ".comhost.dll";

        // HKCU\Software\Classes\CLSID\{guid}  (default) = friendly description
        using RegistryKey clsidKey = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\CLSID\{clsid}", writable: true);
        clsidKey.SetValue(null, "vCamService Camera Source");

        // HKCU\Software\Classes\CLSID\{guid}\InProcServer32
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
