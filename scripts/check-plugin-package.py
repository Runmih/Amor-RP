"""Check the actual package, including identity and host-library exclusions."""

import json
import sys
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[1]
package = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "src/AmorRP.Plugin/bin/x64/Release/AmorRP/latest.zip"
with zipfile.ZipFile(package) as archive:
    names = set(archive.namelist())
    required = {"AmorRP.dll", "AmorRP.Contracts.dll", "AmorRP.json", "AmorRP.deps.json"}
    assert required <= names, f"Missing package files: {required - names}"
    manifest = json.loads(archive.read("AmorRP.json"))
    assert manifest["InternalName"] == "AmorRP"
    assert manifest["DalamudApiLevel"] == 15
    expected_version = ET.parse(root / "src/AmorRP.Plugin/AmorRP.Plugin.csproj").findtext(".//Version")
    assert manifest["AssemblyVersion"] == expected_version
    assert manifest["RepoUrl"] == "https://github.com/Runmih/Amor-RP"
    for name in names:
        assert Path(name).name == name, f"Unexpected nested or unsafe entry: {name}"
        if name.lower().endswith(".dll"):
            assert name in {"AmorRP.dll", "AmorRP.Contracts.dll"}, f"Unexpected bundled DLL: {name}"
print(f"PASS: {package.name}; API 15, correct identity, contracts included, no host DLLs")
