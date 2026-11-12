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
for token in ("The Strike That Stopped the Sky","The Last Human Decision","AdvanceFuture","Save()","Load()","WASD",""):
    if token and token not in (game+ROOT.joinpath("README.md").read_text(encoding="utf-8")):
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
if len(re.findall(r'"(?:The Strike That Stopped the Sky|Company for Sale|The Mayor Who Quit|School of Tomorrow|The Impossible Wedding|Blackout at Nexus|A Street for Sale|Ghost in the Ledger|Sunset Rebellion|The Friend You Fire|The Last Human Decision|Tomorrow Morning)"',game)) < 12:
    errors.append("Missing 12 named story chapters")
for e in errors: print("FAIL:",e)
print(f"Local source audit: {'FAILED' if errors else 'PASS'} ({len(errors)} errors)")
sys.exit(1 if errors else 0)
