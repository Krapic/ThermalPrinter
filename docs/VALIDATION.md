# GitHub preparation — 2026-10-12

- Translated the README, application labels, tooltips, dialogs, status messages, errors, and assembly description to English. Existing code comments were already English.
- Kept Croatian glyph samples in encoding/rendering tests and the test receipt intentionally.
- Added contribution and initial push instructions, expanded Git exclusions, and added a Windows build/test workflow.
- Release build and tests with local .NET SDK 8.0.425: 23 passed, 0 failed, 0 skipped.
- Verified self-contained Windows x64 packaging with `./publish.ps1 -Test`.
- Updated Studio light/dark screenshots from the WPF rendering test.
- No print jobs were sent. Physical printer compatibility still requires hardware checks.
- The GitHub workflow has been added but has not yet run on GitHub.
- Project source licensing remains the owner's choice; dependency notices are retained.

## Historical validation

The entries below describe earlier versions and their original verification scope.

# Validation — 2026-09-30

- Local .NET SDK 8.0.425, Windows x64.
- Phase 1 Release build: succeeded, 0 warnings, 0 errors.
- Phase 2 model/renderer/encoder Release build: succeeded, 0 warnings, 0 errors.
- Integrated application Release build: succeeded, 0 warnings, 0 errors.
- Final `dotnet test -c Release`: 22 passed, 0 failed, 0 skipped.
- `publish.ps1`: self-contained win-x64 single-file publish succeeded.
- Output: `publish/win-x64/BT583Printer.exe`, 162303024 bytes (about 155 MiB).
- WPF window constructed, arranged and rendered in an STA test; sample receipt contains Croatian text and QR. Rendered PNG manually inspected: `docs/app-preview.png`.
- No print commands were sent by tests.
- Packaged EXE launch via tool was rejected by automatic policy review (`blocked by policy`); standalone launch remains unverified.
- Physical BT-583, USB/Bluetooth delivery, firmware-specific code pages/native QR/raster support and printed-code scanning remain manual hardware checks.

## UI refresh — 2026-09-30

- Rounded themed controls, complete light/dark card surfaces and dropdowns, unobtrusive scrollbars.
- Side-by-side block navigation and editor; resizable editor/preview column divider.
- Bluetooth/serial mode shows the BT-583 COM endpoint and baud instead of an unrelated Windows printer selector.
- Croatian main screen labels, active formatting highlights, useful tooltips and disabled controls for incompatible block types.
- Dialogs share the theme and keep Save/Cancel visible below their scrollable content.
- Visual smoke check uses an isolated temporary data directory so it cannot overwrite existing printer settings/templates/history.
- Light and dark renderings inspected. Minimum window layout checks that editor remains usable.
- Release tests: 22 passed, 0 failed. Self-contained publish succeeded: `publish/ui-refresh/BT583Printer.exe`.
- Physical Bluetooth printing was confirmed by the user before this UI change; transport/ESC-POS behavior was not changed in this update.

## Studio 1.2 — 2026-09-30

- Embedded multi-resolution Windows application icon, WPF window icon and branded header.
- Independent block duplication (Ctrl+D), unsaved state in document heading and title bar.
- Preview zoom 65–150%, click-to-select preview elements, live feed updates.
- Copy increment/decrement buttons clamp to 1–99.
- Release tests: 23 passed; new action test verifies deep-copy duplication, unsaved title, copy bounds and zoom transform.
- Light/dark screenshots inspected, and minimum layout check passed.
- Self-contained win-x64 publish: `publish/studio-v1.2/BT583Printer.exe`.
- No transport or encoding changes. No print jobs sent during verification.
