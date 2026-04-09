using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace vCamService.Core.Services;

/// <summary>
/// Shared-memory double-buffer for passing raw video frames between processes.
/// Producer (WPF app with ffmpeg) writes frames; Consumer (COM server in Frame Server) reads.
/// Uses a lock-free commit protocol with sequence numbers to prevent tearing.
/// Supports NV12 (default, preferred by most apps) and BGRA pixel formats.
/// 
/// Memory layout:
///   [Header: 64 bytes] [Slot0: frameSize bytes] [Slot1: frameSize bytes]
/// </summary>
public sealed class SharedFrameBuffer : IDisposable
{
    public const string MmfName = "Global\\vCamService_FrameBuffer";
    public const int HeaderSize = 64;
    public const int DefaultWidth = 1280;
    public const int DefaultHeight = 720;

    /// <summary>Pixel format stored in header: 0 = BGRA (4 bpp), 1 = NV12 (1.5 bpp).</summary>
    public const int PixelFormatBGRA = 0;
    public const int PixelFormatNV12 = 1;

    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _view;
    private unsafe byte* _basePtr;
    private bool _ptrAcquired;
    private bool _disposed;
    private long _frameCounter;

    /// <summary>
    /// Header layout (64 bytes):
    ///   0-3:   magic (0x56434153 = "VCAS")
    ///   4-7:   version (2)
    ///   8-11:  width
    ///  12-15:  height
    ///  16-19:  stride (depends on pixel format)
    ///  20-23:  active slot index (0 or 1) — slot with latest complete frame
    ///  24-31:  sequence number (odd = write in progress, even = committed)
    ///  32-39:  frame counter
    ///  40-47:  producer heartbeat (ticks)
    ///  48-51:  fps numerator
    ///  52-55:  fps denominator
    ///  56-59:  pixel format (0=BGRA, 1=NV12)
    ///  60-63:  reserved
    /// </summary>
    private const int OffsetMagic = 0;
    private const int OffsetVersion = 4;
    private const int OffsetWidth = 8;
    private const int OffsetHeight = 12;
    private const int OffsetStride = 16;
    private const int OffsetActiveSlot = 20;
    private const int OffsetSequence = 24;
    private const int OffsetFrameCounter = 32;
    private const int OffsetHeartbeat = 40;
    private const int OffsetFpsNum = 48;
    private const int OffsetFpsDen = 52;
    private const int OffsetPixelFormat = 56;
    private const uint Magic = 0x56434153;
    private const int Version = 2;

    public int Width { get; private set; } = DefaultWidth;
    public int Height { get; private set; } = DefaultHeight;
    public int FpsNumerator { get; private set; } = 30;
    public int FpsDenominator { get; private set; } = 1;
    public int PixelFormat { get; private set; } = PixelFormatNV12;

    /// <summary>Frame size in bytes. NV12 = w*h*3/2, BGRA = w*h*4.</summary>
    public int FrameSize => PixelFormat == PixelFormatNV12
        ? Width * Height * 3 / 2
        : Width * Height * 4;

