# SYNTERA: The Last Human Decision

A third-person story and civic-strategy game set in Syntera City, a fictional lakeside metropolis. You inherit a stake in Loopline, a failing moto-taxi delivery company. Over twelve chapters from 2026 to 2028, you decide what a corporate AI may buy, whether to trust the cooperative AI that misled you, and which charter you put to a nine-seat council vote. The story follows *Screenplay v1.1*. The game is in English. Every number is a game rule, not a forecast.

## Current status

The status of every feature, including what has and hasn't been verified, is in [Docs/RELEASE_STATUS.md](Docs/RELEASE_STATUS.md). In short:
- **Verified locally:** the campaign compiles in Unity 6000.3.0f1, all 22 EditMode tests pass, the in-editor release check passes, and a local Windows build has been produced.
- **Not yet done:** nobody has play-tested the build, and the art is placeholder primitive shapes, not production art. There are no voice recordings yet (subtitles only).

## How it is built

| Part | Where |
|---|---|
| Rule table (economy, year changes, credit line, evidence cards, council) | `Syntera-Unity/Assets/Syntera/Resources/syntera_campaign_rules.json` |
| Dialogue for all 12 chapters (210 lines, permanent IDs such as `ST01_KATO_003`) | `Syntera-Unity/Assets/Syntera/Resources/syntera_campaign_dialogue.json` |
| Rules engine, chapter flow controller, save format (plain C#, no Unity dependency) | `Syntera-Unity/Assets/Syntera/Core/` |
| Unity presentation: world, movement, e-moto, UI | `Syntera-Unity/Assets/Syntera/Scripts/` |
| Tests | `Syntera-Unity/Assets/Syntera/Tests/Editor/` (Unity) and `tools/test_*.py` (Python) |

Design rules, the cast and place names, and how the save format works are in [Docs/CAMPAIGN_RULES_V1_1.md](Docs/CAMPAIGN_RULES_V1_1.md).

## Open and play

1. Install Unity Hub and the Unity **6000.3.0f1** editor, and activate a licence in Hub.
2. Add the `Syntera-Unity/` folder as a project. Open `Assets/Syntera/Scenes/City.unity` and press **Play**.
3. Controls:
   - **WASD**: walk or drive.
   - **Shift**: sprint.
   - **Right mouse**: orbit the camera.
   - **E**: use orange objective markers and talk to your contact.
   - **F**: enter or exit the e-moto.
   - **Space** or **Enter**: next dialogue line.
   - **M**: map.
   - **+ / -**: text size.
   - **F3**: mute.
   - **Esc**: help.

## Local verification (no GitHub-hosted CI)

**Never create or enable GitHub Actions workflows.** All tests and builds run locally.

    python tools/verify_source.py
    python -m unittest discover -s tools -p "test_*.py"
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\verify-unity-local.ps1             # release check + EditMode tests
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\verify-unity-local.ps1 -BuildWindows

The Unity script finds the editor at `D:\Unity\6000.3.0f1` or in the Unity Hub folder, or you can pass `-UnityExe`. Logs go to `local-unity-logs/`. None of these checks replace playing the build.

## Next

See [Docs/90_MINUTE_PRODUCTION_ACCEPTANCE.md](Docs/90_MINUTE_PRODUCTION_ACCEPTANCE.md) for the remaining gameplay, art, audio and release work.
