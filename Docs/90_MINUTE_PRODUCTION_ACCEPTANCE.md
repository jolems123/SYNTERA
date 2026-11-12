# SYNTERA — 90-minute campaign production acceptance

**Status:** 90 minutes is a design target, not a measured playtime. Unity Editor compilation, runtime performance and full campaign playthrough are not yet verified.

## Proposed player pacing

| Chapter | Target | Main sequence |
|---|---|---|
| 1. Strike That Stopped the Sky | 0–8 min | Robot strike, inspect dispatch, charging records, Kato's decision |
| 2. Company for Sale | 8–15 | Contracts, VANTAGE financing, company independence |
| 3. Mayor Who Quit | 15–23 | Forged resignation, public election |
| 4. School of Tomorrow | 23–31 | Inspect school, teachers versus automation |
| 5. Impossible Wedding | 31–39 | Drive rescue pod to wedding, decide rescue or monetization |
| 6. Blackout at Nexus | 39–47 | Hospital and substation investigation, allocate electricity |
| 7. Street for Sale | 47–55 | Resident and redevelopment investigation |
| 8. Ghost in the Ledger | 55–63 | Drive the evidence to archive, disclose or conceal |
| 9. Sunset Rebellion | 63–71 | Assembly safety and surveillance investigation |
| 10. Friend You Fire | 71–79 | Discover Kato's secret contract and choose response |
| 11. Last Human Decision | 79–85 | Governance charter and AI oversight choice |
| 12. Tomorrow Morning | 85–90 | Citizen council, final decision, ending |

The current source contains two physical investigations for every non-driving chapter, two pod deliveries, authored dialogues and economic choices. It does not yet guarantee 90 minutes of engaging gameplay; fully authored action scenes, story performances, tuned pacing and timed player testing remain necessary. Do not pad playtime with artificial waiting.

## How to verify locally — never use GitHub Actions

Install Unity 6.3 and the Windows Build Support module, then open the Syntera-Unity folder through Unity Hub.

In the Unity menu select SYNTERA > Validate Campaign and Assets Locally.

From Windows PowerShell at the repository root:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\verify-unity-local.ps1

To run the same checks and produce a local Windows player:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\verify-unity-local.ps1 -BuildWindows

Run offline source contracts:

    python tools/verify_source.py
    python -m unittest discover -s tools -p "test_*.py"

These scripts do NOT invoke GitHub Actions. The Unity Editor validation only checks content/configuration; it cannot substitute for playing the build.

## Humanoid characters — production asset handoff

- Licensed imported humanoid prefabs go in Assets/Resources/Characters/Player.prefab, NPC_Default.prefab, or named Kato_Mercer.prefab / Amara_Reed.prefab.
- Each requires a valid Animator, Humanoid Avatar, and a controller with optional Speed float and Talking bool parameters.
- Root motion should be disabled for visuals: SYNTERA moves host objects and drives animation parameters.
- Invalid or absent assets retain the procedural fallback. Realistic skin, hair, clothing, faces, hand animation, mocap and high-quality LODs are still missing; this commit does not supply professional 3D models.

## Voice and music

- Built-in background is original generated tone, not a mastered score.
- Record or license narration clips and import AudioClips at Assets/Resources/Voice/Chapter_00_Line_00.wav, etc. Indexing is zero-based; the authored chapter scenes now contain 10 voice lines each (120 in total).
- Missing recordings keep subtitles available. The local Unity Editor gate reports missing clips. F3 mutes/unmutes audio.
- Amplitude-based mouth gestures are not phoneme lip sync. Retargeted expressions, visemes and a voice-direction pass remain unfinished.

## Release acceptance checklist (not executed here)

- [ ] Unity opens and compiles without errors; scene and camera launch
- [ ] Intro and chapter cinematics skip safely; all controls recover
- [ ] Investigations require both waypoints, save and restore
- [ ] Vehicle enters, accelerates, steers, brakes, collides, stops, exits and finishes both missions
- [ ] All 12 chapters and all 24 decision consequences work, no duplicate payouts
- [ ] Three endings and 2050 continuation work with save, corrupt-file recovery and replay
- [ ] Character animation plays, imported assets retarget and speech works with/without WAVs
- [ ] Keyboard and controller (controller support not yet delivered), readable 720p–1440p interface, accessibility
- [ ] Timed complete playthrough, duration near 90 minutes, stable FPS on target Windows devices
- [ ] Licenses and attribution for every nonprocedural asset, security review and distribution checks

**Release decision: HOLD.** Do not claim this is a finished 90-minute commercial game from offline source checks.
