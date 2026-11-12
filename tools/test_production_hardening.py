"""Source-contract regression checks for the production-hardening milestone.
These do NOT prove Unity compiles or the game takes 90 minutes.
Run locally: python -m unittest discover -s tools -p 'test_*.py'
"""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
S = ROOT / "Syntera-Unity" / "Assets" / "Syntera" / "Scripts"
E = ROOT / "Syntera-Unity" / "Assets" / "Syntera" / "Editor"

class ProductionHardeningContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.game = (S / "SynteraGame.cs").read_text()
        cls.mission = (S / "SynteraMissionObjectives.cs").read_text()
        cls.audio = (S / "SynteraAudioDirector.cs").read_text()
        cls.pod = (S / "SynteraDrivePod.cs").read_text()
        cls.characters = (S / "SynteraCharacterAssets.cs").read_text()
        cls.gate = (E / "SynteraReleaseGate.cs").read_text()
        cls.build = (E / "SynteraEditorTools.cs").read_text()

    def test_field_missions_require_two_visits(self):
        self.assertIn("stage >= 2", self.mission)
        self.assertIn("stage++", self.mission)
        self.assertIn("investigations.TryInteract()", self.game)
        self.assertIn("investigations.Complete", self.game)

    def test_investigation_persists_in_twelve_chapters(self):
        self.assertIn("investigationStages = new int[12]", self.game)
        self.assertIn("State.investigationStages[State.chapter] = investigations.Stage", self.game)
        self.assertIn("State.investigationStages.Length != 12", self.game)

    def test_voices_are_optional_and_subtitles_remain(self):
        self.assertIn('Resources.Load<AudioClip>(file)', self.audio)
        self.assertIn("if (audio == null) return false", self.audio)
        self.assertIn("dialogue[dialogueIndex]", self.game)
        self.assertIn("audioDirector.PlayVoice(dialogueIndex)", self.game)

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

    def test_no_github_actions_configuration(self):
        self.assertFalse((ROOT / ".github" / "workflows").exists())

if __name__ == "__main__":
    unittest.main()
