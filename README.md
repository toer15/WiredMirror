# WiredMirror

A Windows app that receives an iPhone screen over USB or Wi-Fi/AirPlay, with a device picker, central preview, portrait/landscape controls, audio and fullscreen.

**[Download the latest Windows release](https://github.com/toer15/WiredMirror/releases/latest)**

Requires Windows 10/11 x64 and the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). Extract the complete Windows ZIP and open `WiredMirror.exe`. Keep all included files.

## USB

1. Connect the iPhone using a USB data cable. Unlock it.
2. Choose **USB** and the exact phone from **Choose device**. The ID ending distinguishes phones with identical names; no phone is chosen automatically.
3. Open **USB setup > Install needed components**. The app installs Apple support from Apple's official distribution and the capture filter for that chosen phone. Approve Windows' administrator prompt if shown. No ADB is installed.
4. Follow any cable reconnect instruction. Tap **Trust This Computer** on the phone and enter its passcode there when requested.
5. Click **Connect selected**. If the chosen phone still needs unlocking/Trust, the app waits up to two minutes and starts when that exact phone is ready. Stop, a different selection or expiry cancels the request.

With no phone chosen, setup prepares Apple support first. Select the connected phone and run setup again to prepare that phone's capture components. Upstream signature, checksum, per-device targeting, backup and elevation checks are kept. If the phone disappears after stopping capture, reconnect the USB cable.

## Wi-Fi / AirPlay

1. Connect the PC and iPhone to the same trusted Wi-Fi/LAN. Guest network client isolation, VPNs or blocked multicast can prevent discovery.
2. Choose **Wi-Fi / AirPlay**. The app starts its bundled receiver and verifies startup. **AirPlay setup** adds Windows Firewall access for this receiver only, from the local subnet, including networks labelled Public. Use it on a network you trust. It does not change your network profile or disable the firewall.
3. Close other WiredMirror/AirPlay receiver windows if ports 5001/7001 are occupied. Use **Restart AirPlay** after setup or a failed connection.
4. On iPhone, choose **Control Centre > Screen Mirroring > WiredMirror** and approve any phone-side prompt. Once the sender appears in the PC app, choose it and press **Connect selected**. Its screen appears in the central preview.

The app cannot remotely activate Screen Mirroring on an inactive iPhone. Only senders that initiate AirPlay appear in that list. Receiver readiness alone does not establish that the phone can discover or stream over your particular network.

## Orientation and fullscreen

- Choose **Follow phone**, **Portrait / upright**, or either **Landscape** direction. The preference is saved and applies in fullscreen. This rotates the PC preview; phone apps control their own layout. Rotate the phone and unlock its orientation if you want a phone app to rearrange its content.
- **F11** opens fullscreen on the current monitor; **Esc** restores the window. For a Samsung TV, extend the PC desktop over HDMI, move the window to the TV and enter fullscreen. Control phone apps on the iPhone itself.
- Native, 720p, 1080p and software-decoder profiles are available. Status shows incoming FPS, actual decoder mode, PC decode time and audio packet count.
- Follow-phone mode uses the native NV12/D3D11 renderer. Forced orientation uses reusable BGRA buffers and latest-frame sampling, capped at a 1920-pixel edge. It adds conversion work; zero latency is not guaranteed.
- Switching stops the previous source. Reconnect is limited to the exact chosen phone for up to 60 seconds. A connection with no video for 45 seconds is stopped.

## Bluetooth

**Bluetooth** lists real Windows discovery/remembered device records and offers Windows pairing for the selected device. Open iPhone Bluetooth settings to make it discoverable. Remembered devices can be offline. Bluetooth pairing alone does not provide an iPhone screen stream; use USB or AirPlay for video.

## Updates

The app checks stable GitHub releases at startup and announces newer versions on **Check for updates**. Click that button to check manually, then **Download, install and restart**. The app and helper verify the ZIP against GitHub's SHA-256 asset digest. Capture is stopped before installation. The previous installation is retained beside the app as `.backup-<id>` for recovery. Portable settings live in `%LOCALAPPDATA%/WiredMirror/preferences.json` and survive updates.

Use a regular writable folder; updates through linked directories are rejected. Cancelled downloads and failed checks leave the current app unchanged. Builds are not code-signed, so Windows may display SmartScreen.

## Build and release

Requires a .NET 8 or newer SDK, PowerShell and internet access for the pinned native release. The bundled upstream source archive is hash-verified/extracted when `vendor/iPhoneMirror` is absent.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
dotnet run --project tests/WiredMirror.Tests/WiredMirror.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File package.ps1
```

Default build: `outbox/WiredMirror-0.4.0`. Packaging uses a separate `outbox/WiredMirror-0.4.0-release` folder. `WiredMirror.exe --check` verifies native exports, setup inspection and preview attachment. `--check-wireless` also starts and stops the receiver and requires readiness. Neither starts a phone capture. `Setup/WiredMirror.Setup.exe --verify-payload` checks embedded driver hashes.

For a new release, update `Directory.Build.props` and release notes, then push a matching `vX.Y.Z` tag. GitHub Actions builds/tests and uploads Windows and corresponding-source ZIPs plus checksums. Main-branch pushes also build/test. Hosted CI skips interactive device/render checks; run these on a Windows desktop.

## Validation, privacy and licensing

Actual USB video and TikTok content have appeared during development. Physical AirPlay video, audible audio, every iPhone/iOS version and total/input latency are not fully verified. Apps/iOS may omit or alter captured content; this app cannot recreate omitted video. See [VALIDATION.md](VALIDATION.md) for scope and limits.

**Save diagnostics** exports ID endings/readiness without screen content or full phone IDs. Local engine logs are under `%LOCALAPPDATA%/WiredMirror/Logs` and excluded from releases. Inspect diagnostics/logs before sharing them publicly.

GPL-3.0-only. Native binaries are from iPhoneMirror v1.8.3; full corresponding source, notices and pinned hashes are supplied. See [SOURCES.md](SOURCES.md) and [LICENSE](LICENSE). Independent of Apple, TikTok and Samsung.
