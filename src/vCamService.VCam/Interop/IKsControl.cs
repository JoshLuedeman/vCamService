using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IKsControl (GUID 28F54685-06FD-11D2-B27A-00A0C9223196).
/// Kernel Streaming control interface — included for completeness; not called directly
/// by this implementation.
/// </summary>
[ComImport]
[Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IKsControl
{
    [PreserveSig] int KsProperty(IntPtr property, int propertyLength, IntPtr propertyData, int dataLength, out int bytesReturned);
    [PreserveSig] int KsMethod(IntPtr method, int methodLength, IntPtr methodData, int dataLength, out int bytesReturned);
    [PreserveSig] int KsEvent(IntPtr @event, int eventLength, IntPtr eventData, int dataLength, out int bytesReturned);
}
