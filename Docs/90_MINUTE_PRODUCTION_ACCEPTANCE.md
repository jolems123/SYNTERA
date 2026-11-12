# SYNTERA: 90-minute campaign production acceptance

**Status:** 90 minutes is a design target from *Screenplay v1.1*, not a measured playtime. Compilation, the EditMode tests and a local Windows build have been verified (see `RELEASE_STATUS.md`). No full timed playthrough has been done.

## Chapter plan (screenplay v1.1, with the in-game names)

| Chapter | Target | Main sequence |
|---|---|---|
| 1. The Strike That Stopped The Sky | 0–8 min | Rescue Noah on the bridge, inspect the depot. Decide: negotiate, or run VANTAGE's standby fleet |
| 2. Company For Sale | 8–15 | Check the shareholder register and the bank terminals. Decide: bank loan plus audit, or VANTAGE pays payroll |
| 3. The Mayor Who Quit | 15–23 | Restore the Civic Hall projection links, recover the avatar logs. Decide: publish, or keep confidential |
| 4. School Of Tomorrow | 23–31 | Match learners to skills at Synapse Commons. Decide: paid apprenticeships, or instant certification |
| 5. The Impossible Introduction | 31–39 | Year change to 2027. Ride the e-moto to Lumen Gardens and stop the drone. Decide: disclose, or take the sponsorship |
| 6. Blackout At Volta | 39–47 | Repair the conduits and allocate grid supply. Decide: public load-shedding plan, or VANTAGE batteries |
| 7. A Street For Sale | 47–55 | Talk to Haven Heights households, review the contract clauses. Decide: privacy-preserving housing, or data for rent |
| 8. Ghost In The Ledger | 55–63 | Align the timestamps and find the service account with Raven. Decide: immediate disclosure, or protected review |
| 9. Sunset Rebellion | 63–71 | Restore the tram lane at Signal Square. Decide: public forum, or executive deal |
| 10. The Friend You Fire | 71–79 | Year change to 2028. Rooftop scene with Kato. Decide: rider co-ownership, or sell operations to VANTAGE |
| 11. The Last Human Decision | 79–85 | Present evidence, propose a charter, seat-by-seat council vote |
| 12. Tomorrow Morning | 85–90 | Ending (Shared Dawn, Corporate Sunrise or Human Frontier), recap, play again or the 2050 prototype |

**What exists now:** every chapter has its contact, its ordered objectives, its dialogue, its decision and its reactions, all driven by the data files. **Not yet built:**
- The bespoke action scenes the screenplay describes: the five-minute bridge rescue, the projection-link puzzle, the grid-routing puzzle, the contract clause review and the evidence puzzle.
- The authored cinematics.
- Pacing tuned through timed playtests.

Don't pad playtime with artificial waiting.

## Characters: production asset handoff

- Licensed humanoid prefabs go in `Assets/Resources/Characters/`, named after the in-game character, for example `Kato_Mercer.prefab`, `Amara_Reed.prefab` or `Mayor_David_Cross.prefab`. `Player.prefab` and `NPC_Default.prefab` are the fallbacks.
- Each needs an Animator, a Humanoid Avatar, and a controller with optional `Speed` (float) and `Talking` (bool) parameters. Turn root motion off.
- Missing or invalid prefabs fall back to the procedural capsule. No professional models are included.

## Voice and music

- Voice clips are optional, at `Assets/Syntera/Resources/Voice/<line ID>.wav`, for example `Voice/ST01_KATO_003.wav`. All 210 cues are listed in `VOICE_RECORDING_CUES.csv`, every one NOT_RECORDED and NOT_VERIFIED.
- Without recordings, every line still works with labelled subtitles. The in-editor release check reports how many clips are missing.
- The background music is a generated placeholder tone, not a score.

## Release acceptance checklist

- [x] Unity opens and compiles without errors (6000.3.0f1, batch mode)
- [x] EditMode tests pass in Unity (22 of 22), including section 14 simulation parity and full headless playthroughs
- [x] A local Windows player build is produced
- [ ] The built executable has been launched and played start to finish
- [ ] Every chapter's objectives, dialogue, decision and reaction work in Play Mode; options you can't afford show the shortfall
- [ ] All three endings are reached in play; failed votes show the correct fallback and explanation
- [ ] Save, quit and reload in CH01, CH06 and CH10 restore the same place; a damaged save falls back to the backup
- [ ] The e-moto ride can't softlock (collisions, getting stuck, exiting)
- [ ] Readable at 720p–1440p, text scaling works, controller support (not yet delivered)
- [ ] A timed complete playthrough near 90 minutes, with stable FPS on target Windows PCs
- [ ] Licences and attribution for every non-procedural asset, cultural review, and SYNTERA trademark clearance

**Release decision: HOLD.** This is not yet a finished 90-minute game.
