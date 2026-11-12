# Campaign rules core: screenplay v1.1

All numbers are game-design values from *SYNTERA: The Last Human Decision, 90 Minute Screenplay v1.1* (sections 5, 6, 7 and 14). They are not economic forecasts.

## Where things live

| Piece | Path |
|---|---|
| Rule table (the single source of truth) | `Syntera-Unity/Assets/Syntera/Resources/syntera_campaign_rules.json` |
| Engine (plain C#, no UnityEngine) | `Syntera-Unity/Assets/Syntera/Core/` (`Syntera.Core` assembly) |
| Unity EditMode tests | `Syntera-Unity/Assets/Syntera/Tests/Editor/CampaignRulesTests.cs` |
| Independent Python reference | `tools/test_campaign_rules.py` |

The C# engine and the Python reference are separate implementations reading the same JSON, so a mistake in either one makes the two disagree.

## Transaction order (section 10)

`CampaignEngine` is the only way to change state:

1. Check that the step is the current mission.
2. Check Credits and Treasury. An option you can't afford is rejected with the exact shortfall.
3. Commit one deterministic action.
4. Add a SHA-256 hash-linked audit event.
5. Raise `Committed`, where the Unity layer saves a checkpoint.

`PreviewCharter` evaluates the council without changing anything.

## How the ambiguous rules were settled

Each of these readings reproduces every section 14 figure. The alternative readings do not.

1. **Naomi's credit line.** It's offered the first time a Credits shortfall happens with no Treasury shortfall, once per run. The simulated "intent" player always accepts it, *even when 40 Credits still isn't enough*. If the option is still unaffordable, the player takes the other option. Accepting the line only when it fully covers the gap gives 24 credit-line paths, but the PDF says 32. In game, the offer screen states the remaining shortfall before the player accepts (`OptionStatus.CreditsShortfallAfterCreditLine`).
2. **Option you can't afford.** The simulated player falls back to the first affordable alternative, which is always option B.
3. **Year changes.** The rules apply in the order listed. The Jobs ≥ 55 tax check reads Jobs before the 2028 apprentice and badge adjustment.
4. **Evidence cards.** Each card counts once. "Worker outcomes" is one card whether it was earned through CH01, CH04 or both.
5. **Limits.** Trust, Jobs, Autonomy, Grid, VANTAGE influence and the relationship scores stay within 0–100. Credits and Treasury never go below 0.
6. **Kato reconciled.** This is checked straight after the CH10 A effects are applied (Kato ≥ 55).

## Verified results (executed, not estimated)

| Check | Screenplay | Engine |
|---|---|---|
| Paths | 1,024 | 1,024 |
| Shared / Compact / Covenant passes | 242 / 466 / 348 | 242 / 466 / 348 |
| No charter can pass | 280 | 280 |
| Every intended choice affordable | 922 | 922 |
| Credit line used | 32 | 32 |
| Most A options on one path | 9 | 9 |
| All-A seats (Shared / Compact / Covenant) | 5 / 3 / 2 | 5 / 3 / 2 |
| All-B seats | 3 / 5 / 2 | 3 / 5 / 2 |
| Negative resources / audit failures | 0 | 0 / 0 |

How each was run:

- **C# engine:** compiled with the .NET Framework 4 `csc` (C# 5) and run against the real test file, with a scratch NUnit shim. All 10 tests passed.
- **Python reference:** 4 of 4 tests passed.
- **Unity Editor run of the same tests:** NOT RUN YET. It needs Unity 6000.3.0f1 to be installed. Then run `tools/verify-unity-local.ps1`.

## Cast and place names (project decision, overrides the screenplay)

The story structure, roles, arcs and dialogue are screenplay v1.1. Only the names differ: characters use English names, and places use modern AI-era names.

| v1.1 screenplay | In game |
|---|---|
| Kato Ssemakula | Kato Mercer |
| Amara Achieng | Amara Reed |
| Noah Okello | Noah Bennett |
| Mama Sarah | Mama Sarah (Sarah Hughes) |
| Mayor David Mukasa | Mayor David Cross |
| Cllr. Olivia Nansubuga | Cllr. Olivia Grant (council bloc `grant`) |
| Grace Nabirye | Grace Kim |
| Rehema "Raven" Atim | Rachel "Raven" Black |
| Naomi Wanjiru | Naomi Walsh |
| Iris Mutesi | Iris Mitchell |
| Kibira Junction | Nexus Junction |
| Bulungi Square | Signal Square |
| Ssomero Commons | Synapse Commons |
| Nalubale Gardens | Lumen Gardens |
| Kiyira substation | Volta substation |
| Mirembe Heights | Haven Heights |
| Wansi Market | Circuit Market |
| boda / e-boda | moto-taxi / e-moto |
| rolex (street food) | egg-roll |
| kwanjula | introduction ceremony |

AURA, VANTAGE, GAIA, Pip-04, Loopline, Civic Hall and Quantum Hill are unchanged.

The v1.0 prototype characters are not part of the new structure: Mayor Adrian Cross, SENTINEL, and the drone-wedding and doorbell-consent plots. They remain only in the old prototype scripts until Sprint 2 replaces them.

## Dialogue data

`syntera_campaign_dialogue.json` holds all 12 chapters. For each one it has the staging beats, the main dialogue, the reactions to each option, the CH11 charter speeches and the CH12 ending scripts. It also holds the conditional lines from section 8. There are 210 lines, all copied word for word from the screenplay.

- **Line IDs** look like `ST01_KATO_003` (chapter, speaker, number) and are **permanent**. A new line gets the next free number for that speaker. Existing lines are never renumbered, because audio, subtitles and translations will refer to these IDs.
- **What dialogue can do.** It can only test flags. It never changes Credits, Treasury or ownership. `DialogueLibrary.Load` rejects any reference to an unknown decision, option, charter, ending, flag or speaker.
- **Conditional beats.** The game picks exactly one matching variant. If none matches, the beat is skipped. The screenplay doesn't write neutral fallback lines, and I haven't invented any.
- **Language.** The game is English only. The screenplay's notes about Luganda and Swahili lines (CH04, section 10) don't apply. Grace corrects the robot tutor as staging only, with no spoken line. Local terms are translated too: see the name table above. `tools/test_campaign_rules.py` fails if any screenplay-only name or term comes back into game text.

## How the game runs a chapter (Sprint 2)

`CampaignDirector` (in `Syntera.Core`) steps through each chapter in this order:

1. Intro title card.
2. Field work: the objectives must be done in order, then the player talks to the chapter's contact.
3. Conversation, including the conditional lines from section 8.
4. Decision.
5. Reaction.
6. Year summary, when a new year starts.

CH11 then goes through evidence, charter proposal, charter speeches and the seat-by-seat vote. CH12 plays the ending and then the recap.

`SynteraGame` (Unity) only draws the current stage and passes on what the player does. It never changes numbers itself.

- **Saves (format 2, `syntera-campaign.json` plus `.bak`).** A save stores the player's committed actions and the current stage. Loading replays every action through the engine and checks the audit chain, so a hand-edited or out-of-step save is rejected and the backup is used instead. A save from the old v1.0 prototype (`syntera-city-save.json`) is never changed or deleted. The player is told a new campaign has started.
- **Play again** asks for confirmation first, and keeps the finished run as the backup.
- **Continue to 2050** is a clearly labelled prototype. It works on a separate copy of the final numbers with its own save file, and never changes the 2026–2028 record.
- **Voice clips** are optional files at `Resources/Voice/<line ID>`. The full list of 210 recording cues is in `Docs/VOICE_RECORDING_CUES.csv`. Every line works with subtitles alone.

## Set pieces (Sprints 4–9)

- **Data:** chapter objectives can name a puzzle, defined in `syntera_activities.json`. There are seven puzzle types: documents, rotate, match, select, allocate, quiz and speech. Puzzle logic is in `Syntera.Core/Activities.cs`, and **puzzles never change campaign numbers**. Finishing one only completes its objective.
- **Assist:** it unlocks after 3 failed attempts and solves one step, as section 9 of the screenplay requires.
- **The CH01 rescue** has a 5-minute timer. When it runs out, a safety drone rescues Noah and the engine applies the authored outcome `ST01_RESCUE_BY_DRONE` (Trust −2), once, as an audited transaction that is saved and replayed.
- **The CH05 ride can't softlock:** there's a roadside reset (R), autopilot after 3 collisions or from the options (P), and the run can't be lost.
- **The CH09 rally speech** offers options based on the player's record (flags and stats). It's dialogue only and has no economic effect.

## Any balance change

Edit the JSON, then run both test suites. The section 14 assertions will fail on purpose. Update the PDF's figures and the test expectations together.
