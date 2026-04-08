using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFMediaType (GUID 44AE0FA8-EA31-4109-8D2E-4CAE4997C555).
/// Inherits all IMFAttributes vtable slots, then adds media-type–specific methods.
/// </summary>
[ComImport]
[Guid("44AE0FA8-EA31-4109-8D2E-4CAE4997C555")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaType : IMFAttributes
{
    [PreserveSig] int IsEqual([MarshalAs(UnmanagedType.Interface)] IMFMediaType pIMediaType, out int pdwFlags);
    [PreserveSig] int GetRepresentation(Guid guidRepresentation, out IntPtr ppvRepresentation);
    [PreserveSig] int FreeRepresentation(Guid guidRepresentation, IntPtr pvRepresentation);
}
