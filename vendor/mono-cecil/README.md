# mono-cecil (vendored)

This directory contains a bundled copy of the upstream mod loader. It is the install-time
source of truth: install.cmd extracts directly from here and never reaches out to the network.
Refresh manually with `pixi run update-deps`, then commit.

## Snapshot

- Asset: `0.11.5`
- Upstream URL: https://www.nuget.org/api/v2/package/Mono.Cecil/0.11.5
- SHA-256: `9cf1706f35b4f209c28da7417608bed7a307621b0f0179c52258af78bc4668d0`
- Fetched at: 2026-05-02T11:41:34.9066681+01:00
- Source: direct-url

Do not edit this directory by hand. Run ``pixi run package`` (or CI release) to refresh.
