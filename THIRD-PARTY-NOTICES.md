# Third-Party Notices

This project depends on the following third-party software.

---

## Mono.Cecil

- **Version:** 0.11.5
- **License:** MIT
- **Upstream:** https://github.com/jbevain/cecil
- **Usage:** IL patching of the game's `Assembly-CSharp.dll` to inject the mod loader bootstrap.
- **Bundled:** yes (shipped in the release ZIP and deployed to the game's `Managed/` folder at install time).

---

## OpenTrack

- **Version:** N/A (UDP wire protocol only)
- **License:** ISC
- **Upstream:** https://github.com/opentrack/opentrack
- **Usage:** Head pose data is received over the OpenTrack UDP protocol. No OpenTrack code is bundled.
- **Bundled:** no.

---
