using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// P/Invoke declarations for mf.dll (MFCreateVirtualCamera) and mfplat.dll
/// (MFStartup, MFShutdown, MFCreate* helpers).
/// All functions return HRESULT; check &lt; 0 for failure.
/// </summary>
public static class MFInterop
{
    // -----------------------------------------------------------------------
    // mf.dll
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a virtual camera device. Windows 11 22H2+ only.
    /// </summary>
    /// <param name="type">0 = SoftwareCameraSource.</param>
    /// <param name="lifetime">0 = System, 1 = Session (removed when process exits).</param>
    /// <param name="access">0 = CurrentUser, 1 = AllUsers.</param>
    /// <param name="friendlyName">Display name shown in camera pickers.</param>
    /// <param name="sourceId">String form of the COM CLSID that implements IMFMediaSource.</param>
    /// <param name="categories">GUID of the KS category (e.g., KSCATEGORY_VIDEO_CAMERA).</param>
    /// <param name="categoryCount">Number of entries in <paramref name="categories"/> (normally 1).</param>
    /// <param name="virtualCamera">Receives the IMFVirtualCamera interface.</param>
    [DllImport("mf.dll")]
    public static extern int MFCreateVirtualCamera(
        int type,
        int lifetime,
        int access,
        [MarshalAs(UnmanagedType.LPWStr)] string friendlyName,
        [MarshalAs(UnmanagedType.LPWStr)] string sourceId,
        [In] ref Guid categories,
        int categoryCount,
        [MarshalAs(UnmanagedType.Interface)] out IMFVirtualCamera virtualCamera);

    // -----------------------------------------------------------------------
    // mfplat.dll
    // -----------------------------------------------------------------------

    /// <summary>Initialises the Media Foundation platform.</summary>
    /// <param name="version">Pass <see cref="MFGuids.MF_VERSION"/>.</param>
    /// <param name="flags">0 for the default apartment.</param>
    [DllImport("mfplat.dll")]
    public static extern int MFStartup(int version, int flags);

    [DllImport("mfplat.dll")]
    public static extern int MFShutdown();

    /// <summary>Creates a thread-safe media-event queue used by IMFMediaEventGenerator implementations.</summary>
    [DllImport("mfplat.dll")]
    public static extern int MFCreateEventQueue(
        [MarshalAs(UnmanagedType.Interface)] out IMFMediaEventQueue queue);

    [DllImport("mfplat.dll")]
    public static extern int MFCreateMediaType(
        [MarshalAs(UnmanagedType.Interface)] out IMFMediaType mediaType);

    [DllImport("mfplat.dll")]
    public static extern int MFCreateSample(
        [MarshalAs(UnmanagedType.Interface)] out IMFSample sample);

    [DllImport("mfplat.dll")]
    public static extern int MFCreateMemoryBuffer(
        int maxLength,
        [MarshalAs(UnmanagedType.Interface)] out IMFMediaBuffer buffer);

    [DllImport("mfplat.dll")]
    public static extern int MFCreateStreamDescriptor(
        int streamId,
        int mediaTypeCount,
        [MarshalAs(UnmanagedType.LPArray)] IMFMediaType[] mediaTypes,
        [MarshalAs(UnmanagedType.Interface)] out IMFStreamDescriptor streamDescriptor);

    [DllImport("mfplat.dll")]
    public static extern int MFCreatePresentationDescriptor(
        int streamDescCount,
        [MarshalAs(UnmanagedType.LPArray)] IMFStreamDescriptor[] streamDescs,
        [MarshalAs(UnmanagedType.Interface)] out IMFPresentationDescriptor presentationDesc);
}
