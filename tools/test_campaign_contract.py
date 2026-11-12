"""Offline regression checks for the SYNTERA Unity story and save pipeline.
Run: python -m unittest discover -s tools -p 'test_*.py'
No GitHub Actions or network access required.
These source contracts do NOT certify Unity compilation or runtime gameplay.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
GAME = ROOT / "Syntera-Unity/Assets/Syntera/Scripts/SynteraGame.cs"
WORLD = ROOT / "Syntera-Unity/Assets/Syntera/Scripts/SynteraWorld.cs"

class CampaignSourceContract(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.game = GAME.read_text(encoding="utf-8")
        cls.world = WORLD.read_text(encoding="utf-8")

    def test_twelve_missions_have_named_contacts(self):
        segment = self.game.split("missionContacts =", 1)[1].split(";", 1)[0]
        self.assertEqual(segment.count('"') // 2, 12)

    def test_decision_requires_open_npc_dialogue(self):
        self.assertIn("if (State.chapter >= MaxChapters || !dialogueOpen) return;", self.game)
        self.assertIn("Vector3.Distance(player.transform.position, nearby.transform.position) <= InteractionRadius", self.game)
        self.assertIn("Input.GetKeyDown(KeyCode.E)", self.game)

    def test_npcs_are_tagged(self):
        self.assertIn("AddComponent<SynteraCharacter>()", self.world)
        self.assertIn("public string characterName;", self.world)

    def test_save_has_backup_and_validation(self):
        self.assertIn('SavePath + ".bak"', self.game)
        self.assertIn("File.Copy(SavePath, backup, true)", self.game)
        self.assertIn("foreach (string path in new[] { SavePath, SavePath + \".bak\" })", self.game)
        self.assertIn("parsed.decisions.Length != 12", self.game)

    def test_replay_and_future_progress(self):
        self.assertIn('Replay 2026–2028', self.game)
        self.assertIn('Continue to 2050', self.game)
        self.assertIn('State.chapter = 12;', self.game)
        self.assertIn("private void AdvanceFuture(bool automate)", self.game)

    def test_no_workflows(self):
        self.assertFalse((ROOT / ".github/workflows").exists())

if __name__ == "__main__":
    unittest.main()
