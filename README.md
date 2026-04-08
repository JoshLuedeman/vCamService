# 📷 vCamService

[![Platform](https://img.shields.io/badge/platform-Windows%2011%2022H2%2B-blue?logo=windows)](https://www.microsoft.com/en-us/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen)]()

**Turn any RTSP or MJPEG IP camera into a virtual webcam — no drivers, no admin rights required.**

vCamService ingests live streams from network cameras and re-exposes them as a standard Windows webcam device. Once running, open Zoom, Teams, OBS, Slack, Google Meet, or any other app and simply select **"vCamService Camera"** from the camera picker — exactly as you would a physical webcam.

> **Who is this for?** Security camera owners, home-lab enthusiasts, streamers, and anyone who wants to route an IP camera feed into video call software without buying dedicated hardware or dealing with kernel drivers.

---

## ✨ Features

- 🎥 **RTSP & MJPEG support** — H.264, H.265, and MJPEG streams from any IP camera
- 🪟 **Zero-driver virtual camera** — uses the Windows 11 `MFCreateVirtualCamera` API; registers per-user under HKCU, no elevation needed
- 🔄 **Automatic reconnection** — exponential backoff with jitter; streams recover silently after network interruption
- 🖼️ **Live preview** — 10 fps in-app preview of the active stream
- 🗂️ **Multiple stream profiles** — configure as many cameras as you like, switch the active feed in one click
- 📡 **RTSP transport selection** — TCP or UDP per stream (TCP default for firewall-friendly operation)
- 🔔 **System tray** — minimize to tray; full control from the tray icon
- 📊 **Status bar** — real-time CPU and memory readout
- 💾 **Persistent config** — streams and preferences saved to `%AppData%\vCamService\config.json`
- 📝 **Structured logging** — rolling daily logs via Serilog
- 📦 **MSI installer** — single self-contained executable, no .NET install required on the target machine

---

## 🖼️ Screenshots

> _Screenshots coming soon. The UI features a stream list, live preview panel, and status bar — all in a clean WPF window with system-tray support._

```
┌─────────────────────────────────────────────────────┐
│  vCamService                              _ □ ✕      │
├─────────────────────────────────────────────────────┤
│  Streams                    │  Preview               │
│  ┌─────────────────────┐    │  ┌─────────────────┐  │
│  │ ● Front Door   [▶]  │    │  │                 │  │
│  │   Back Yard    [▶]  │    │  │   Live preview  │  │
│  │ + Add stream…       │    │  │                 │  │
│  └─────────────────────┘    │  └─────────────────┘  │
├─────────────────────────────────────────────────────┤
│  🟢 Camera: vCamService Camera  │ CPU 2%  RAM 38 MB  │
└─────────────────────────────────────────────────────┘
```

---

## 🖥️ Requirements

| Requirement | Minimum |
|---|---|
| **OS** | Windows 11 version 22H2 (build 22621) or later |
| **.NET runtime** | .NET 8 — bundled in the MSI (self-contained) |
| **FFmpeg** | Must be on `PATH` — install with `winget install ffmpeg` |

> ⚠️ **Windows 11 22H2+ is a hard requirement.** The `MFCreateVirtualCamera` API used to create user-mode virtual cameras was introduced in that release and is not available on Windows 10 or earlier Windows 11 builds.

---

## 📥 Installation

### Option A — MSI Installer (recommended)

1. Download `vCamService-Setup.msi` from the [Releases](../../releases) page.
2. Run the installer — no elevation needed, installs per-user.
3. Install FFmpeg if you haven't already:
   ```powershell
   winget install ffmpeg
   ```
4. Launch **vCamService** from the Start Menu.

### Option B — Build from Source

See [Building from Source](#building-from-source) below.

---

## 🚀 Usage

### 1 — Add a stream

Click **"+ Add stream…"** and fill in:

| Field | Description |
|---|---|
| **Name** | Friendly label, e.g. `Front Door` |
| **URL** | Full stream URL, e.g. `rtsp://192.168.1.100:554/stream1` or `http://192.168.1.101/video` |
| **Protocol** | `rtsp` or `mjpeg` |
| **Resolution / FPS** | Target decode resolution (default 1280 × 720 @ 30 fps) |
| **RTSP transport** | `tcp` (recommended) or `udp` |

### 2 — Activate a stream

Click the **▶** button next to any stream to make it the active source feeding the virtual camera. The status bar turns green and shows **"vCamService Camera"**.

### 3 — Select the camera in your app

Open Zoom, Teams, OBS, or any webcam-aware application. In the camera selector, choose:

```
vCamService Camera
```

That's it — the live feed from your IP camera will appear.

### 4 — Minimize to tray

Close the window to minimize to the system tray. The virtual camera keeps running. Right-click the tray icon to restore, switch streams, or exit.

---

## 🏗️ Architecture

vCamService is structured as three projects with a clear layering boundary:

```
┌─────────────────────────────────────────────────────────┐
│                     vCamService.App                     │
│  ┌─────────────┐  ┌────────────────┐  ┌─────────────┐  │
│  │  WPF UI     │  │ AppOrchestrator│  │  TrayIcon   │  │
│  │  (MVVM)     │  │  + DI Host     │  │  Service    │  │
│  └──────┬──────┘  └───────┬────────┘  └─────────────┘  │
└─────────┼─────────────────┼───────────────────────────┘
          │                 │
┌─────────▼─────────────────▼───────────────────────────┐
│                  vCamService.Core                       │
│  ┌───────────┐  ┌───────────┐  ┌────────────────────┐  │
│  │StreamReader│  │FrameBuffer│  │ ReconnectManager   │  │
│  │(FFmpeg sub)│  │(overwrite)│  │ (exp. backoff)     │  │
│  └───────────┘  └───────────┘  └────────────────────┘  │
└───────────────────────────────────────────────────────┘
          │
┌─────────▼───────────────────────────────────────────┐
│                  vCamService.VCam                    │
│  ┌──────────────────┐  ┌────────────────────────┐   │
│  │VirtualCameraSource│  │ VirtualCameraManager   │   │
│  │(IMFMediaSource)   │  │ (MFCreateVirtualCamera)│   │
│  └──────────────────┘  └────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

### Layer responsibilities

| Project | Role |
|---|---|
| **`vCamService.Core`** | Protocol-agnostic stream decoding (FFmpeg subprocess → raw BGRA frames), config persistence, and reconnection logic. No UI, no Windows-specific APIs. |
| **`vCamService.VCam`** | Wraps the Windows Media Foundation `MFCreateVirtualCamera` COM API. Exposes `VirtualCameraManager` (start/stop/send-frame) and `VirtualCameraSource` (the `IMFMediaSource` implementation). Contains 18 hand-written COM interface definitions and P/Invoke declarations. |
| **`vCamService.App`** | WPF + MVVM shell. Hosts the DI container (`Microsoft.Extensions.Hosting`), `AppOrchestrator`, tray icon, and resource monitor. |

### Threading model

| Thread | Role |
|---|---|
| **Main thread** | WPF dispatcher — all UI mutations |
| **Thread R1…RN** | One `StreamReader` per configured stream; reads FFmpeg stdout and writes BGRA frames to its `FrameBuffer` |
| **Thread F** | Frame feeder — reads the active `FrameBuffer` and pushes frames to `VirtualCameraManager` at the configured FPS |
| **Timer T** | Preview refresh — `DispatcherTimer` at 10 fps on the UI thread |
| **Timer S** | Status bar — CPU/RAM refresh every 2 seconds |

### Frame pipeline

```
IP Camera
  │  (RTSP / MJPEG / TCP / UDP)
  ▼
FFmpeg subprocess
  │  stdout: raw BGRA bytes  (width × height × 4)
  ▼
FrameBuffer  (lock-free overwrite — always newest frame)
  ▼
Frame feeder thread  (@ VCamFps)
  ▼
VirtualCameraManager.SendFrame()
  ▼
IMFVirtualCamera  (Windows Media Foundation session camera)
  ▼
Zoom / Teams / OBS / any webcam consumer
```

BGRA is used throughout: FFmpeg outputs it directly (`-pix_fmt bgra`), Media Foundation consumes it natively, and WPF renders it without conversion.

---

## 🔧 Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Windows 11 22H2+ for running/testing (compilation works on macOS/Linux)
- [WiX Toolset v4](https://wixtoolset.org/) for building the MSI (Windows only)

### Build & test

```powershell
# Clone
git clone https://github.com/your-username/vCamService.git
cd vCamService

# Build all projects
dotnet build

# Run tests  (25 tests: 18 Core + 7 VCam)
dotnet test
```

### Build the MSI installer

Run on Windows. The script publishes a self-contained `win-x64` single-file executable, then invokes WiX to produce the MSI.

```powershell
.\build-installer.ps1 -Version "1.2.0"
```

Output: `src/vCamService.Installer/bin/Release/vCamService-Setup.msi`

| Parameter | Default | Description |
|---|---|---|
| `-Version` | `1.0.0` | Product version embedded in the MSI |
| `-Configuration` | `Release` | Build configuration |

> The published app is `--self-contained true -r win-x64 -p:PublishSingleFile=true`, so no separate .NET runtime installation is required on the target machine.

---

## ⚙️ Configuration

Config is stored at `%AppData%\vCamService\config.json` and written on every change.
The file is created automatically on first run.

### Full schema

```jsonc
{
  "configVersion": 1,

  // ID of the stream currently feeding the virtual camera (null = none active)
  "activeStreamId": "a1b2c3d4-...",

  // Virtual camera output resolution and frame rate
  "vCamWidth":  1280,
  "vCamHeight": 720,
  "vCamFps":    30,

  // Minimize to system tray on window close
  "minimizeToTray": true,

  "streams": [
    {
      // Auto-generated UUID — do not edit
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",

      "name": "Front Door",
      "url":  "rtsp://192.168.1.100:554/stream1",

      // "rtsp" or "mjpeg"
      "protocol": "rtsp",

      // Requested decode resolution (FFmpeg will scale if needed)
      "width":  1280,
      "height": 720,
      "fps":    30,

      // RTSP only: "tcp" (recommended) or "udp"
      "rtspTransport": "tcp",

      // Set false to keep the stream configured but prevent it from starting
      "enabled": true
    }
  ]
}
```

### Key settings

| Setting | Default | Notes |
|---|---|---|
| `vCamWidth` / `vCamHeight` | `1280` / `720` | Output resolution of the virtual camera device |
| `vCamFps` | `30` | Frame rate fed to Media Foundation |
| `minimizeToTray` | `true` | Window close sends to tray instead of exiting |
| `rtspTransport` | `"tcp"` | Use `"udp"` only if TCP causes issues; UDP is faster but drops frames behind NAT |
| `enabled` | `true` | Disabled streams are skipped at startup |

---

## 📋 Logging

Logs are written to:

```
%AppData%\vCamService\logs\vcamservice-YYYYMMDD.log
```

- Rolling daily files — one file per calendar day
- Old log files are retained according to Serilog's default retention policy
- Log level: `Information` in Release builds; increase to `Debug` or `Verbose` by modifying the Serilog configuration in `App.xaml.cs`

Typical log entries:
```
[12:01:05 INF] AppOrchestrator started. Active stream: Front Door
[12:01:05 INF] StreamReader started: rtsp://192.168.1.100:554/stream1
[12:01:05 INF] VirtualCameraManager started (1280x720 @ 30fps)
[12:03:42 WRN] StreamReader lost connection; reconnecting in 2.1s (attempt 1)
[12:03:44 INF] StreamReader reconnected successfully
```

---

## ⚠️ Known Limitations

| Limitation | Detail |
|---|---|
| **Windows 11 22H2+ only** | `MFCreateVirtualCamera` is not available on Windows 10 or earlier Windows 11 builds (pre-22621). There is no planned workaround. |
| **FFmpeg required** | FFmpeg must be installed separately and available on `PATH`. The app checks for it at startup via `ffmpeg -version` and shows instructions if not found. |
| **No audio** | The virtual camera is video-only. Audio from IP cameras is not captured or forwarded. |
| **Single virtual camera** | Only one virtual device is created (`vCamService Camera`). Multiple simultaneous outputs are not supported. |
| **Session lifetime** | The virtual camera device exists only while the app is running. It is registered fresh on each launch under `HKCU` (no permanent driver installation). |
| **x64 only** | Published as `win-x64`. ARM64 Windows is untested. |

---

## 🤝 Contributing

Contributions are welcome! A few guidelines:

- **Bug reports** — open an issue with the log file attached (`%AppData%\vCamService\logs\`)
- **Pull requests** — keep changes focused; one concern per PR
- **New stream protocols** — implement `IStreamReader` in `vCamService.Core` and wire up in `AppOrchestrator`
- **Tests** — add tests under `tests/` for any new Core or VCam logic

```
vCamService.sln
├── src/
│   ├── vCamService.Core/       ← stream logic, models, config (testable, no UI)
│   ├── vCamService.VCam/       ← Media Foundation COM layer
│   ├── vCamService.App/        ← WPF UI shell
│   └── vCamService.Installer/  ← WiX v4 MSI
└── tests/
    ├── vCamService.Core.Tests/  ← 18 unit tests
    └── vCamService.VCam.Tests/  ← 7 unit tests
```

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

<p align="center">
  Built with ❤️ using .NET 8, Windows Media Foundation, and FFmpeg.
</p>
