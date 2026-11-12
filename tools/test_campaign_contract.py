"""Offline source contracts for the SYNTERA Unity presentation layer.
Run: python -m unittest discover -s tools -p "test_*.py"
These do NOT certify Unity compilation or runtime gameplay; the EditMode tests and
tools/verify-unity-local.ps1 do that.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "Syntera-Unity/Assets/Syntera/Scripts"
CORE = ROOT / "Syntera-Unity/Assets/Syntera/Core"


class PresentationLayerContract(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.game = (SCRIPTS / "SynteraGame.cs").read_text(encoding="utf-8")
        cls.world = (SCRIPTS / "SynteraWorld.cs").read_text(encoding="utf-8")

    def test_game_uses_the_core_director_not_its_own_economy(self):
        self.assertIn("CampaignDirector", self.game)
        self.assertIn("CampaignRules.Load(", self.game)
        self.assertIn("DialogueLibrary.Load(", self.game)
        for forbidden in ("State.credits +=", "State.trust =", "GetOutcome(", "leftChoices", "rightChoices"):
            self.assertNotIn(forbidden, self.game, forbidden)

    def test_old_content_is_gone(self):
        self.assertFalse((SCRIPTS / "SynteraCampaignContent.cs").exists())
        for old in ("SENTINEL", "Mayor Adrian Cross", "Dr. Elias Morgan", "Nexus Central"):
            self.assertNotIn(old, self.game + self.world, old)

    def test_contact_must_be_near_and_objectives_done(self):
        self.assertIn("InteractionRadius", self.game)
        self.assertIn("Director.CanTalkToContact", self.game)
        self.assertIn("Input.GetKeyDown(KeyCode.E)", self.game)

    def test_unaffordable_choices_are_greyed_with_shortfall(self):
        self.assertIn("GUI.enabled = status.Affordable", self.game)
        self.assertIn("status.Explanation", self.game)
        self.assertIn("CreditsShortfallAfterCreditLine", self.game)

    def test_save_is_atomic_with_backup_and_replay_validation(self):
        self.assertIn('SavePath + ".bak"', self.game)
        self.assertIn("File.Replace(temp, SavePath, backup)", self.game)
        self.assertIn("CampaignSave.Read(", self.game)
        self.assertIn("recoveredFromBackup", self.game)

    def test_restart_needs_confirmation_and_legacy_save_untouched(self):
        self.assertIn("confirmRestart = true", self.game)
        self.assertIn("LegacySavePath", self.game)
        self.assertNotIn("File.Delete(LegacySavePath)", self.game)

    def test_2050_is_a_separate_labelled_prototype(self):
        proto = (SCRIPTS / "SynteraFuturePrototype.cs").read_text(encoding="utf-8")
        self.assertIn("PROTOTYPE", proto)
        self.assertIn("SCENARIO, NOT A CERTAINTY", proto)
        self.assertNotIn("Engine.", proto)

    def test_accessibility_basics(self):
        self.assertIn("textScale", self.game)
        self.assertIn("SpeakerName", self.game)
        self.assertIn("KeyCode.F3", self.game)

    def test_npcs_are_tagged(self):
        self.assertIn("AddComponent<SynteraCharacter>()", self.world)
        self.assertIn("public string characterName;", self.world)

    def test_no_workflows(self):
        self.assertFalse((ROOT / ".github/workflows").exists())


if __name__ == "__main__":
    unittest.main()
