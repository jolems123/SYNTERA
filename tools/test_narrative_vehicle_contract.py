"""Offline source contracts for driving, cinematics and actors.
Run: python -m unittest discover -s tools -p "test_*.py"
Does not replace Unity compilation, Play Mode or a Windows build.
"""
from pathlib import Path
import json
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "Syntera-Unity" / "Assets" / "Syntera" / "Scripts"
DIALOGUE = ROOT / "Syntera-Unity/Assets/Syntera/Resources/syntera_campaign_dialogue.json"


class NarrativeVehicleContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.game = (SCRIPTS / "SynteraGame.cs").read_text(encoding="utf-8")
        cls.pod = (SCRIPTS / "SynteraDrivePod.cs").read_text(encoding="utf-8")
        cls.cinematic = (SCRIPTS / "SynteraCinematicDirector.cs").read_text(encoding="utf-8")
        cls.animation = (SCRIPTS / "SynteraActorAnimation.cs").read_text(encoding="utf-8")
        cls.doc = json.loads(DIALOGUE.read_text(encoding="utf-8"))

    def test_ch05_is_the_drive_chapter(self):
        drives = [o["id"] for ch in self.doc["chapters"] for o in ch["objectives"] if o["kind"] == "drive"]
        self.assertEqual(["ST05_OBJ_RIDE"], drives)
        self.assertIn("ObjectiveKind.Drive", self.game)
        self.assertIn("drivePod.RouteComplete", self.game)
        self.assertIn("public void ConfigureDelivery(string routeId, Vector3 target)", self.pod)

    def test_player_driving_has_physics_and_exit(self):
        for text in ("AddComponent<Rigidbody>()", "body.AddForce", "body.MoveRotation",
                     "ToggleDriver()", "ForceExit()", "Physics.CheckCapsule"):
            self.assertIn(text, self.pod)

    def test_cinematics_are_skippable_and_lock_input(self):
        self.assertIn("KeyCode.Space", self.cinematic)
        self.assertIn("IsPlaying = false", self.cinematic)
        self.assertIn("InputLocked = true; cinematic.UpdateCamera(); return;", self.game)

    def test_npc_animation_and_contact_placement(self):
        for text in ("SetHomePosition", "leftArm.localRotation", "rightArm.localRotation"):
            self.assertIn(text, self.animation)
        self.assertIn("SetHomePosition(home)", self.game)

    def test_dialogue_is_advanced_one_line_at_a_time(self):
        self.assertIn("Director.Advance()", self.game)
        self.assertIn("Director.CurrentLine", self.game)

    def test_replay_resets_pod(self):
        self.assertIn("drivePod.ResetPod();", self.game)
        self.assertIn("public void ResetPod()", self.pod)

    def test_no_github_actions(self):
        self.assertFalse((ROOT / ".github/workflows").exists())


if __name__ == "__main__":
    unittest.main()
