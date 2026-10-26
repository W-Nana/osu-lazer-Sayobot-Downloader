#!/usr/bin/env python3
"""Extract the pinned official Linux release for compilation; never installs the game."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parent.parent
VERSION = "2026.1005.0"
BASE = f"https://github.com/ppy/osu/releases/download/{VERSION}-lazer/"
TOOLS = ROOT / ".tools"
DESTINATION = TOOLS / f"lazer-{VERSION}"
PACKAGE_NAME = f"osulazer-{VERSION}-lazer-linux-x64-full.nupkg"


def main():
    if (DESTINATION / "usr/bin/osu.Game.dll").is_file():
        print(f"Using official reference assemblies: {DESTINATION / 'usr/bin'}")
        return
    if not shutil.which("unsquashfs"):
        raise SystemExit("unsquashfs is required to extract the official AppImage, or supply LazerReferenceDirectory when building.")

    TOOLS.mkdir(exist_ok=True)
    package = TOOLS / f"lazer-{VERSION}.nupkg"
    if not package.exists():
        print(f"Downloading {PACKAGE_NAME}", flush=True)
        with urllib.request.urlopen(BASE + PACKAGE_NAME, timeout=60) as src, package.open("wb") as dst:
            shutil.copyfileobj(src, dst)

    # Velopack's release manifest, when published, supplies a checksum for the full package.
    with urllib.request.urlopen(BASE + "releases.linux-x64.json", timeout=30) as response:
        manifest = json.load(response)
    assets = manifest.get("Assets", [])
    asset = next((item for item in assets if item.get("FileName") == PACKAGE_NAME), None)
    if asset is None:
        raise SystemExit("Pinned full package is absent from the official release manifest.")
    if asset.get("Size") and package.stat().st_size != int(asset["Size"]):
        raise SystemExit("Reference package size does not match the official manifest.")
    algorithm, expected = ("sha256", asset["SHA256"]) if asset.get("SHA256") else ("sha1", asset.get("SHA1"))
    if not expected:
        raise SystemExit("The official manifest contains no usable package checksum.")
    with package.open("rb") as stream:
        actual = hashlib.file_digest(stream, algorithm).hexdigest()
    if actual.lower() != expected.lower():
        raise SystemExit("Reference package checksum does not match the official manifest.")

    appimage = TOOLS / "osulazer.AppImage"
    with zipfile.ZipFile(package) as archive:
        appimage.write_bytes(archive.read("lib/app/osulazer.AppImage"))
    appimage.chmod(0o755)
    offset = subprocess.check_output([str(appimage), "--appimage-offset"], text=True).strip()
    subprocess.run(["unsquashfs", "-d", str(DESTINATION), "-offset", offset,
                    "-no-progress", str(appimage), "usr/bin"], check=True)
    print(f"Prepared official reference assemblies: {DESTINATION / 'usr/bin'}")


if __name__ == "__main__":
    main()
