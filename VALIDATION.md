# WiredMirror 0.4.0 validation

The app targets .NET 8 on Windows x64. Packaging retains the hash-pinned iPhoneMirror v1.8.3 engine and its GPL corresponding source.

45 automated checks cover exact phone selection, Trust/readiness, list changes, setup readiness, teardown failures, reconnection, AirPlay identity, Bluetooth source restrictions, portrait/landscape fitting and actual corner rendering, stable-release selection, update URL/digest validation, archive traversal rejection, replacement and rollback preserving the previous installation and extra files.

Native smoke checks load the bundled engine, inspect USB setup, verify exports and attach/detach the preview. The wireless check starts the bundled host and requires readiness. These checks do not fabricate phone frames.

Integrated USB setup installed Apple support and the selected phone's capture filter. Actual USB video and TikTok content appeared in the preview and fullscreen. Repeat capture in the 0.4 code was verified with live portrait, landscape and fullscreen previews, including switching back to the native renderer while video continued. Audio packets reached the native renderer; audible output and synchronisation were not independently verified. An earlier USB restore timeout required a cable reconnect; the stop used for the update test completed successfully. Driver safeguards remain enabled.

The GitHub Actions release build passed. An isolated older-version build downloaded the actual GitHub 0.4.0 release through the app, checked its digest, stopped live USB capture, installed and restarted as 0.4.0. The original 0.3.9 test installation remained in its backup folder. The downloaded app/source hashes matched GitHub's asset digests. Automatic startup checks and the single-instance guard were exercised. The source package also rebuilt independently.

AirPlay setup actually installed receiver-specific TCP/UDP rules from LocalSubnet, and restart/readiness checks passed after competing old receiver copies were closed. Physical AirPlay video, two-phone switching and the Samsung TV path still need hardware testing. Router isolation, VPNs and firewall policy can prevent discovery. Receiver readiness alone does not prove an iPhone can discover or stream to it.

Forced orientation changes the PC preview, not another app's layout on the phone. BGRA conversion may cost additional processing time. Incoming frame rate and PC decode timing do not measure total/input delay. Zero lag, every iPhone/iOS version and universal TikTok compatibility are not claimed. Omitted content is not recreated.

GitHub's asset digest protects integrity relative to its authenticated release response. Portable builds are not code-signed; Windows may display SmartScreen or require administrator approval for USB/network setup.
