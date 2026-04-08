# Project Memory

This file captures project learnings that persist across agent sessions. It serves as
institutional memory so agents don't repeat mistakes or rediscover established patterns.

**How to update this file:** When you learn something that future agents should know —
a pattern that works well, a mistake to avoid, a key decision — add it to the appropriate
section below. Keep entries concise (one or two lines). Include dates for decisions.
Do not remove entries unless they are explicitly obsolete.

---

## Patterns That Work

- **Callback injection in App layer:** ViewModels receive `Func<>` / `Action<>` callbacks instead of direct service references — keeps MVVM layer decoupled from `AppOrchestrator`. See `App.xaml.cs` lines 86-100.
- **Testable subclass for ConfigService:** Protected constructor accepts custom config directory, enabling unit tests with temp folders. See `ConfigServiceTests.cs`.
- **Atomic config writes:** `ConfigService.Save()` writes to `.tmp` then moves — prevents corruption on crash.
- **BGRA throughout the frame pipeline:** FFmpeg outputs BGRA, `FrameBuffer` stores BGRA, Media Foundation consumes BGRA — no format conversion needed anywhere.

## Patterns to Avoid

- **Don't instantiate `StreamReader` directly in orchestrator** — currently hardcoded (`new StreamReader()` in `AppOrchestrator`). Use a factory or DI registration when refactoring.
- **Don't assume frame dimensions match** — VCam layer has hardcoded 1280×720; no validation that `FrameBuffer` frames match. A mismatch could cause buffer overflows.

## Key Decisions

Record important architectural and process decisions with rationale. Link to ADRs when
they exist.

- **2026-04-08:** Three-layer architecture — `Core` (protocol-agnostic stream decoding, config, reconnection), `VCam` (Windows Media Foundation COM interop), `App` (WPF MVVM shell). Core has no UI or Windows-specific APIs.
- **2026-04-08:** FFmpeg as external subprocess — chosen over in-process libraries for reliability, hardware decode support, and broad codec coverage. Must be on `PATH`; checked at startup via `FfmpegChecker`.
- **2026-04-08:** `MFCreateVirtualCamera` (Win11 22H2+) for user-mode virtual camera — no kernel driver, no admin rights, per-user HKCU registration, session lifetime (auto-removes on exit).
- **2026-04-08:** Single-frame overwrite `FrameBuffer` (not a queue) — prioritizes low latency over frame completeness. Slow consumers lose frames; no backpressure.
- **2026-04-08:** `ConfigVersion` field in `AppConfig` reserved for future schema migrations but no migration logic implemented yet.

## Common Mistakes

Things agents frequently get wrong. Check this section before starting work.

- **Windows 11 22H2+ is a hard requirement.** `MFCreateVirtualCamera` does not exist on Windows 10 or earlier Win11 builds. VCam COM tests cannot run in CI without this OS version.
- **FFmpeg must be installed separately.** It is not bundled — the app checks for it at startup and shows instructions if missing.
- **VCam tests skip actual COM/MF calls.** The 7 VCam tests validate state management only; they do not create a real virtual camera device.

## Reviewer Feedback

Persistent feedback from code reviews that applies broadly, not just to a single PR.

- *(No entries yet — add broadly applicable review feedback here)*
