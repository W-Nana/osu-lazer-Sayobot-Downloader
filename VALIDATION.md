# Validation Record

Date: 2026-10-26.

## Target and Build

- Target: **osu!lazer 2026.1005.0-lazer** stable.
- Reference assemblies: `osu.Game.dll`, `osu.Framework.dll`, and other assemblies from that release's Linux package.
- The complete official release package was verified against its release manifest using SHA-256:
  `3ae026e4ee6a7f5f994365fbcef4c8030d400acef06df26279b6f6e0f4e0cc0f`.
- Built with the workspace's .NET SDK 10.0.403.
- Release build: zero warnings and zero errors.
- The installation package contains only this ruleset's DLL and documentation, without game or framework assemblies.

## Localisation Tests (0.2.0)

Six tests passed using `LocalisationManager` from the targeted stable release:

- All 35 catalogued messages have English, Simplified Chinese, Traditional Chinese, and Japanese translations with working format placeholders.
- Language selection covers `zh`, `zh-Hans`, `zh-Hant`, Taiwan, Hong Kong, Macau, and Japanese regional variants. Explicit Chinese script selection takes precedence over the region.
- Unsupported languages fall back to English while retaining local number formatting. English also works before a localisation store is available.
- Existing bound notification text and nested error descriptions update immediately when switching between the four languages.
- Translated error descriptions cover HTTP 404/410, other HTTP statuses, connection failures, timeouts, invalid archives, file errors, import failures, and unexpected errors. Original diagnostics remain available in the logs.
- String equality and argument changes behave correctly, allowing progress and download stage updates to be displayed.

## Downloader Tests

Twelve offline tests and one live network test passed:

- Beatmap set ID and URL parsing, including full and no-video download routes.
- Complete `.osz` downloads through redirects, without Authorization or Cookie headers.
- HTTPS Sayobot nodes and nonstandard ports are accepted; external and HTTP redirects are rejected.
- HTML error pages, truncated downloads, incorrect beatmap set IDs, stale MD5 checksums, and ZIP archives without `.osu` files are rejected.
- User cancellation removes partial temporary files.
- HTTP 404 remains a recoverable download failure.
- Existing destination files are never overwritten or deleted.
- Live Sayobot download: beatmap set **41823**, **2,872,542 bytes**, six ZIP entries, and two `.osu` difficulties. Download and archive validation succeeded, and temporary files were removed afterward.

## Integration Test with Stable Release Assemblies

Uses `TestRunHeadlessGameHost`, a temporary Realm database, and `DummyAPIAccess`. No player data or login credentials are used. Sayobot transfers use the real network; the official API is mocked.

The following workflow passed:

```text
Load the service through the ruleset icon
→ Call the real BeatmapModelDownloader.Download() API
→ Receive the public DownloadBegan event
→ Cancel the original request, leaving it in the Failed state
→ Complete the Sayobot download
→ Import through the real BeatmapManager into the temporary Realm database
→ Find both difficulties by beatmap set ID
```

Official fallback was tested with a missing mirror set. Two native download requests were created: the first reached `Failed`, and the fallback remained `Waiting` under the mocked API. The fallback bypassed interception without entering a loop. The mocked request was then cancelled and temporary data cleaned up.

This test also identified an issue with older beatmaps that omit online IDs. The importer now fills in known beatmap set IDs so native availability tracking can recognise those imports.

## Cancellation Notifications and Error Reporting (0.2.1)

The integration test was extended to use notifications created by the real native downloader and a recording `INotificationOverlay`:

- A native notification was withheld from the public notification collection for 250 milliseconds. It was still closed and marked as read after interception.
- The intercepted cancellation notification was also closed when the mirror was missing the requested set.
- The actual official fallback notification remained visible. Pre-existing cancellation notifications with the same title and notifications from other components were preserved.
- Complete diagnostics from a real mirror download failure remained in the local runtime log. `Logger.NewEntry` listeners did not receive that entry, preventing an error notification or Sentry event through that logging path.

All checks passed. Notification cleanup uses the public collection, text descriptors, and `Close(false)`, without accessing private fields or replacing the native notification callback. Notifications are preserved when matching titles cannot be identified unambiguously. The duration of any brief notification flash has not been measured in a windowed client.

## Remaining Verification and Known Limitations

- Settings, notification layouts, and every download entry point have not been manually checked in a windowed player client.
- Multiplayer, ranked play, automatic updates, and Windows/macOS execution have not been individually verified.
- Native download buttons do not show Sayobot's progress; progress appears in separate notifications.
- Updates use the public `ImportAsUpdate` API, but have not been tested against an existing player database containing scores and collections.

To check downloading and importing on your platform, enter `41823` in the manual download field before trying native download entry points.
