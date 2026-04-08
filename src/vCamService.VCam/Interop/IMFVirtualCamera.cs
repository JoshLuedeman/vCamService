using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFVirtualCamera — interface from mfvirtualcamera.h.
/// Introduced in Windows 11 (22H2). GUID: 02B7B2D1-9461-4CC9-B4D2-FFE23F07CF8E.
/// Note: verify GUID against your Windows SDK's mfvirtualcamera.h before shipping.
/// </summary>
[ComImport]
[Guid("02B7B2D1-9461-4CC9-B4D2-FFE23F07CF8E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFVirtualCamera
{
    /// <summary>Attaches the managed IMFMediaSource as the video source.</summary>
    [PreserveSig] int AddSource([MarshalAs(UnmanagedType.Interface)] IMFMediaSource pMediaSource);

    [PreserveSig] int AddEffect([MarshalAs(UnmanagedType.Interface)] object pEffect);
    [PreserveSig] int RemoveEffect([MarshalAs(UnmanagedType.Interface)] object pEffect);
    [PreserveSig] int RemoveAllEffects();

    /// <summary>
    /// Starts the virtual camera. Pass null to use CoCreateInstance on the registered sourceId CLSID.
    /// </summary>
    [PreserveSig] int Start([MarshalAs(UnmanagedType.Interface)] object? pActivate);

    [PreserveSig] int Stop();

    /// <summary>Removes the virtual camera device from the system (session-lifetime cameras auto-remove on process exit).</summary>
    [PreserveSig] int Remove();

    [PreserveSig] int GetMediaSource([MarshalAs(UnmanagedType.Interface)] out IMFMediaSource ppMediaSource);
}
