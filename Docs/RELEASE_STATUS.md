SYNTERA release status. GitHub Actions CI: DISABLED by policy. Content and art: procedural placeholder (not production art).

| Feature | Status |
|---|---|
| Campaign rules core (screenplay v1.1) | implemented; tested outside Unity (csc + NUnit shim, Python reference); section 14 parity exact |
| Dialogue data (12 chapters, 210 lines, stable IDs) | implemented; tested outside Unity |
| Campaign director + replay save format 2 | implemented; tested outside Unity (full headless playthroughs, save/reload every step, tamper rejection) |
| Unity Editor compilation (6000.3.0f1) | PASS: 0 errors, 0 warnings after the linearVelocity fix (batch mode, 2026-11-12) |
| In-editor release gate | PASS: 12 chapters, 210 lines, section 14 parity exact (242/466/348, 280, 922, 32); 0 of 210 voice clips recorded |
| EditMode tests in Unity | PASS: 22 passed, 0 failed (Unity 6000.3.0f1, Personal licence via Unity Hub 3.16.3) |
| Play Mode / real gameplay | NOT RUN |
| Windows build | PASS: Builds/Windows/SYNTERA.exe produced locally; executable not yet play-tested |

Environment note: this PC runs Windows 10 build 18363, and Unity 6.3 officially needs build 19043 (21H1) or newer. Unity runs here, but it warns that it may behave unexpectedly.

See Docs/CAMPAIGN_RULES_V1_1.md.
