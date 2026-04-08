namespace vCamService.VCam.Interop;

/// <summary>
/// Well-known GUIDs and integer constants used throughout the virtual-camera implementation.
/// </summary>
public static class MFGuids
{
    // -----------------------------------------------------------------------
    // MFStartup version
    // -----------------------------------------------------------------------

    /// <summary>
    /// Passed to MFStartup. Value = (MF_SDK_VERSION &lt;&lt; 16 | MF_API_VERSION)
    /// where SDK=0x0002, API=0x0070.
    /// </summary>
    public const int MF_VERSION = 0x00020070;

    // -----------------------------------------------------------------------
    // HRESULT constants
    // -----------------------------------------------------------------------

    public const int S_OK = 0;
    public const int E_NOTIMPL = unchecked((int)0x80004001);
    public const int E_FAIL = unchecked((int)0x80004005);

    /// <summary>Returned when methods are called after Shutdown().</summary>
    public const int MF_E_SHUTDOWN = unchecked((int)0xC00D3E85);

    /// <summary>Returned from Pause() on a live source that does not support pausing.</summary>
    public const int MF_E_INVALID_STATE_TRANSITION = unchecked((int)0xC00D3E82);

    // -----------------------------------------------------------------------
    // IMFMediaSource::GetCharacteristics flags
    // -----------------------------------------------------------------------

    /// <summary>Source is live (no seeking, no pausing).</summary>
    public const int MFMEDIASOURCE_IS_LIVE = 0x1;

    // -----------------------------------------------------------------------
    // MediaEventType values (from mfobjects.h enum _MediaEventType)
    // -----------------------------------------------------------------------

    /// <summary>Fired by IMFMediaSource when it starts producing data.</summary>
    public const int MESourceStarted = 200;

    /// <summary>Fired by IMFMediaSource when it stops.</summary>
    public const int MESourceStopped = 202;

    /// <summary>Fired by IMFMediaSource to announce a new stream.</summary>
    public const int MENewStream = 204;

    /// <summary>Fired by IMFMediaStream when a stream ends naturally.</summary>
    public const int MEEndOfStream = 302;

    /// <summary>Fired by IMFMediaStream when a sample is ready for consumption.</summary>
    public const int MEMediaSample = 400;

    // -----------------------------------------------------------------------
    // Major media types
    // -----------------------------------------------------------------------

    /// <summary>MFMediaType_Video = {73646976-0000-0010-8000-00AA00389B71} ('vids' FOURCC).</summary>
    public static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00AA00389B71");

    // -----------------------------------------------------------------------
    // Video sub-types
    // -----------------------------------------------------------------------

    /// <summary>
    /// MFVideoFormat_ARGB32 — D3DFMT_A8R8G8B8 (value 21 = 0x00000015).
    /// Wire format is BGRA in memory (Blue byte first), matching SkiaSharp/GDI convention.
    /// </summary>
    public static readonly Guid MFVideoFormat_ARGB32 = new("00000015-0000-0010-8000-00AA00389B71");

    // -----------------------------------------------------------------------
    // IMFAttributes keys for media type configuration
    // -----------------------------------------------------------------------

    public static readonly Guid MF_MT_MAJOR_TYPE         = new("48EBA18E-F8C9-4687-BF11-0A74C9F96A8F");
    public static readonly Guid MF_MT_SUBTYPE             = new("F7E34C9A-42E8-4714-B74B-CB29D72C35E5");

    /// <summary>Packed (width &lt;&lt; 32 | height) in a SetUINT64 call.</summary>
    public static readonly Guid MF_MT_FRAME_SIZE          = new("1652C33D-D6B2-4012-B834-72030849A37D");

    /// <summary>Packed (numerator &lt;&lt; 32 | denominator) in a SetUINT64 call.</summary>
    public static readonly Guid MF_MT_FRAME_RATE          = new("C459A2E8-3D2C-4E44-B132-FEE5156C7BB0");

    public static readonly Guid MF_MT_PIXEL_ASPECT_RATIO  = new("C6BF4C91-5A9C-4B32-A7D4-FB8D76F47C19");
    public static readonly Guid MF_MT_INTERLACE_MODE      = new("E2724BB8-E676-4806-B4B2-A8D6EF8D9C9E");
    public static readonly Guid MF_MT_ALL_SAMPLES_INDEPENDENT = new("C9173739-5E56-461C-B713-46FB995CB95F");

    // -----------------------------------------------------------------------
    // KS categories
    // -----------------------------------------------------------------------

    /// <summary>KSCATEGORY_VIDEO_CAMERA — device category for virtual cameras.</summary>
    public static readonly Guid KSCATEGORY_VIDEO_CAMERA   = new("E5323777-F976-4F5B-9B55-B94699C46E44");
}
