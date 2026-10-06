from pathlib import Path
import re
import unittest


ROOT = Path(__file__).resolve().parents[1]
EVENT_SCENE = (ROOT / "bin" / "EventScene.cs").read_text(encoding="utf-8-sig")
MATERIAL_POINTS = (ROOT / "bin" / "EventMaterialPoints.cs").read_text(encoding="utf-8-sig")
EVENT_CONFIG = (ROOT / "bin" / "event.ini").read_text(encoding="utf-8-sig")


class EventMaterialPointsTests(unittest.TestCase):
    def test_event_config_grants_material_points(self):
        values = re.findall(r"materialPoints\(([^)]*)\)", EVENT_CONFIG)
        self.assertTrue(values)
        self.assertTrue(all(value.isdigit() and int(value) >= 0 for value in values))

    def test_event_effect_updates_state_and_world_map_label(self):
        self.assertIn('s.StartsWith("materialPoints(")', EVENT_SCENE)
        self.assertIn("EventMaterialPoints.TryParse", EVENT_SCENE)
        self.assertIn("BattleStateManager.MaterialPoints =", EVENT_SCENE)
        self.assertIn('GetNodeOrNull<Label>("pointNum")', EVENT_SCENE)

    def test_invalid_values_are_rejected_and_logged(self):
        self.assertIn("int.TryParse(amountText, out amount)", MATERIAL_POINTS)
        self.assertIn("amount >= 0", MATERIAL_POINTS)
        self.assertIn("GD.PushError", MATERIAL_POINTS)
        self.assertIn("DateTime.Now:yyyy-MM-dd HH:mm:ss", MATERIAL_POINTS)

    def test_addition_is_clamped_to_integer_range(self):
        self.assertIn("(long)current + amount", MATERIAL_POINTS)
        self.assertIn("int.MaxValue", MATERIAL_POINTS)


if __name__ == "__main__":
    unittest.main(verbosity=2)
