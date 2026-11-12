"""Offline source regression checks for the narrative-driving milestone.
Run: python -m unittest discover -s tools -p "test_*.py"
Does not replace Unity compilation, Play Mode or a Windows build.
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "Syntera-Unity" / "Assets" / "Syntera" / "Scripts"

class NarrativeVehicleContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.game = (SCRIPTS / "SynteraGame.cs").read_text(encoding="utf-8")
        cls.story = (SCRIPTS / "SynteraCampaignContent.cs").read_text(encoding="utf-8")
        cls.pod = (SCRIPTS / "SynteraDrivePod.cs").read_text(encoding="utf-8")
        cls.cinematic = (SCRIPTS / "SynteraCinematicDirector.cs").read_text(encoding="utf-8")
        cls.animation = (SCRIPTS / "SynteraActorAnimation.cs").read_text(encoding="utf-8")

    def test_twelve_scripted_dialogue_scenes(self):
        self.assertEqual(self.story.count('new [] {'), 12)
        self.assertIn("AURA:", self.story)
        self.assertIn("VANTAGE:", self.story)
        self.assertIn("KATO:", self.story)

    def test_dialogue_precedes_choices(self):
        self.assertIn("if (dialogueIndex < dialogue.Length)", self.game)
        self.assertIn("dialogueIndex++", self.game)
        self.assertIn("dialogueOpen", self.game)

    def test_two_delivery_chapters(self):
        self.assertIn("chapter == 4 || chapter == 7", self.story)
        self.assertIn("if (!IsRouteSatisfied(State.chapter))", self.game)
        self.assertIn("RouteComplete", self.pod)
        self.assertIn("DistanceToWaypoint", self.pod)
        self.assertIn("completedRouteMask", self.game)

    def test_player_driving_has_physics_and_exit(self):
        for text in ("AddComponent<Rigidbody>()", "body.AddForce", "body.MoveRotation",
                     "ToggleDriver()", "ForceExit()", "Physics.CheckCapsule"):
            self.assertIn(text, self.pod)

    def test_cinematics_are_skippable_and_lock_input(self):
        self.assertIn("KeyCode.Space", self.cinematic)
        self.assertIn("IsPlaying = false", self.cinematic)
        self.assertIn("InputLocked = true; cinematic.UpdateCamera(); return;", self.game)

    def test_npc_animation_and_routing(self):
        for text in ("SetHomePosition", "leftArm.localRotation", "rightArm.localRotation"):
            self.assertIn(text, self.animation)
        self.assertIn("PositionMissionContact(true)", self.game)

    def test_save_and_replay(self):
        self.assertIn("State.completedRouteMask |= 1 << State.chapter;", self.game)
        self.assertIn("drivePod.ForceExit();", self.game)
        self.assertIn("SavePath + \".bak\"", self.game)

    def test_mission_outcomes_are_specific_and_budget_checked(self):
        self.assertIn("private static readonly Outcome[,] Outcomes", self.story)
        self.assertEqual(self.story.count("new Outcome("), 24)
        self.assertIn("State.credits + outcome.credits < 0", self.game)
        self.assertIn("State.treasury + outcome.treasury < 0", self.game)
        self.assertIn("State.lastOutcome = outcome.consequence;", self.game)

    def test_replay_repositions_player_pod_and_contact(self):
        self.assertIn("drivePod.ResetPod();", self.game)
        self.assertIn("PositionMissionContact();", self.game)
        self.assertIn("public void ResetPod()", self.pod)

    def test_no_github_actions(self):
        self.assertFalse((ROOT / ".github/workflows").exists())

if __name__ == "__main__":
    unittest.main()
