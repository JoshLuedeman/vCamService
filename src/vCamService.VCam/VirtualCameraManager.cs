using System.Runtime.InteropServices;
using DirectN;
using Microsoft.Win32;
using vCamService.Core.Services;
using vCamService.VCam.Utilities;

namespace vCamService.VCam;

/// <summary>
/// Public API for creating and managing the virtual camera device.
/// Handles COM registration, MF lifecycle, camera start/stop, and stream reader.
/// </summary>
public sealed class VirtualCameraManager : IDisposable
{
    public string DeviceName => "vCamService";
    public bool IsRunning { get; private set; }

    private IComObject<IMFVirtualCamera>? _camera;
    private VideoStreamReader? _streamReader;
    private bool _mfInitialised;
    private bool _disposed;

    public event Action<string>? OnLog;
    public event Action<string>? OnError;

    /// <summary>
    /// Checks whether the COM class is registered and all required files are present.
    /// </summary>
    public static bool IsComRegistered()
    {
        string clsid = typeof(Activator).GUID.ToString("B").ToUpperInvariant();
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey($@"Software\Classes\CLSID\{clsid}\InProcServer32");
        if (key?.GetValue(null) is not string comhostPath) return false;
        if (!File.Exists(comhostPath)) return false;

        // Verify essential companion files alongside comhost
        string comDir = Path.GetDirectoryName(comhostPath)!;
        string[] requiredFiles = ["vCamService.VCam.dll", "vCamService.VCam.runtimeconfig.json",
                                  "vCamService.VCam.deps.json", "vCamService.Core.dll"];
        return requiredFiles.All(f => File.Exists(Path.Combine(comDir, f)));
    }

    /// <summary>
    /// Start the virtual camera. If streamUrl is provided, starts ffmpeg to feed live frames.
    /// </summary>
    public void Start(string? streamUrl = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning) return;

        EventProvider.LogInfo("Start() entered");

        if (!IsComRegistered())
            throw new InvalidOperationException(
                "COM class not registered. Run diag\\register-com.ps1 as admin or install the MSI.");
        EventProvider.LogInfo("COM class verified registered");

        try
        {
            MFFunctions.MFStartup();
            _mfInitialised = true;
            EventProvider.LogInfo("MFStartup succeeded");

            string clsid = $"{{{typeof(Activator).GUID.ToString().ToUpperInvariant()}}}";
            EventProvider.LogInfo($"CLSID={clsid}");

            var hr = Functions.MFCreateVirtualCamera(
                __MIDL___MIDL_itf_mfvirtualcamera_0000_0000_0001.MFVirtualCameraType_SoftwareCameraSource,
                __MIDL___MIDL_itf_mfvirtualcamera_0000_0000_0002.MFVirtualCameraLifetime_Session,
                __MIDL___MIDL_itf_mfvirtualcamera_0000_0000_0003.MFVirtualCameraAccess_CurrentUser,
                DeviceName,
                clsid,
                null,
                0,
                out var camera);
            hr.ThrowOnError();

            _camera = new ComObject<IMFVirtualCamera>(camera);
            EventProvider.LogInfo("MFCreateVirtualCamera succeeded");

            _camera.Object.Start(null).ThrowOnError();
            EventProvider.LogInfo("IMFVirtualCamera.Start succeeded");

            // Start stream reader AFTER camera — it probes the stream, saves config,
            // then waits in background for the COM server to create shared memory.
            if (!string.IsNullOrWhiteSpace(streamUrl))
            {
                _streamReader = new VideoStreamReader(streamUrl);
                _streamReader.OnLog += msg => OnLog?.Invoke(msg);
                _streamReader.OnError += msg => OnError?.Invoke(msg);
                _streamReader.Start();
                OnLog?.Invoke($"Stream reader started for {streamUrl}");
            }

            IsRunning = true;
        }
        catch
        {
            // Rollback partial initialization on failure
            _streamReader?.Dispose();
            _streamReader = null;

            _camera?.Dispose();
            _camera = null;

            if (_mfInitialised)
            {
                MFFunctions.MFShutdown();
                _mfInitialised = false;
            }
            throw;
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        try
        {
            _camera?.Object.Remove();
        }
        finally
        {
            _camera?.Dispose();
            _camera = null;

            _streamReader?.Dispose();
            _streamReader = null;

            IsRunning = false;
            if (_mfInitialised)
            {
                MFFunctions.MFShutdown();
                _mfInitialised = false;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

}