    /// <summary>
    /// Create as owner (COM server in Frame Server). Creates the Global\ MMF with NULL DACL.
    /// LocalService has SeCreateGlobalPrivilege natively.
    /// Acquires a raw pointer held for the lifetime of this instance.
    /// </summary>
    public static unsafe SharedFrameBuffer CreateOwner(int width, int height, int fpsNum, int fpsDen, int pixelFormat = PixelFormatNV12)
    {
        var buffer = new SharedFrameBuffer
        {
            Width = width, Height = height,
            FpsNumerator = fpsNum, FpsDenominator = fpsDen,
            PixelFormat = pixelFormat
        };
        int frameSize = pixelFormat == PixelFormatNV12
            ? width * height * 3 / 2
            : width * height * 4;
        int stride = pixelFormat == PixelFormatNV12 ? width : width * 4;
        long totalSize = HeaderSize + (frameSize * 2L);

        // Create MMF with a NULL DACL (allows all access) so the app can write.
        nint secAttr = CreateEveryoneSecurityAttributes();
        nint nativeHandle = nint.Zero;
        try
        {
            nativeHandle = CreateFileMappingW(
                new nint(-1), // INVALID_HANDLE_VALUE → page file backed
                secAttr,
                0x04, // PAGE_READWRITE
                (uint)(totalSize >> 32),
                (uint)(totalSize & 0xFFFFFFFF),
                MmfName);

            if (nativeHandle == nint.Zero)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            buffer._mmf = MemoryMappedFile.OpenExisting(MmfName, MemoryMappedFileRights.ReadWrite);
        }
        finally
        {
            if (nativeHandle != nint.Zero) CloseHandle(nativeHandle);
            FreeSecurityAttributes(secAttr);
        }

        buffer._view = buffer._mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite);

        // Acquire pointer once — held until Dispose()
        byte* ptr = null;
        buffer._view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        buffer._basePtr = ptr;
        buffer._ptrAcquired = true;

        // Initialize header — write magic LAST so consumers see a complete header
        *(int*)(ptr + OffsetVersion) = Version;
        *(int*)(ptr + OffsetWidth) = width;
        *(int*)(ptr + OffsetHeight) = height;
        *(int*)(ptr + OffsetStride) = stride;
        *(int*)(ptr + OffsetActiveSlot) = 0;
        *(long*)(ptr + OffsetSequence) = 0L;
        *(long*)(ptr + OffsetFrameCounter) = 0L;
        *(long*)(ptr + OffsetHeartbeat) = DateTime.UtcNow.Ticks;
        *(int*)(ptr + OffsetFpsNum) = fpsNum;
        *(int*)(ptr + OffsetFpsDen) = fpsDen;
        *(int*)(ptr + OffsetPixelFormat) = pixelFormat;
        Thread.MemoryBarrier();
        *(uint*)(ptr + OffsetMagic) = Magic; // magic last = ready signal

