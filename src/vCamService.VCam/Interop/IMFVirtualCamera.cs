using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFVirtualCamera — from mfvirtualcamera.h (Windows 11 22H2+).
/// GUID verified against DirectN auto-generated bindings from the Windows SDK.
/// Inherits from IMFAttributes; the runtime infers the 30 inherited vtable slots
/// automatically, so no <c>new</c> re-declarations are needed.
/// </summary>
[ComImport]
[Guid("1c08a864-ef6c-4c75-af59-5f2d68da9563")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFVirtualCamera : IMFAttributes
{
    // ===== IMFVirtualCamera-specific methods (after 30 inherited IMFAttributes slots) =====

    /// <summary>Adds device-source information (symbolic link, etc.) to the virtual camera.</summary>
    [PreserveSig] int AddDeviceSourceInfo([MarshalAs(UnmanagedType.LPWStr)] string deviceSourceInfo);

    /// <summary>Adds a device property to the virtual camera.</summary>
    [PreserveSig] int AddProperty(IntPtr pKey, int type, IntPtr pbData, int cbData);

    /// <summary>Adds a registry entry for the virtual camera device.</summary>
    [PreserveSig] int AddRegistryEntry(
        [MarshalAs(UnmanagedType.LPWStr)] string entryName,
        [MarshalAs(UnmanagedType.LPWStr)] string? subkeyPath,
        int dwRegType,
        IntPtr pbData,
        int cbData);

    /// <summary>
    /// Starts the virtual camera. Pass null if no async completion callback is needed.
    /// Activation of the media source CLSID is handled by MFCreateVirtualCamera, not Start.
    /// </summary>
    [PreserveSig] int Start([MarshalAs(UnmanagedType.Interface)] IMFAsyncCallback? pCallback);

    /// <summary>Stops the virtual camera.</summary>
    [PreserveSig] int Stop();

    /// <summary>Removes the virtual camera device from the system.</summary>
    [PreserveSig] int Remove();

    /// <summary>Gets the underlying IMFMediaSource for the virtual camera.</summary>
    [PreserveSig] int GetMediaSource([MarshalAs(UnmanagedType.Interface)] out IMFMediaSource ppMediaSource);

    /// <summary>Sends a camera property request.</summary>
    [PreserveSig] int SendCameraProperty(
        ref Guid propertySet,
        int propertyId,
        int propertyFlags,
        IntPtr propertyPayload,
        int propertyPayloadLength,
        IntPtr data,
        int dataLength,
        out int dataWritten);

    /// <summary>Creates a synchronisation event object.</summary>
    [PreserveSig] int CreateSyncEvent(
        ref Guid kseventSet,
        int kseventId,
        int kseventFlags,
        IntPtr eventHandle,
        [MarshalAs(UnmanagedType.Interface)] out object cameraSyncObject);

    /// <summary>Creates a synchronisation semaphore object.</summary>
    [PreserveSig] int CreateSyncSemaphore(
        ref Guid kseventSet,
        int kseventId,
        int kseventFlags,
        IntPtr semaphoreHandle,
        int semaphoreAdjust,
        [MarshalAs(UnmanagedType.Interface)] out object cameraSyncObject);
}
