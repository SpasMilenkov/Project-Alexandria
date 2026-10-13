import json
import os
from pathlib import Path
import shlex
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "init.sh"


class LayoutCapacityTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="garage-capacity-offline-")
        self.root = Path(self.temporary.name)
        self.output = self.root / "output"
        self.state = self.root / "layout.json"
        self.state.write_text(json.dumps({"version": 0, "roles": [], "stagedRoleChanges": [], "stagedParameters": None}))
        self.log = self.root / "calls.jsonl"
        self.config = self.root / "garage.toml"
        self.config.write_text("# fixture config\n")
        fake = self.root / "garage.py"
        fake.write_text('''import json, os, sys
from pathlib import Path
state_path = Path(os.environ["FIXTURE_LAYOUT"])
state = json.loads(state_path.read_text())
arguments = sys.argv[1:]
with Path(os.environ["FIXTURE_CALLS"]).open("a") as log:
    log.write(json.dumps(arguments) + "\\n")
if arguments == ["json-api", "GetClusterLayout"]:
    print(json.dumps(state))
elif arguments[:2] == ["layout", "assign"]:
    state["assignedCapacity"] = int(arguments[-2][:-1]) * 1000000000
    state_path.write_text(json.dumps(state))
elif arguments[:2] == ["layout", "apply"]:
    state["roles"] = [{"id": "fixture-node", "capacity": state.pop("assignedCapacity")}]
    state["version"] += 1
    state_path.write_text(json.dumps(state))
else:
    raise SystemExit("unsupported fixture CLI request")
''')
        source = SCRIPT.read_text()
        start = source.index("CAPACITY_ARGUMENT=")
        end = source.index("# ── Buckets", start)
        body = source[start:end].replace("/output", str(self.output)).replace("/etc/garage.toml", str(self.config))
        self.script = self.root / "test.sh"
        self.script.write_text("set -eu\numask 077\nNODE_ID=fixture-node\ng() { python3 " + shlex.quote(str(fake)) + ' "$@"; }\n' + body)

    def tearDown(self):
        self.temporary.cleanup()

    def run_layout(self, capacity="10G", reviewed="false"):
        return subprocess.run(["/bin/sh", str(self.script)], check=False, text=True, capture_output=True,
                              env=dict(os.environ, GARAGE_CAPACITY=capacity, GARAGE_CAPACITY_CHANGE_REQUESTED=reviewed,
                                       FIXTURE_LAYOUT=str(self.state), FIXTURE_CALLS=str(self.log)))

    def mutations(self):
        calls = [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []
        return [call for call in calls if call[:1] == ["layout"]]

    def test_fresh_minimum_applies_one_gb_and_emits_actual_bytes(self):
        result = self.run_layout("1G")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.output / "garage-capacity.bytes").read_text(), "1000000000\n")
        self.assertEqual(len(self.mutations()), 2)

    def test_rerun_stale_default_preserves_actual_capacity_and_data(self):
        first = self.run_layout("25G")
        self.assertEqual(first.returncode, 0, first.stderr)
        before = self.state.read_text()
        self.log.unlink()

        rerun = self.run_layout("10G")

        self.assertEqual(rerun.returncode, 0, rerun.stderr)
        self.assertEqual(self.state.read_text(), before)
        self.assertEqual(self.mutations(), [])
        self.assertIn("Existing layout capacity preserved: 25000000000 bytes", rerun.stdout)
        self.assertEqual((self.output / "garage-capacity.bytes").read_text(), "25000000000\n")

    def test_explicit_change_rejected_without_layout_or_receipt_mutation(self):
        self.assertEqual(self.run_layout("25G").returncode, 0)
        before = self.state.read_text()
        self.log.unlink()

        changed = self.run_layout("30G", reviewed="true")

        self.assertNotEqual(changed.returncode, 0)
        self.assertIn("Existing Garage layout capacity is 25000000000 bytes", changed.stderr)
        self.assertIn("data were preserved", changed.stderr)
        self.assertEqual(self.state.read_text(), before)
        self.assertEqual(self.mutations(), [])

    def test_unknown_repair_capacity_is_read_and_preserved(self):
        self.assertEqual(self.run_layout("25G").returncode, 0)
        self.log.unlink()

        result = self.run_layout("")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.output / "garage-capacity.bytes").read_text(), "25000000000\n")
        self.assertEqual(self.mutations(), [])

    def test_staged_changes_are_never_committed_by_installer(self):
        layout = json.loads(self.state.read_text())
        layout["stagedRoleChanges"] = [{"id": "fixture-node", "capacity": 123}]
        self.state.write_text(json.dumps(layout))

        result = self.run_layout()

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("unreviewed staged layout changes", result.stderr)
        self.assertEqual(self.mutations(), [])

    def test_capacity_below_minimum_rejected_before_layout_changes(self):
        result = self.run_layout("0G")

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("at least 1 GB", result.stderr)
        self.assertEqual(self.mutations(), [])


if __name__ == "__main__":
    unittest.main()