        return buffer;
    }

    /// <summary>
    /// Open as writer (WPF app). Opens existing Global\ MMF for ReadWrite.
    /// No SeCreateGlobalPrivilege needed — just opens an existing object.
    /// Acquires a raw pointer held for the lifetime of this instance.
    /// </summary>
    public static unsafe SharedFrameBuffer? OpenWriter()
    {
        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? view = null;
        try
        {
            mmf = MemoryMappedFile.OpenExisting(MmfName, MemoryMappedFileRights.ReadWrite);
            view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

            byte* ptr = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            if (*(uint*)(ptr + OffsetMagic) != Magic)
            {
                view.SafeMemoryMappedViewHandle.ReleasePointer();
                view.Dispose();
                mmf.Dispose();
                return null;
            }

            var buffer = new SharedFrameBuffer();
            buffer._mmf = mmf;
            buffer._view = view;
            buffer._basePtr = ptr;
            buffer._ptrAcquired = true;
            buffer.Width = *(int*)(ptr + OffsetWidth);
            buffer.Height = *(int*)(ptr + OffsetHeight);
            buffer.FpsNumerator = Math.Max(*(int*)(ptr + OffsetFpsNum), 1);
            buffer.FpsDenominator = Math.Max(*(int*)(ptr + OffsetFpsDen), 1);
            int pixFmt = *(int*)(ptr + OffsetPixelFormat);
            buffer.PixelFormat = pixFmt == PixelFormatBGRA ? PixelFormatBGRA : PixelFormatNV12;
            return buffer;
        }
        catch
        {
            view?.Dispose();
            mmf?.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Create as producer (WPF app, requires elevation for Global\ namespace).
    /// DEPRECATED: Use CreateOwner (COM server) + OpenWriter (app) instead.
    /// Kept for backward compatibility during development.
    /// </summary>
    public static SharedFrameBuffer CreateProducer(int width, int height, int fpsNum, int fpsDen, int pixelFormat = PixelFormatNV12)
    {
        return CreateOwner(width, height, fpsNum, fpsDen, pixelFormat);
    }

    /// <summary>
    /// Open as consumer (COM server in Frame Server). Opens existing MMF read-only.
    /// </summary>
    public static unsafe SharedFrameBuffer? OpenConsumer()
    {
        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? view = null;
        try
        {
            mmf = MemoryMappedFile.OpenExisting(MmfName, MemoryMappedFileRights.Read);
            view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            byte* ptr = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            if (*(uint*)(ptr + OffsetMagic) != Magic)
            {
                view.SafeMemoryMappedViewHandle.ReleasePointer();
                view.Dispose();
                mmf.Dispose();
                return null;
            }

            var buffer = new SharedFrameBuffer();
            buffer._mmf = mmf;
            buffer._view = view;
            buffer._basePtr = ptr;
            buffer._ptrAcquired = true;
            buffer.Width = *(int*)(ptr + OffsetWidth);
            buffer.Height = *(int*)(ptr + OffsetHeight);
            buffer.FpsNumerator = Math.Max(*(int*)(ptr + OffsetFpsNum), 1);
            buffer.FpsDenominator = Math.Max(*(int*)(ptr + OffsetFpsDen), 1);
            int pixFmt = *(int*)(ptr + OffsetPixelFormat);
            buffer.PixelFormat = pixFmt == PixelFormatBGRA ? PixelFormatBGRA : PixelFormatNV12;
            return buffer;
        }
        catch
        {
            view?.Dispose();
            mmf?.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Producer: write a complete frame using lock-free double-buffer protocol.
    /// Uses pre-acquired pointer for zero per-call overhead.
    /// </summary>
    public unsafe void WriteFrame(ReadOnlySpan<byte> frameData)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_basePtr == null) return;
        int frameSize = FrameSize;
        if (frameData.Length < frameSize) return;

        int activeSlot = *(int*)(_basePtr + OffsetActiveSlot);
        int writeSlot = 1 - activeSlot;

        // Mark sequence odd (write in progress)
        long seq = *(long*)(_basePtr + OffsetSequence);
        long newSeq = seq + 1;
        *(long*)(_basePtr + OffsetSequence) = newSeq;
        Thread.MemoryBarrier();

        // Write frame data to inactive slot — single memcpy
        long slotOffset = HeaderSize + ((long)writeSlot * frameSize);
        frameData[..frameSize].CopyTo(new Span<byte>(_basePtr + slotOffset, frameSize));

        // Commit: flip active slot, mark sequence even
        Thread.MemoryBarrier();
        *(int*)(_basePtr + OffsetActiveSlot) = writeSlot;
        *(long*)(_basePtr + OffsetSequence) = newSeq + 1;
        *(long*)(_basePtr + OffsetFrameCounter) = ++_frameCounter;
        *(long*)(_basePtr + OffsetHeartbeat) = DateTime.UtcNow.Ticks;
    }

    /// <summary>
    /// Consumer: read the latest complete frame into the destination buffer.
    /// Returns true if a valid frame was read without tearing.
    /// Uses pre-acquired pointer for zero per-call overhead.
    /// </summary>
    public unsafe bool TryReadFrame(nint destination, int destinationLength)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_basePtr == null) return false;
        int frameSize = FrameSize;
        if (destinationLength < frameSize) return false;

        long seqBefore = *(long*)(_basePtr + OffsetSequence);
        if (seqBefore % 2 != 0) return false; // write in progress

        int activeSlot = *(int*)(_basePtr + OffsetActiveSlot);
        long slotOffset = HeaderSize + ((long)activeSlot * frameSize);

        Buffer.MemoryCopy(_basePtr + slotOffset, (void*)destination, destinationLength, frameSize);

        Thread.MemoryBarrier();
        long seqAfter = *(long*)(_basePtr + OffsetSequence);
        return seqBefore == seqAfter;
    }

    /// <summary>
    /// Consumer: check if producer is alive (heartbeat within last 2 seconds).
    /// </summary>
    public unsafe bool IsProducerAlive()
    {
        if (_basePtr == null) return false;
        long ticks = *(long*)(_basePtr + OffsetHeartbeat);
        return (DateTime.UtcNow.Ticks - ticks) < TimeSpan.TicksPerSecond * 2;
    }

    /// <summary>
    /// Get a raw pointer to a specific slot for direct writes (zero-copy from pipe).
    /// Caller must write exactly FrameSize bytes.
    /// </summary>
    public unsafe byte* GetSlotPointer(int slot)
    {
        if (_basePtr == null) return null;
        return _basePtr + HeaderSize + ((long)slot * FrameSize);
    }

    /// <summary>
    /// Commit a frame written directly via GetSlotPointer.
    /// Call after writing the full frame to the slot.
    /// </summary>
    public unsafe void CommitSlot(int slot)
    {
        if (_basePtr == null) return;
        Thread.MemoryBarrier();
        *(int*)(_basePtr + OffsetActiveSlot) = slot;
        long seq = *(long*)(_basePtr + OffsetSequence);
        *(long*)(_basePtr + OffsetSequence) = seq + 2; // keep even (committed)
        *(long*)(_basePtr + OffsetFrameCounter) = ++_frameCounter;
        *(long*)(_basePtr + OffsetHeartbeat) = DateTime.UtcNow.Ticks;
    }

    /// <summary>Get the inactive slot index for writing.</summary>
    public unsafe int GetWriteSlot()
    {
        if (_basePtr == null) return 0;
        return 1 - *(int*)(_basePtr + OffsetActiveSlot);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ptrAcquired && _view != null)
        {
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
            _ptrAcquired = false;
        }
        unsafe { _basePtr = null; }
        _view?.Dispose();
        _mmf?.Dispose();
    }

    #region Win32 interop for kernel object security

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateFileMappingW(
        nint hFile, nint lpAttributes, uint flProtect,
        uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool InitializeSecurityDescriptor(nint pSecurityDescriptor, uint dwRevision);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool SetSecurityDescriptorDacl(
        nint pSecurityDescriptor, bool bDaclPresent, nint pDacl, bool bDaclDefaulted);

    /// <summary>
    /// Creates SECURITY_ATTRIBUTES with a NULL DACL (allows all access).
    /// Caller must free with FreeSecurityAttributes.
    /// </summary>
    private static nint CreateEveryoneSecurityAttributes()
    {
        // SECURITY_DESCRIPTOR
        nint sd = Marshal.AllocHGlobal(64); // SECURITY_DESCRIPTOR_MIN_LENGTH = ~20 bytes
        InitializeSecurityDescriptor(sd, 1); // SECURITY_DESCRIPTOR_REVISION
        SetSecurityDescriptorDacl(sd, true, nint.Zero, false); // NULL DACL = allow all

        // SECURITY_ATTRIBUTES struct: { nLength, lpSecurityDescriptor, bInheritHandle }
        nint sa = Marshal.AllocHGlobal(nint.Size * 3);
        Marshal.WriteInt32(sa, 0, nint.Size * 3); // nLength
        Marshal.WriteIntPtr(sa, nint.Size, sd); // lpSecurityDescriptor
        Marshal.WriteInt32(sa, nint.Size * 2, 0); // bInheritHandle = FALSE
        return sa;
    }

    private static void FreeSecurityAttributes(nint sa)
    {
        if (sa == nint.Zero) return;
        nint sd = Marshal.ReadIntPtr(sa, nint.Size);
        if (sd != nint.Zero) Marshal.FreeHGlobal(sd);
        Marshal.FreeHGlobal(sa);
    }

    #endregion
}
