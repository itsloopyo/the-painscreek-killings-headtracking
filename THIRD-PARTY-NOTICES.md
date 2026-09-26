# Third-Party Notices

PainscreekHeadTracking bundles, statically links, or credits the third-party components
listed below. Each remains the property of its authors and is used under its own
licence. Where a licence requires the copyright notice, the conditions and the
disclaimer to accompany a binary distribution, the full text is reproduced here
verbatim, and this file ships at the root of every release ZIP we publish.

Nothing in this repository is derived from, or redistributes any part of,
The Painscreek Killings.

| Component | Version | Licence | How it ships |
|-----------|---------|---------|--------------|
| Mono.Cecil | 0.11.5 | MIT | `mod/Mono.Cecil.dll`, `tools/Mono.Cecil.dll`, and the source package at `vendor/mono-cecil/`. Licence at `vendor/mono-cecil/LICENSE` and reproduced below |
| cameraunlock-core | `b4df73a` | MIT | `mod/CameraUnlock.Core.dll` and `mod/CameraUnlock.Core.Unity.dll`. Licence at `licenses/cameraunlock-core-LICENSE.txt` and reproduced below |
| PainscreekHeadTracking | see `CHANGELOG.md` | MIT | `mod/PainscreekHeadTracking.dll`. Licence at `LICENSE` |
| Unity | n/a | n/a | Not bundled; compiled against signature-only stubs, never redistributed |
| OpenTrack | n/a | ISC | Not bundled; UDP wire-protocol interoperability only |

---

## Mono.Cecil

Vendored at `vendor/mono-cecil/` as the upstream NuGet package, shipped in the
installer ZIP and used as the install-time source. Taken from the upstream
release asset untouched and not modified in any way. The installer ZIP carries
the package at `vendor/mono-cecil/`, the upstream licence file beside it at
`vendor/mono-cecil/LICENSE`, and the `lib/net40/Mono.Cecil.dll` extracted from
that package at `mod/Mono.Cecil.dll` (deployed to the game's `Managed/` folder)
and at `tools/Mono.Cecil.dll` (used by the bootstrap patcher at install time).

- Upstream: https://github.com/jbevain/cecil
- Package: https://www.nuget.org/api/v2/package/Mono.Cecil/0.11.5
- Version: 0.11.5
- SHA-256 of `Mono.Cecil.0.11.5.nupkg`: `9cf1706f35b4f209c28da7417608bed7a307621b0f0179c52258af78bc4668d0`

The licence text below carries two copyright holders. Both are reproduced as
upstream states them.

```
Copyright (c) 2008 - 2015 Jb Evain
Copyright (c) 2008 - 2011 Novell, Inc.

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

---

## cameraunlock-core

Git submodule at `cameraunlock-core/`, built into `CameraUnlock.Core.dll` and
`CameraUnlock.Core.Unity.dll`, which ship in the installer ZIP at
`mod/CameraUnlock.Core.dll` and `mod/CameraUnlock.Core.Unity.dll` and are
deployed to the game's `Managed/` folder. It is our own code, but it is MIT
under a different copyright holder from this repository's own LICENSE, so its
notice has to travel with the binaries in their own right. The installer ZIP also carries it verbatim as
`licenses/cameraunlock-core-LICENSE.txt`.

- Upstream: https://github.com/itsloopyo/cameraunlock-core
- Pinned commit: `b4df73a5d8076968fcbf7e4088dd49db11a2684e`

```
MIT License

Copyright (c) 2026 itsloopyo

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## PainscreekHeadTracking

Our own code, MIT under `Copyright (c) 2026 itsloopyo`. The full text ships as
`LICENSE` at the root of the installer ZIP, alongside `PainscreekHeadTracking.dll`
in `mod/`. It is listed here so this file is a complete inventory of every
binary the ZIP redistributes and where each one's notice lives.

---

## Unity

No Unity Technologies code, assembly or asset is contained in this repository or
in any release ZIP. The mod compiles against reference stubs generated from
`cameraunlock-core/csharp/stubs/UnityStubs.cs`, which declares only the type and
member signatures the mod calls, with no implementations. Real Unity assemblies
are read from a developer's own game installation at build time, are never
committed, and are never redistributed. The Unity engine assemblies the mod binds
to at runtime are the ones already present in the player's own copy of the game.

---

## OpenTrack

Not bundled and not linked. This mod implements the OpenTrack UDP pose datagram
layout so that OpenTrack (https://github.com/opentrack/opentrack, ISC licence)
and compatible trackers can drive it. No OpenTrack code, headers or binaries
are copied, linked or redistributed, so its licence triggers no notice
obligation here. It is credited because the wire format is its work.

---

## The Painscreek Killings

The Painscreek Killings and all related names, logos, characters and marks are
trademarks of their respective owners. They are used here only to identify the
game this mod applies to, which is nominative use and not a claim of any right
in them. This project is an unofficial, fan-made modification. It is not
affiliated with, endorsed by, or sponsored by the game's developers, its
publishers, its engine vendor, or any other rights holder. It redistributes no
game code, no game assets and no proprietary DLLs, and it requires a
legitimately purchased copy of the game. Any engine structure offsets,
function addresses or byte patterns referenced in the source were derived by
the authors through independent analysis of a legitimately owned copy. They
are factual measurements recorded as numbers; no decompiled or disassembled
game code is stored in this repository.
