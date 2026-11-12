# SYNTERA — Narrative, driving and cinematics milestone

## Delivered in source
- Twelve authored conversations using existing named characters, with dialogue progression before choices and 24 distinct consequence outcomes. Each decision has specific resource and reputation effects, validated against the player's credits and public treasury.
- Mission choice buttons are unavailable until the dialogue has advanced.
- Chapters 5 (wedding drones, index 4) and 8 (ledger evidence, index 7) require delivering a pod to an orange checkpoint.
- The pod uses Rigidbody forces, acceleration, steering, braking, obstacle-aware exit spacing, and an on-screen remaining-distance indicator. This is **arcade vehicle handling**, not professional wheel/suspension simulation.
- Mission completion is stored in the save as a bit mask, so route progression survives reload. The story contact moves to the delivery destination; replay resets the car, player and contact positions.
- A lightweight skippable camera/caption sequence opens the game and plays between chapters.
- NPCs have simple procedural arm, head and idle motion. These are **placeholder animations**, not professional character performances.
- Save/replay includes backup recovery and resets vehicle state.

## Local verification
Run in repository root:
\`\`\`bash
python tools/verify_source.py
python -m unittest discover -s tools -p "test_*.py"
\`\`\`
Both are source checks only. Unity Editor and a Windows build must run on a machine with Unity 6.3 installed. GitHub Actions and paid GitHub CI **must not be enabled or run**.

## Unity Play Mode acceptance checklist
1. Generate starter scene from SYNTERA menu and press Play. Watch the introductory cinematic, skip it with Space.
2. Walk to Kato, press E, advance every line, and make the robot-strike choice. Confirm only one set of stat changes.
3. Check that NPC idle/arm motions appear and that the newly assigned mission contact moves nearby.
4. At wedding chapter, press F by the cyan vehicle and use WASD to drive toward the orange checkpoint; Space brakes.
5. Try deciding before delivery: choices must be blocked. Cross checkpoint, safely exit, and return to the nearby mission NPC.
6. Save/exit/reload during the delivery chapter: checkpoint completion must persist.
7. Repeat driving objective for the ledger chapter, including collision and exit safety.
8. Verify dialogue, camera and driving input locks work during cinematics; press Space to skip.
9. Finish all 12 chapters, confirm the ending, replay and optionally continue toward 2050.
10. Test keyboard controls and readable subtitles at 720p, 1080p, and 1440p; collect actual FPS and errors.

## Not delivered, do not claim
- A complete tested 60–90-minute game or complete feature-length screenplay with voiced cinematics.
- Professional rigged characters, vehicle suspension or realistic traffic navigation.
- Unity compile, Play Mode results, real-device FPS, Windows executable or commercial certification.

These remain launch blockers. Local editor compilation and playtesting are required before describing the game as production-ready.
