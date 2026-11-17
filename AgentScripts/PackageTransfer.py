"""Create a portable Unity source archive, without machine-specific caches or credentials."""
import hashlib
import json
from pathlib import Path
import zipfile

project = Path(__file__).resolve().parents[1]
workspace = project.parent
output = workspace / "Transfer" / "LyokhaVR-Quest3-2026-11-17.zip"
output.parent.mkdir(exist_ok=True)

files = []
for folder in ("Assets", "Packages", "ProjectSettings", "AgentScripts", "Documentation"):
    for path in sorted((project / folder).rglob("*")):
        if path.is_file() and path.name != ".DS_Store" and "__pycache__" not in path.parts:
            if path.is_symlink():
                raise RuntimeError(f"Resolve external file before packing: {path}")
            files.append((path, "LyokhaVR/" + path.relative_to(project).as_posix()))
files += [(project / ".gitignore", "LyokhaVR/.gitignore")]
for name in ("trash.blend", "Avtozak_Textured.blend"):
    files.append((workspace / name, "SourceFiles/" + name))

instructions = (project / "Documentation/TRANSFER_TO_VR_PC.md").read_text(encoding="utf-8")
manifest = []
with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as archive:
    archive.writestr("START_HERE.txt", instructions.encode("utf-8-sig"))
    for path, name in files:
        data = path.read_bytes()
        archive.writestr(name, data)
        manifest.append({"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
    archive.writestr("FILE_MANIFEST.json", json.dumps(manifest, ensure_ascii=False, indent=2))

with zipfile.ZipFile(output) as archive:
    bad = archive.testzip()
    if bad:
        raise RuntimeError(f"ZIP CRC check failed: {bad}")
    names = set(archive.namelist())
    required = ["LyokhaVR/ProjectSettings/ProjectVersion.txt", "LyokhaVR/Packages/manifest.json", "LyokhaVR/Packages/packages-lock.json"]
    required += [f"LyokhaVR/Assets/_Project/Scenes/{scene}.unity" for scene in ("MainMenuDesktop", "CourtyardDesktop", "DetentionDesktop", "MainMenuVR", "CourtyardVR", "DetentionVR")]
    assert all(name in names for name in required), "Missing required Unity project files."
    assert not any("/Library/" in name or "/.git/" in name or "/Logs/" in name for name in names)
    # Every shipped Unity asset must retain its GUID metadata.
    assets = [name for name in names if name.startswith("LyokhaVR/Assets/") and not name.endswith(".meta")]
    assert all(name + ".meta" in names for name in assets), "Asset metadata missing."
    for item in manifest:
        assert hashlib.sha256(archive.read(item["path"])).hexdigest() == item["sha256"], item["path"]

digest = hashlib.sha256(output.read_bytes()).hexdigest()
output.with_suffix(".zip.sha256").write_text(digest + "  " + output.name + "\n", encoding="utf-8")
report = {"path": str(output), "bytes": output.stat().st_size, "files": len(manifest), "sha256": digest, "crcVerified": True, "assetMetadataVerified": True, "requiredScenes": 6}
output.with_suffix(".report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
