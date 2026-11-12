"""Source-contract regression checks for the production-hardening milestone.
These do NOT prove Unity compiles or the game takes 90 minutes.
Run locally: python -m unittest discover -s tools -p 'test_*.py'
"""
import unittest
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
S = ROOT / "Syntera-Unity" / "Assets" / "Syntera" / "Scripts"
E = ROOT / "Syntera-Unity" / "Assets" / "Syntera" / "Editor"

class ProductionHardeningContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.game = (S / "SynteraGame.cs").read_text(encoding="utf-8")
        cls.mission = (S / "SynteraMissionObjectives.cs").read_text(encoding="utf-8")
        cls.audio = (S / "SynteraAudioDirector.cs").read_text(encoding="utf-8")
        cls.pod = (S / "SynteraDrivePod.cs").read_text(encoding="utf-8")
        cls.characters = (S / "SynteraCharacterAssets.cs").read_text(encoding="utf-8")
        cls.gate = (E / "SynteraReleaseGate.cs").read_text(encoding="utf-8")
        cls.build = (E / "SynteraEditorTools.cs").read_text(encoding="utf-8")

    def test_field_objectives_require_physical_presence(self):
        self.assertIn("InteractRange", self.mission)
        self.assertIn("objectives.InRange", self.game)
        self.assertIn("Director.CompleteObjective(next.Id)", self.game)

    def test_objectives_persist_through_the_save(self):
        core = (S.parent / "Core" / "CampaignSave.cs").read_text(encoding="utf-8")
        self.assertIn("CompletedObjectives", core)
        self.assertIn('"objectives"', core)

    def test_voices_are_optional_and_subtitles_remain(self):
        self.assertIn('Resources.Load<AudioClip>(file)', self.audio)
        self.assertIn("if (audio == null) return false", self.audio)
        self.assertIn("line.Text", self.game)
        self.assertIn("audioDirector.PlayVoice(line.Id)", self.game)

    def test_original_audio_and_mute_control(self):
        self.assertIn("AudioClip.Create", self.audio)
        self.assertIn("Mathf.Sin", self.audio)
        self.assertIn("ToggleMute", self.audio)
        self.assertIn("KeyCode.F3", self.game)

    def test_humanoid_prefab_fallback(self):
        self.assertIn("Resources.Load<GameObject>", self.characters)
        self.assertIn("animator.avatar.isHuman", self.characters)
        self.assertIn("SynteraHumanoidMotionBridge", self.characters)

    def test_improved_pod_physics(self):
        for token in ("ApplySuspension()", "AddForceAtPosition", "GetPointVelocity",
                      "linearDamping", "GetComponent<Rigidbody>", "RestoreCompletedRoute"):
            self.assertIn(token, self.pod + self.game)

    def test_editor_build_is_gated(self):
        self.assertIn("[SYNTERA UNITY VALIDATION] PASS", self.gate)
        self.assertIn("SynteraReleaseGate.Run();", self.build)
        self.assertIn("StandaloneWindows64", self.build)

    def test_voice_recording_manifest_is_honest(self):
        with (ROOT / "Docs" / "VOICE_RECORDING_CUES.csv").open(newline="", encoding="utf-8") as fh:
            rows = list(csv.DictReader(fh))
        self.assertEqual(len(rows), 210)
        self.assertTrue(all(row["resource_filename"].endswith(row["line_id"] + ".wav") for row in rows))
        self.assertTrue(all(row["recording_status"] == "NOT_RECORDED" for row in rows))
        self.assertTrue(all(row["rights_status"] == "NOT_VERIFIED" for row in rows))

    def test_playtime_is_tracked_and_saves_use_atomic_replacement(self):
        self.assertIn("ElapsedSeconds += Time.unscaledDeltaTime", self.game)
        self.assertIn("File.Replace(temp, SavePath, backup)", self.game)
        self.assertIn("recoveredFromBackup", self.game)

    def test_player_and_cinematic_production_paths(self):
        self.assertIn("SynteraPlayerPresentation", self.game)
        cinematic = (S / "SynteraCinematicDirector.cs").read_text(encoding="utf-8")
        self.assertIn("PlayAt(", cinematic)
        self.assertIn("three distinct shots", cinematic.lower())

    def test_no_github_actions_configuration(self):
        self.assertFalse((ROOT / ".github" / "workflows").exists())

if __name__ == "__main__":
    unittest.main()
