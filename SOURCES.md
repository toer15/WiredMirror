# Source and attribution

WiredMirror 0.4.0 is GPL-3.0-only. Application/helper code and tests are in `src`
and `tests`; `build.ps1` and `package.ps1` build the supplied packages.

The native USB/AirPlay capture components are unmodified iPhoneMirror binaries:

- Project: https://github.com/RayrenSX/iPhoneMirror
- Release: https://github.com/RayrenSX/iPhoneMirror/releases/tag/v1.8.3
- Revision: `1270c443a29457751cadd20f6d7bbe1ce213b9db`
- Release archive SHA-256:
  `97c5ded39a9437069cb24fd4ee59fe9775c1ebcf2700e3fd6305c9e424396bf0`

`WiredMirror.Setup` compiles upstream GPL driver-installation services directly
from the included source tree. It replaces the upstream UI with a JSON-lines
protocol and the in-app setup window. The English localisation adapter reads
the original messages; a .NET 8 compatibility adapter loads only the X.509 DER
certificate obtained from WinVerifyTrust's verified signer chain. Upstream Apple
package-signature validation, payload hashes, per-device targeting, backup and
elevation protections remain in place.

The corresponding-source package includes `iPhoneMirror-v1.8.3-source.zip`, the
unmodified vendor tree with the `vendor/iPhoneMirror/` prefix. Its SHA-256 is
`79e480c5ca753bb6441ded5f8d72922c15b51b4b1fe6f31d4e6a93a6257d1200`.
The build extracts and verifies this archive when the vendor tree is absent.
Original upstream source is also available at
https://github.com/RayrenSX/iPhoneMirror/archive/refs/tags/v1.8.3.zip .

Native third-party notices and driver dependencies are retained in
`THIRD_PARTY_NOTICES.md`, `DRIVER_DEPENDENCIES.md` and `licenses/`.
AirPlay/FFmpeg notices, component hashes and exact source references are retained
under `Wireless/licenses/`. The optional UxPlay runtime is not distributed here.
Apple's proprietary support package is downloaded from Apple during setup and
is not bundled in the release ZIP. The .NET runtime is not bundled.

WiredMirror is independent of Apple, TikTok and Samsung.
