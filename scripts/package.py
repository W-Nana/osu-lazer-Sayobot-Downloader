#!/usr/bin/env python3
"""Create a one-DLL end-user package, excluding all game/runtime dependencies."""
import hashlib
from pathlib import Path
import shutil
import zipfile

root = Path(__file__).resolve().parent.parent
assembly = root / "osu.Game.Rulesets.Sayobot/bin/Release/net10.0/osu.Game.Rulesets.Sayobot.dll"
if not assembly.is_file():
    raise SystemExit("Build the Release configuration first.")
output = root / "artifacts"
output.mkdir(exist_ok=True)
target = output / assembly.name
shutil.copy2(assembly, target)
archive = output / "SayobotDownload-0.1.0-lazer-2026.1005.0.zip"
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as package:
    package.write(target, target.name)
    package.write(root / "README.md", "README.md")
    package.write(root / "VALIDATION.md", "VALIDATION.md")
digest = hashlib.sha256(target.read_bytes()).hexdigest()
(output / "SHA256SUMS").write_text(f"{digest}  {target.name}\n", encoding="utf-8")
print(f"DLL: {target}\nZIP: {archive}\nSHA-256: {digest}")
