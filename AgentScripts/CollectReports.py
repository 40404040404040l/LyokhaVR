"""Aggregate executed route reports without changing their original verdicts."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
expected = {"food": "Food", "job": "Job", "wallet_keep": "None", "container": "Container", "police": "Police", "health": "Health"}
results = []
for mode, ending in expected.items():
    path = root / "Verification" / ("playthrough-" + mode + ".json")
    report = json.loads(path.read_text())
    assert report["Status"] == "passed", (mode, report["Status"])
    assert report["Evidence"][-1]["ending"] == ending, (mode, report["Evidence"][-1])
    results.append({"mode": mode, "status": report["Status"], "ending": ending, "elapsed": report["elapsed"], "travelled": report["TravelDistance"], "report": str(path.relative_to(root))})
production = list((root / "Assets/_Project/Scripts/Domain").glob("*.cs")) + list((root / "Assets/_Project/Scripts/Gameplay").glob("*.cs"))
production += [root / "Assets/_Project/Scenes/LyokhaGame.unity", root / "Assets/_Project/Settings/GameConfig.asset"]
hashes = {str(path.relative_to(root)): hashlib.sha256(path.read_bytes()).hexdigest() for path in production}
output = {"status": "passed", "execution": "Aggregate of independently executed scene routes. Five routes passed in the serial suite; health was rerun after correcting the test observer and post-arrest re-navigation. The original reports are unchanged.", "results": results, "productionFileHashesAtAggregation": hashes}
(root / "Verification/routes-aggregate.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n")
print(json.dumps({"status": output["status"], "routes": len(results), "endings": [row["ending"] for row in results]}))
