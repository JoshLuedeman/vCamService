using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// Shared helper methods for Media Foundation interop.
/// </summary>
internal static class MFHelpers
{
    internal static void ThrowIfFailed(int hr, string context)
    {
        if (hr < 0) throw new COMException($"Media Foundation call failed in {context}", hr);
    }

    internal static long PackedUInt64(uint hi, uint lo) => ((long)hi << 32) | lo;
}
