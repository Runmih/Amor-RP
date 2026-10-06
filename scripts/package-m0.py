"""Bundle verified build outputs and the current source for local M0 testing."""

import hashlib
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
artifacts = root / "artifacts"
plugin = root / "src/AmorRP.Plugin/bin/x64/Release/AmorRP/latest.zip"
server = artifacts / "server"
if not (server / "AmorRP.Server.dll").is_file():
    raise SystemExit("Publish the server to artifacts/server before packaging.")
subprocess.run([sys.executable, str(root / "scripts/check-plugin-package.py"), str(plugin)], check=True)
artifacts.mkdir(exist_ok=True)
shutil.copyfile(plugin, artifacts / "AmorRP-M0-plugin.zip")
with zipfile.ZipFile(artifacts / "AmorRP-M0-server.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(server.rglob("*")):
        if path.is_file():
            archive.write(path, path.relative_to(server))
    archive.write(root / "LICENSE.md", "LICENSE.md")
with zipfile.ZipFile(artifacts / "AmorRP-M0-source.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    excluded = {".git", ".tools", ".vs", "bin", "obj", "artifacts", "__pycache__", "TestResults", ".venv"}
    for directory, folders, files in os.walk(root):
        folders[:] = sorted(folder for folder in folders if folder not in excluded)
        for name in sorted(files):
            if (name == ".env" or name.startswith(".env.") and name != ".env.example"
                    or name.endswith((".user", ".suo", ".local.json"))):
                continue
            path = Path(directory) / name
            if path.is_file() and not path.is_symlink():
                archive.write(path, path.relative_to(root))
checksums = []
for path in sorted(artifacts.glob("AmorRP-M0-*.zip")):
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    checksums.append(f"{digest}  {path.name}")
    print(f"Packaged {path.name} ({path.stat().st_size:,} bytes)")
(artifacts / "SHA256SUMS.txt").write_text("\n".join(checksums) + "\n", encoding="utf-8")
