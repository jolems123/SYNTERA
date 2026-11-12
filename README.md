# SYNTERA — The Last Human Decision

A fictional futuristic 3D city game where your decisions shape the struggle between humans and AI. The first campaign spans 2026–2028; its longer-term creative vision extends to 2050.

## Current status

This is the **new** repository. Its prior contents were deleted by the owner. A clean Unity 6 source prototype has been committed here as the new baseline. The previous v1.9 source archive remains available only as a separate downloadable artifact from the earlier conversation and is **not** represented here as fully restored.

**Playability is not yet certified.** The procedural low-poly city, third-person movement, 12 decision chapters, three endings, autosave, districts and ambient traffic are implemented in C# source. Professional art, cinematic audio, spoken dialogue, rigorous gameplay QA, full 90-minute content and actual Unity builds are not complete.

## Open and run (local, without GitHub CI)

1. Install Unity Hub and Unity **6.3** editor with Windows Build Support if creating an executable.
2. Use **Add project from disk** and select the repository's \`Syntera-Unity/\` folder.
3. From the Unity editor select **SYNTERA → Create Starter Scene**.
4. Open \`Assets/Syntera/Scenes/City.unity\` if it is not already open, then press **Play**.
5. Move with **WASD / arrows**, **Shift** to sprint, right mouse drag for camera, **M** for map, **Esc** for help. Choose an option on the mission card to resolve a crisis and advance toward 2028.
6. To package locally use **SYNTERA → Build Windows Player Locally**; inspect Console and test the resulting executable.

You can also use Unity batch mode from a Windows computer with the editor installed:

\`\`\`powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe" -batchmode -quit -projectPath "$PWD\Syntera-Unity" -executeMethod Syntera.Editor.SynteraEditorTools.BatchBuildWindows -logFile "$PWD\unity-build.log"
\`\`\`

Adjust path for your installed editor version. This is an **example only**; it has not been executed here.

## No GitHub-hosted CI

**Never create or enable GitHub Actions workflows, or run GitHub-hosted CI/build jobs.** All tests and builds must run locally or on an explicitly approved, non-GitHub runner. Do not confuse "no CI" with "no testing."

## Story, driving and cinematic controls (source milestone)

This version adds authored dialogue across twelve chapters, chapter-specific economic outcomes, two orange-beacon delivery objectives (chapters 5 and 8), a cyan drivable pod, procedural NPC gestures and skippable in-engine captioned camera sequences.

- **WASD**: walk or drive; **Shift**: sprint on foot.
- **E**: talk to the current mission contact within range; advance dialogue on-screen.
- **F**: enter/exit the cyan pod while stopped; **Space**: brake in the pod.
- Drive to the orange mission beacon for delivery objectives before a choice can be completed.
- **Space/Enter**: skip cinematic; **M**: city map; **Esc**: close help or dialogue.

Run local source tests using `python -m unittest discover -s tools -p 'test_*.py'` and `python tools/verify_source.py`. See `Docs/NARRATIVE_DRIVING_MILESTONE.md` for Unity Play Mode acceptance cases. **Neither script certifies a real Unity build or a 90-minute playthrough.** Do not enable GitHub Actions.

## Next requirements

- Resolve Unity compile errors found by the editor, add local EditMode and PlayMode tests.
- Restore and extend the 90-minute screenplay with cinematic dialogue, characters, 12 missions and meaningful pacing.
- Professional rigged characters, clothing, photorealistic assets, reliable vehicle physics, performance and accessibility.
- Save integrity tests, Windows smoke tests, and independent player testing before release.
