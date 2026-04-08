using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFAttributes (GUID 2CD2D921-C447-44A7-A13C-4ADABFC247E3).
/// Base interface for many MF objects. Only used to call into native MF objects.
/// </summary>
[ComImport]
[Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFAttributes
{
    [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
    [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
    [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
    [PreserveSig] int Compare([MarshalAs(UnmanagedType.Interface)] IMFAttributes pTheirs, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
    [PreserveSig] int GetUINT32(ref Guid guidKey, out int punValue);
    [PreserveSig] int GetUINT64(ref Guid guidKey, out long punValue);
    [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
    [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
    [PreserveSig] int GetStringLength(ref Guid guidKey, out int pcchLength);
    [PreserveSig] int GetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string pwszValue, int cchBufSize, out int pcchLength);
    [PreserveSig] int GetAllocatedString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out int pcchLength);
    [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
    [PreserveSig] int GetBlob(ref Guid guidKey, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pBuf, int cbBufSize, out int pcbBlobSize);
    [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
    [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
    [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
    [PreserveSig] int DeleteItem(ref Guid guidKey);
    [PreserveSig] int DeleteAllItems();
    [PreserveSig] int GetCount(out int pcItems);
    [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
    [PreserveSig] int CopyAllItems([MarshalAs(UnmanagedType.Interface)] IMFAttributes pDest);
    [PreserveSig] int SetUINT32(ref Guid guidKey, int unValue);
    [PreserveSig] int SetUINT64(ref Guid guidKey, long unValue);
    [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
    [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
    [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    [PreserveSig] int SetBlob(ref Guid guidKey, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pBuf, int cbBufSize);
    [PreserveSig] int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.Interface)] object pUnknown);
}
