# TrueMoon MiniAudioEx Windows x64

Local fork package `TrueMoon.MiniAudioEx 4.0.1-truemoon.1`.
It contains the unchanged MiniAudioEx.NET 4.0.1 managed assemblies and a source-built
Windows x64 miniaudioex.dll with failed-initialization cleanup fixed in the libvorbis adapter.
The original NuGet package is not overwritten or re-signed. Other native RIDs are not included.

Use this package instead of JAJ.Packages.MiniAudioEx, not alongside it. The existing
MiniAudioEx namespaces and library entry points remain unchanged. Windows requires
the MSVC runtime used by the `/MD` build; see `native-build.json` for exact toolchain,
source/archive/patch/native hashes. NuGet framework compatibility does not promise other OS/RIDs.

The patch frees caller-owned OggVorbis_File state and uninitializes the data-source
base when ov_open_callbacks/ov_fopen/ov_wfopen/ov_mopen fail. Required null/empty
arguments are rejected before allocation. Successful cleanup nulls the released pointer.
No public native structure or function signature changes.

The underlying Ogg Vorbis duration API remains unavailable. Device behavior and
audio-module App/DI/ECS integration are separate from this dependency correction.

Source: https://github.com/japajoe/miniaudioex/tree/9f85b56157027cc57c7cd0a13fea7fee3f1e167e
Managed binding: https://www.nuget.org/packages/JAJ.Packages.MiniAudioEx/4.0.1
The exact patch and build provenance are included in this package.
See THIRD-PARTY-NOTICES.md and licenses/ for license terms.
