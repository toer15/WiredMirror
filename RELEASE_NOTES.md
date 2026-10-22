WiredMirror 0.4.0 adds portrait and landscape preview controls, AirPlay network setup and receiver recovery, and GitHub updates.

- Choose **Follow phone**, **Portrait**, or either **Landscape** direction. The choice persists across launches and works in fullscreen. This rotates the app's preview; phone apps control their own layout.
- **AirPlay setup** adds receiver-only Windows Firewall rules from the local subnet when requested. **Restart AirPlay** restarts the receiver; unavailable ports and failed startup are reported. A single-instance guard prevents new copies from competing for ports.
- **Check for updates** downloads newer stable releases, verifies GitHub's SHA-256 digest, stops mirroring, installs and restarts. The previous installation is kept as a backup. Startup checks announce available versions without interrupting capture.
- USB setup, exact device selection, audio, fullscreen and Bluetooth discovery remain available.

Extract the full Windows ZIP and run `WiredMirror.exe`. Requires Windows 10/11 x64 and the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

For AirPlay, use the same trusted LAN and initiate **Control Centre > Screen Mirroring > WiredMirror** on the iPhone. Choose the active sender in the PC app and connect. Bluetooth pairing alone cannot carry the screen.

Physical AirPlay video and total latency require testing with the actual phone/network. No zero-latency or universal protected-video support is promised. GPL-3.0-only; corresponding source and notices are included.
