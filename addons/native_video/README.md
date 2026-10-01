# Native Video for Godot

Hardware-accelerated MP4/MOV video playback (H.264/HEVC) for Godot 4 on
macOS, Windows, and Linux, using the platform media stack as a **hardware
decoder only** — never in player mode — and presenting decoded frames to the
GPU through either zero-copy import or an explicit, counted CPU-copy path.
Drop-in compatible with Godot's `VideoStreamPlayer`.

## Architecture

The extension is built around a Godot-independent **core** that drives the
OS media framework in Decoder mode: `AVAssetReader` on macOS, a synchronous
`IMFSourceReader` on Windows. Either way, decode itself runs on a bounded
shared worker pool, not one thread per video; on Windows, COM is confined to
a single dedicated MTA (multi-threaded apartment) executor thread the
backend owns for the life of the open media, since Godot's own main thread
runs in an STA that Media Foundation object creation can't use directly.
The core owns the master clock, the frame queue, sync, and decode-ahead, so
nothing implicit in the OS framework owns timing.

On macOS, frames reach the GPU with no CPU copy: the hardware-decoded NV12
surface is imported via `RenderingDevice.texture_create_from_extension` as a
`Texture2DRD` and converted NV12→RGB in a single Metal compute pass into an
engine-owned texture returned from `VideoStreamPlayback._get_texture()`.
Godot never samples the decoder surface directly. On Windows the same
zero-copy shape is available when Godot runs its D3D12 rendering driver —
see [Windows present path](#windows-present-path) below for the one case
where it falls back to a CPU copy instead.

Highlights:

- **Decoder mode, engine-owned clock** — audio-master sync with a monotonic
  fallback for silent clips; drop-late / hold-early present policy recovers
  from decode hiccups without permanent drift.
- **Explicit present pipelines** — zero-copy on macOS and Windows D3D12;
  counted, bounded CPU-copy on Windows Vulkan and the initial Linux Vulkan
  backend.
- **Hardware decode via the OS** — AVFoundation on macOS, Media Foundation
  on Windows, GStreamer with VA-API/NVDEC on Linux; low CPU and battery, no
  bundled codec libraries.
- **Adaptive scrubbing** — keyframe-only while dragging fast, exact frame on
  settle.
- **Shared decode-worker pool** — many videos in one scene without a thread
  per video; per-stream frame order preserved.

## Requirements

- **macOS** (Apple Silicon or Intel; the extension links AVFoundation,
  CoreMedia, CoreVideo, and Metal directly), **Windows** (the extension links
  Media Foundation and D3D11/D3D12 directly), or **x86_64 GNU/Linux** with
  GStreamer 1.22+ and the appropriate hardware decoder plugin.
- **Godot 4.6+**, **Forward+ or Mobile renderer** (the RenderingDevice
  renderers). The Compatibility/OpenGL renderer is not supported on any
  platform — there is no OpenGL texture presentation path.
- **Zig 0.16.0** to build from source (see [mise.toml](mise.toml)). Other
  Zig versions are not supported: the extension is built against
  [gdzig](https://github.com/gdzig/gdzig), a pre-1.0 binding generator
  pinned to a specific commit and Zig version.

## Releases

Each GitHub Release attaches a `native_video-<tag>.zip` containing an
`addons/native_video/` folder — a self-contained Godot addon: drop it into
a project's `addons/` directory and Godot picks up `native_video.gdextension`
automatically. It bundles:

| Platform | Arch | Files |
| --- | --- | --- |
| macOS | universal (arm64 + x86_64, via `lipo`) | `libnative_video.macos.debug.dylib`, `libnative_video.macos.release.dylib` |
| Windows | x86_64 | `native_video.windows.debug.x86_64.dll`, `native_video.windows.release.x86_64.dll` |
| Windows | x86 (32-bit) | `native_video.windows.debug.x86_32.dll`, `native_video.windows.release.x86_32.dll` |
| Windows | arm64 | `native_video.windows.debug.arm64.dll`, `native_video.windows.release.arm64.dll` |
| Linux | x86_64 | `libnative_video.linux.debug.x86_64.so`, `libnative_video.linux.release.x86_64.so` |

Both a debug and a release binary ship for every platform/arch, matching
Godot's `template_debug`/`template_release` export split — the `.gdextension`
routes each automatically based on how the exported project was built.
Debug binaries are built with `-Doptimize=ReleaseSafe` (safety checks kept
in, unstripped); release binaries with `-Doptimize=ReleaseFast` (stripped).

**iOS** builds and links (produces a valid iOS Mach-O `dylib`), but is not
shipped in releases: Godot's iOS export conventionally expects a static
library / `.xcframework` rather than a plain dylib, and only link-time (not
on-device runtime) has been verified. Follow-up work; not currently
tracked for a specific release.

## Windows present path

Windows can run Godot's Vulkan or D3D12 `RenderingDevice` driver, and this
build links two import paths that hand decoded frames to Godot. The choice
is made once, automatically, at startup, from the active RD driver name and
the running Godot version — never a build-time variant, never a
try-and-fail probe:

| Renderer driver | Import path | Zero-copy | Notes |
| --- | --- | --- | --- |
| `d3d12` | D3D12 shared-handle import | Yes | Shared NT handles + a shared D3D11/D3D12 fence hand the decoder's D3D11 texture to Godot's D3D12 device; a plane-split compute pass splits NV12/P010 into luma/chroma views. The selection code requires Godot 4.5+ for this path (`texture_create_from_extension` isn't implemented for the D3D12 driver before that); this extension's Godot 4.6+ floor already clears that bar. |
| `vulkan` (stock) or anything else | CPU-copy fallback | No | A zero-copy Vulkan path (DXGI shared handles into `VK_KHR_external_memory_win32`) is not built here — it was hard-disabled upstream even before this Zig port, because Godot's `texture_create_from_extension` mis-binds NV12 plane aspects on the Vulkan driver. Adds one GPU→CPU readback per frame before the texture reaches Godot. |

External absolute drive paths are opened in place. On Windows, `C:\...` and
`C:/...` inputs are normalized to a UTF-16 `file:///C:/...` URI without
percent-encoding Unicode or spaces; no copy, link, or ASCII alias is created.

If the D3D12 path is selected but fails to come up — no D3D12 device to
bind, or the shared handles can't be opened across adapters — the extension
degrades to the CPU-copy path automatically, once, and stays there for the
rest of that playback session; it never retries the zero-copy path per
frame. Call `get_cpu_copy_count()` on a playback instance to see how many
frames took the CPU-copy path this session (always 0 on macOS, since Metal
import is always zero-copy there).

**Recommendation:** run Godot with the D3D12 rendering driver for zero-copy
present. Stock Vulkan still gets full hardware decode, just with a per-frame
GPU→CPU readback added before present.

## Linux backend (GStreamer)

Linux uses GStreamer as a demux/decoder graph with bounded, unsynchronised
`appsink` outputs. GStreamer never renders video or audio and is not the master
clock: the existing Zig core continues to own scheduling, decode-ahead, A/V
sync, seeking and frame selection. Video and audio use independent graphs so a
mid-stream audio-track change can seek audio without disturbing video.

The initial production path targets x86_64 Linux, Godot's Vulkan
RenderingDevice (Forward+ or Mobile), H.264/HEVC in MP4/MOV/M4V, and NV12/P010
decoder output. Decoded system-memory planes are copied into two reused staging
buffers and uploaded to Godot textures. This is an explicit, bounded CPU-copy
presenter; every presented frame increments `get_cpu_copy_count()`. DMA-BUF
import is not yet enabled because Godot's public RenderingDevice extension API
does not expose enough Vulkan image-import synchronization to make modifier and
fence handling portable.

Distributed x86_64 Linux binaries use Zig's architecture baseline (SSE2) and
do not require AVX, AVX2, or AVX-512. Release CI disassembles both `.so` files
and rejects non-baseline vector instructions, preventing a build-host CPU from
silently becoming a deployment requirement.

Development packages (Debian 13 package names):

```bash
sudo apt install pkg-config build-essential libgstreamer1.0-dev \
  libgstreamer-plugins-base1.0-dev
```

Runtime packages for the ordinary open-source desktop stack:

```bash
sudo apt install gstreamer1.0-plugins-base gstreamer1.0-plugins-good \
  gstreamer1.0-plugins-bad gstreamer1.0-libav gstreamer1.0-tools \
  gstreamer1.0-vaapi
```

- Intel and AMD: a working VA-API driver plus the GStreamer `va`/VA-API
  decoder plugin is required. Verify it with `gst-inspect-1.0 vah264dec` or
  `gst-inspect-1.0 vah265dec`.
- NVIDIA: install the distribution's GStreamer NVDEC/nvcodec plugin matching
  the proprietary driver. Availability and package names vary by distribution.
- Debian 13 is the verified build baseline. Ubuntu 24.04 and newer are
  supported when they provide GStreamer 1.22+ and the same plugin/API set.
- Wayland and X11 do not enter the decoder graph; each still needs separate
  Vulkan/driver validation. Compatibility/OpenGL remains unsupported.

The selected decoder, negotiated pixel format, presenter, and fallback reason
are reported in Godot diagnostics. Software decoding is never selected
silently: if GStreamer chooses `avdec_h264`/`avdec_h265`, opening fails. For
diagnostics or unsupported machines it can be explicitly enabled with
`GODOT_NATIVE_VIDEO_ALLOW_SOFTWARE=1`; the fallback is then logged as a warning
and is not advertised as hardware acceleration.

Before deployment, collect a path-free capability report on the target Linux
machine. The strict form exits with code 2 when no supported hardware decoder
factory is visible:

```bash
bash tools/linux_diagnostics.sh
bash tools/linux_diagnostics.sh --require-hardware
```

Run it independently under Wayland and X11 when both sessions are supported by
the product. A complete hardware qualification records the session type,
GPU/driver, selected GStreamer factory, Godot renderer, H.264/HEVC result,
audio result, seek result, lifecycle stress result, and CPU-copy count.

The extension dynamically links GStreamer under its LGPL license; GStreamer,
codec plugins and GPU drivers are not redistributed by this repository. A
distributor must separately audit the licenses of the exact runtime plugins it
ships (notably distro-provided FFmpeg/libav plugin builds).

## Runtime source switching

External MP4/MOV/M4V sources follow the normal `VideoStreamPlayer` lifecycle on
Godot 4.7.x. `stop()` synchronously cancels and joins any in-flight decode
slice, pauses the native video/audio pipeline, drains queued audio and video,
resets the source to position 0, and detaches the displayed frame. Calling it
more than once is safe. A later `play()` resumes the same source from 0.

Setting `stream = null`, assigning a different stream, freeing the player with
`queue_free()`, and replacing a scene while playback is active are supported.
Reassigning a stream resource returned from Godot's resource cache is also
safe, including when its 64-bit ObjectID has the high bit set.
Destruction first retires presented frame handles, then cancels the decoder,
waits for its worker, detaches native callbacks/readers, and releases each
backend handle exactly once. No plugin-side audio worker or audio-bus reference
survives disposal. The process-wide bounded scheduler threads remain idle for
reuse; they do not retain a disposed stream or decoder.

The lifecycle regression harness accepts two external paths and performs 50
stop/unbind/rebind/replay cycles, including a second simultaneous decoder,
active-player deletion and real `SceneTree.change_scene_to_packed()` replacement:

```bash
godot --path project --script res://lifecycle_stress.gd -- /path/a.mp4 /path/b.mp4
zig build lifecycle-smoke -- /path/a.mp4 /path/b.mp4 --cycles=50
```

On Windows, quote paths containing spaces. Headless runs validate decoder,
audio, worker and ownership teardown but cannot validate texture presentation;
use Forward+ or Mobile for the full presentation path. The only intentional
long-lived native objects are the bounded shared scheduler threads described
above.

## Known limitations

- **Headless mode (`--headless`)**: presentation is disabled because there
  is no RenderingDevice. Decode, audio mixing, the master clock, and
  playback state machines keep running normally, and end-of-stream is
  reached. No texture output is available.
- **Compatibility/OpenGL renderer**: not supported on any platform — no
  CPU present path.
- **Linux DMA-BUF zero-copy**: not available in the initial Linux backend;
  Vulkan presentation uses the counted CPU-copy upload described above.
- **Vulkan zero-copy on Windows**: not available. A DXGI-shared-handle path
  into Vulkan was hard-disabled upstream (a Godot `texture_create_from_extension`
  bug mis-binds NV12 plane aspects on the Vulkan driver) and isn't built into
  this extension, so Vulkan on Windows always takes the CPU-copy fallback
  described above.
- **HEVC on Windows** depends on a decoder MFT being registered on the
  machine (typically the "HEVC Video Extensions" package) — not present on
  every Windows installation.
- **Single precision only (prebuilt binaries)**: the released libraries are
  built for the standard single-precision Godot. They will not load into a
  double-precision (`precision=double`) Godot build — the two disagree on the
  in-memory layout of core types. Double-precision users build from source
  with `-Dprecision=double` (see [Building from source](#building-from-source)).

## Diagnostics

Every log line the extension emits — from the main thread or a decode
worker, on either backend — is mirrored to Godot's own reporting surface as
well as stderr: errors and warnings show up in the editor's Output panel
(`push_error` / `push_warning`), not just a console window, and info/debug
lines print there too. A failed `load()` (bad path, backend open failure,
unsupported content) is reported the same way with the underlying error name,
instead of failing silently. Normal diagnostics do not expose the full media
path.

`get_cpu_copy_count()` on a live `VideoStreamPlayback` reports how many
frames in this session have gone through a CPU-copy presenter — the Windows
Vulkan fallback or the current Linux Vulkan path.

## Scope

- **Codecs:** H.264, HEVC (Main and Main10).
- **Containers:** MP4, MOV — the loader registers `.mp4`, `.mov`, and
  `.m4v`.
- **Pixel formats:** NV12 (8-bit) and 10-bit biplanar — x420 on macOS,
  P010 on Windows — negotiated to match the source's bit depth. The
  conversion shader normalises 10-bit payloads in 16-bit GPU words on both
  platforms.
- **Colorimetry:** BT.709, BT.601, and BT.2020 YCbCr matrices; BT.709 /
  BT.601 / BT.2020 / DCI-P3 primaries; BT.709, PQ (ST 2084 / HDR10), and HLG
  transfer functions; video and full range. Unspecified fields default to
  BT.709, video range.
- **HDR:** PQ/HLG clips tone-map to watchable SDR by default, or output
  scene-linear HDR (RGBA16F, 1.0 = 203-nit Reference White per BT.2408) when
  the stream's `output_mode` is set to HDR.
- **Audio:** AAC, decoded to interleaved float32 PCM. Mono, stereo, and 5.1
  sources are channel-mixed to the clip's canonical output format.
- **Multi-track audio:** supported — enumerate tracks with
  `NativeVideoStream.get_audio_tracks()` and select one via
  `VideoStreamPlayer.audio_track`, either before `play()` or mid-playback.

Beyond this scope, the extension will attempt to play anything the OS media
framework can decode, as long as it arrives in one of the registered
container extensions above. Such content may well work, but only the matrix
above is tested and contractually supported.

**Out of scope:** VP9/AV1. **Known limitation:** mixed-sample-rate clips —
the first audio track's sample rate wins, and a mid-stream switch to a
track with a differing rate is refused.

## Building from source

1. Install Zig 0.16.0 (`mise install` picks it up from
   [mise.toml](mise.toml), or install it directly — the system Zig on most
   machines will be a newer, incompatible version).

   The pinned `gdzig` bindgen currently expects Godot 4.6's extension API
   JSON. A library generated against 4.6 is compatible with and has been
   smoke-tested in Godot 4.7.1, but passing a 4.7 executable through
   `-Dgodot-path` makes this bindgen revision fail while parsing the newer
   JSON. For a 4.7 runtime, omit `-Dgodot-path` (the build downloads Godot
   4.6 for bindgen) or point it at a 4.6.x executable.

   To build bindings with 4.6 but run integration checks with an installed
   4.7 executable, provide both paths separately:

   ```bash
   zig build lifecycle-smoke \
     -Dgodot-path=/path/to/Godot-4.6 \
     -Druntime-godot-path=/path/to/Godot-4.7 -- /path/a.mp4 /path/b.mp4
   ```

2. Build the extension:

   ```bash
   zig build
   ```

   This compiles `libnative_video.dylib` (macOS), `native_video.dll`
   (Windows), or `libnative_video.so` (Linux) and installs it to `project/lib/`, where
   `project/native_video.gdextension` expects it. `build.zig` is the entire
   build entry point. Apple/Windows dependencies are system frameworks;
   Linux additionally resolves the documented GStreamer development packages
   through `pkg-config`.

   Builds default to a stripped `ReleaseFast` binary (~380 KB on macOS
   arm64; Windows binary size hasn't been separately measured). Pass
   `-Doptimize=Debug` for a debug build, or `-Doptimize=ReleaseSmall` to
   trade some speed for an even smaller library.

   To target a double-precision Godot build, add `-Dprecision=double`
   (default is `float`). The precision must match the Godot the extension
   loads into; a mismatch is a memory-layout error, not a graceful failure.

3. Run the core unit tests (no Godot needed):

   ```bash
   zig build test
   ```

   Linux can additionally run the standalone decode/seek/reopen harness under
   Valgrind. Only definite and indirect leaks fail the step; install
   `valgrind` first and explicitly enable software decoding only on machines
   without a hardware plugin:

   ```bash
   GODOT_NATIVE_VIDEO_ALLOW_SOFTWARE=1 zig build leak-check
   ```

4. Run the demo project (builds the extension, launches Godot, and opens
   the interactive playback UI against `project/synthetic.mp4` or a clip
   passed after `--`):

   ```bash
   zig build run
   ```

   For a headless pass/fail check instead — loads a clip, plays it, polls
   for a presented video texture, does a pixel-content sanity check, and
   quits with exit 0 (PASS) / 1 (FAIL) — use the dedicated smoke step:

   ```bash
   zig build smoke
   ```

   Two additional integration steps exercise lifecycle teardown and load the
   release-style addon directory from a generated clean Godot project:

   ```bash
   zig build lifecycle-smoke -- /path/a.mp4 /path/b.mp4 --cycles=50
   zig build package-smoke
   ```

   (equivalent to `zig build run -- --smoke`, but headless). Both steps
   download a matching Godot build for bindgen/running if `GODOT_PATH` or
   `-Dgodot-path=/path/to/Godot` isn't set.

## Project layout

- `src/core/` — engine-independent core (clock, frame queue, scrubber,
  present selector, decode scheduler, backend interface, color math). Pure
  Zig, no Godot, AVFoundation, or Media Foundation imports; this is what
  `zig build test` exercises.
- `src/avf/` — the AVFoundation backend (macOS): `avf_shim.m`/`avf_shim.h`
  (a C-ABI Objective-C shim) plus the Zig glue that drives it through the
  core `Backend` interface. `decode_smoke.zig` is a standalone CLI harness
  that pumps the backend without Godot, for isolating decode-side bugs.
- `src/mf/` — the Media Foundation backend (Windows): a synchronous
  `IMFSourceReader` driven through the core `Backend` interface, the
  dedicated MTA `ComExecutor` thread, and the hand-written D3D11/D3D12/DXGI
  bindings under `src/mf/win/`. `decode_smoke.zig` mirrors the AVFoundation
  harness for isolating decode-side bugs without Godot.
- `src/linux/` — the Linux GStreamer backend, isolated C ABI shim, and
  standalone decode smoke.
- `src/godot/` — [gdzig](https://github.com/gdzig/gdzig) glue:
  `NativeVideoStream` / `NativeVideoStreamPlayback` registration, the
  present pipeline that runs the NV12→RGB compute pass, and the surface
  importers it selects between — Metal on macOS; on Windows, the pure
  `importer_selector` function plus the D3D12 zero-copy and CPU-copy
  importers it chooses between; on Linux, the counted system-memory Vulkan
  presenter.
- `project/` — the example/verification Godot project and dev harness.
  `main.gd`/`main.tscn` are a single entry point with two modes selected by
  CLI flag: interactive playback UI (`zig build run`, the default) or
  headless pass/fail verification (`zig build smoke`, `--smoke`).
- `addon/` — the shipped `.gdextension` template packaged into release
  zips (see [Releases](#releases) below). Not used by `zig build`; the dev
  harness in `project/` has its own `native_video.gdextension` pointing at
  local build output.

For the historical record of how this implementation was chosen —
benchmarks and rationale — see [EVALUATION.md](EVALUATION.md).

## License

MIT — see [LICENSE](LICENSE).
