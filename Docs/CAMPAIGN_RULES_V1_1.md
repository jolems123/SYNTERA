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

## Any balance change

Edit the JSON, then run both test suites. The section 14 assertions will fail on purpose. Update the PDF's figures and the test expectations together.
