SYNTERA release status. GitHub Actions CI: DISABLED by policy. Content and art: procedural placeholder (not production art).

| Feature | Status |
|---|---|
| Campaign rules core (screenplay v1.1) | implemented; section 14 parity exact (in editor and Python reference) |
| Dialogue data (12 chapters, 210 lines, stable IDs) | implemented; tested |
| Campaign director + replay save format 2 | implemented; tested (headless playthroughs, save/reload every step, tamper rejection) |
| Set pieces (sprints 4-8): CH01 bridge rescue with 5-minute timer and safety-drone fallback (Trust -2); 19 chapter puzzles (documents, relay/conduit/switch rotation, skill and timeline matching, contract clause review, service-account search, grid allocation, security-drone quiz, rally speech gated by the player's record); CH05 e-moto ride with obstacles, autopilot and roadside reset; location set dressing; world reacting to choices (riders or fleet, mayor hologram, grid lights, sunset, council seats FOR/AGAINST); end-of-chapter cards; year-change scenes; story card | implemented; Unity tests pass; NOT yet human play-tested |
| Polish (sprint 9): controller and keyboard menu navigation, high contrast, text scaling, reduced motion, auto-drive, key remapping, dialogue history, performance overlay (F9) | implemented; NOT yet human play-tested |
| Unity Editor compilation (6000.3.0f1) | PASS |
| In-editor release gate | PASS: 12 chapters, 210 lines, section 14 parity exact; 0 of 210 voice clips recorded |
| EditMode tests | PASS: 29 of 29 |
| PlayMode tests (full campaign through the real scene) | PASS: 4 of 4. All three endings reached; reload mid-campaign resumes the same chapter; worst frame 38 ms in the editor; no managed-memory growth |
| Windows build | PASS: Builds/Windows/SYNTERA.exe produced locally |
| Human playthrough, 90-minute timing, fun | NOT DONE: needs players |
| Production art, animation, voice, music, cultural review, trademark clearance | NOT DONE: needs specialists or licensed assets |

Environment note: this PC runs Windows 10 build 18363, and Unity 6.3 officially needs build 19043 (21H1) or newer. Unity runs here, but it warns that it may behave unexpectedly.

See Docs/CAMPAIGN_RULES_V1_1.md and Docs/90_MINUTE_PRODUCTION_ACCEPTANCE.md.
