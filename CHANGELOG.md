# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-04-10

### Added
- WPF system tray application for controlling virtual camera streaming
- Virtual camera via Windows Media Foundation Frame Server (COM server)
- MJPEG HTTP stream capture using FFmpeg (ffmpeg + ffprobe)
- Shared memory IPC (memory-mapped file) for zero-copy frame transfer between app and Frame Server
- NV12 native pixel format pipeline for hardware-compatible streaming
- Automatic stream configuration via ffprobe (resolution, frame rate, pixel format)
- COM registration/unregistration for the virtual camera device
- MSI installer with WiX v7 (bundles app, COM server, and FFmpeg)
- CI workflow (build, test, MSI verification on every push/PR)
- Release workflow (build, test, MSI, GitHub Release on version tags)
- 28 unit tests covering SharedFrameBuffer, StreamConfig, and VideoStreamReader parsing
- Serilog logging with daily rolling file sink

### Technical Details
- .NET 10 (preview), WPF, CommunityToolkit.Mvvm
- DirectN for Media Foundation COM interop
- Hand-written flattened COM interfaces for correct CCW vtable layout
- Framework-dependent deployment with COM hosting via comhost.dll
