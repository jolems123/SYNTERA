# SYNTERA — Interactive campaign milestone

## Current source behavior
- Eight named world characters are tagged as interactable characters.
- Twelve chapters have a designated mission contact. Approach the contact within five world units and press E to open a consequential choice.
- The player can walk around Syntera City and travel to the eight districts through the map.
- Decisions affect trust, employment, AI influence, the environment and credits. The final choices determine a 2028 civilization ending, with replay or an optional simplified 2029–2050 continuation.
- Autosave uses a temporary file and retains a backup copy. Invalid or corrupt primary saves fall back to the backup.

## Local verification
Run `python -m unittest discover -s tools -p 'test_*.py'` and `python tools/verify_source.py` from the repository root.
In Unity, run `SYNTERA → Create Starter Scene` and test the entire loop in Play Mode. Use the Windows Build menu locally only after it compiles.
**GitHub Actions must stay disabled.**

## Release blockers
- Unity Editor compilation/Play Mode and Windows build not yet executed or certified.
- Full 90-minute authored mission pacing, authored dialogue, cinematic sequencing, NPC schedules, music, polished assets and advanced vehicle physics are not delivered in this milestone.
- Full save-corruption recovery and gameplay behavior need real runtime tests.
- Distinctive 3D actor models, locomotion and photorealistic production art still needed.

## Manual acceptance scenarios
1. New game launches with the player near Kato.
2. The first decision cannot be made from a distance.
3. Press E near Kato to resolve the robot strike.
4. Resolve all 12 named chapters in order; approach the correct contact each time.
5. Verify city indicators change only once per choice.
6. Close and reopen; confirm the current mission and indicators persist.
7. Corrupt the primary save with a valid backup, then verify recovery.
8. Finish the campaign, review the ending, replay and continue to 2050 separately.
9. Confirm camera/movement stops when dialogue or map is open.
10. Build a Windows player locally and record console errors, FPS and crash reports.
