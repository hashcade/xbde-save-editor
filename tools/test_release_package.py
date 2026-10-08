#!/usr/bin/env python3
"""Extract a platform package and verify its launchers and CLI."""

import argparse
import json
import plistlib
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("rid", choices=("win-x64", "osx-arm64", "osx-x64", "linux-x64"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    version = (root / "VERSION").read_text(encoding="utf-8").strip()
    archive = root / "dist" / f"xbde-save-editor-v{version}-{args.rid}.zip"
    with tempfile.TemporaryDirectory(prefix="xbde-package-test-") as directory:
        target = Path(directory)
        if args.rid.startswith("osx-"):
            subprocess.run(["ditto", "-x", "-k", str(archive), str(target)], check=True)
            app = target / "XBDE Save Editor.app"
            with (app / "Contents/Info.plist").open("rb") as source:
                info = plistlib.load(source)
            assert info["CFBundleShortVersionString"] == version
            assert (app / "Contents/MacOS" / info["CFBundleExecutable"]).is_file()
            if (root / "gui/Assets/app-icon.png").is_file():
                assert info["CFBundleIconFile"] == "AppIcon.icns"
                assert (app / "Contents/Resources/AppIcon.icns").stat().st_size > 0
            subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
            cli = target / "Cli/XbdeEditor.Cli"
        else:
            with zipfile.ZipFile(archive) as source:
                source.extractall(target)
                for member in source.infolist():
                    path = target / member.filename
                    if path.is_file() and args.rid != "win-x64":
                        path.chmod(member.external_attr >> 16)
            suffix = ".exe" if args.rid == "win-x64" else ""
            gui = target / ("XBDE Save Editor.exe" if suffix else "XbdeEditor.Gui")
            assert gui.is_file()
            cli = target / f"XbdeEditor.Cli{suffix}"
        for config in target.rglob("*.runtimeconfig.json"):
            properties = json.loads(config.read_text(encoding="utf-8"))["runtimeOptions"]["configProperties"]
            if properties.get("System.Runtime.InteropServices.BuiltInComInterop.IsSupported", True):
                raise ValueError(f"Shared trimmed runtime requires COM interop to be disabled: {config.name}")
        assert not list(target.rglob("*.pdb"))
        subprocess.run([sys.executable, str(root / "tools/test_cli.py"), "--cli", str(cli)], check=True)
    print("Release package tests passed.")


if __name__ == "__main__":
    main()
