using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFMediaEvent (GUID DF598932-F10C-4E39-BBA2-C308F101DAA3).
/// Inherits all IMFAttributes vtable slots, then adds event-specific methods.
/// </summary>
[ComImport]
[Guid("DF598932-F10C-4E39-BBA2-C308F101DAA3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaEvent : IMFAttributes
{
    // GetType returns the MediaEventType enum value (e.g. MESourceStarted = 200)
    [PreserveSig] int GetType(out int pmet);
    [PreserveSig] int GetExtendedType(out Guid pguidExtendedType);
    [PreserveSig] int GetStatus(out int phrStatus);
    // PROPVARIANT is opaque here; callers that need the value should use a proper struct.
    [PreserveSig] int GetValue(IntPtr pvValue);
}
