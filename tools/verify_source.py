"""Local-only SYNTERA source audit. No GitHub Actions or network calls."""
from pathlib import Path
import json
import re
import sys

ROOT=Path(__file__).resolve().parents[1]
PROJECT=ROOT/"Syntera-Unity"
REQUIRED=[
    "Assets/Syntera/Scripts/SynteraGame.cs",
    "Assets/Syntera/Scripts/SynteraWorld.cs",
    "Assets/Syntera/Editor/SynteraEditorTools.cs",
    "Packages/manifest.json",
    "ProjectSettings/ProjectVersion.txt",
]
errors=[]
for name in REQUIRED:
    if not (PROJECT/name).is_file(): errors.append(f"Missing: {name}")

game=(PROJECT/REQUIRED[0]).read_text(encoding="utf-8") if (PROJECT/REQUIRED[0]).exists() else ""
editor=(PROJECT/REQUIRED[2]).read_text(encoding="utf-8") if (PROJECT/REQUIRED[2]).exists() else ""
for token in ("CampaignDirector","CampaignSave.Read(","CampaignSave.Write(","public void Save()","WASD","Continue to 2050"):
    if token not in game:
        errors.append(f"Missing gameplay contract: {token}")
for token in ("MenuItem(\"SYNTERA/Create Starter Scene\")", "BuildPipeline.BuildPlayer","StandaloneWindows64"):
    if token not in editor: errors.append(f"Missing Unity editor contract: {token}")
try:
    dependencies=json.loads((PROJECT/"Packages/manifest.json").read_text())["dependencies"]
    if "com.unity.modules.physics" not in dependencies: errors.append("Unity physics module missing")
except (ValueError, OSError, KeyError) as exc: errors.append(f"Invalid Unity manifest: {exc}")
if (ROOT/".github/workflows").exists():
    errors.append("Forbidden GitHub Actions workflow directory exists")
if "m_EditorVersion:" not in (PROJECT/"ProjectSettings/ProjectVersion.txt").read_text():
    errors.append("ProjectVersion missing editor marker")
try:
    story=json.loads((PROJECT/"Assets/Syntera/Resources/syntera_campaign_dialogue.json").read_text(encoding="utf-8"))
    titles=[c["title"] for c in story["chapters"]]
    if len(titles)!=12 or len(set(titles))!=12: errors.append("Missing 12 named story chapters")
except (ValueError, OSError, KeyError) as exc: errors.append(f"Invalid dialogue data: {exc}")
for e in errors: print("FAIL:",e)
print(f"Local source audit: {'FAILED' if errors else 'PASS'} ({len(errors)} errors)")
sys.exit(1 if errors else 0)
