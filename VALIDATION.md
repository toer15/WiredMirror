# WiredMirror 0.4.0 validation

The app targets .NET 8 on Windows x64. Packaging retains the hash-pinned iPhoneMirror v1.8.3 engine and its GPL corresponding source.

45 automated checks cover exact phone selection, Trust/readiness, list changes, setup readiness, teardown failures, reconnection, AirPlay identity, Bluetooth source restrictions, portrait/landscape fitting and actual corner rendering, stable-release selection, update URL/digest validation, archive traversal rejection, replacement and rollback preserving the previous installation and extra files.

Native smoke checks load the bundled engine, inspect USB setup, verify exports and attach/detach the preview. The wireless check starts the bundled host and requires readiness. These checks do not fabricate phone frames.

During earlier development, integrated USB setup installed Apple support and the selected phone's capture filter. Actual USB video and TikTok content appeared in the preview and fullscreen. Audio packets reached the native renderer; audible output and synchronisation were not independently verified. A later USB restore timeout required a cable reconnect. Driver safeguards remain enabled.

Physical AirPlay video, repeat capture after that USB disconnect, two-phone switching and the Samsung TV path need hardware testing. Router isolation, VPNs, firewall policy and competing receivers can prevent discovery. Receiver readiness alone does not prove an iPhone can discover or stream to it.

Forced orientation changes the PC preview, not another app's layout on the phone. BGRA conversion may cost additional processing time. Incoming frame rate and PC decode timing do not measure total/input delay. Zero lag, every iPhone/iOS version and universal TikTok compatibility are not claimed. Omitted content is not recreated.

GitHub's asset digest protects integrity relative to its authenticated release response. Portable builds are not code-signed; Windows may display SmartScreen or require administrator approval for USB/network setup.
