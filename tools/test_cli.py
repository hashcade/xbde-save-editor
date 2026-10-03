#!/usr/bin/env python3
"""Exercise the CLI using generated saves, without distributing game files."""

import argparse
import json
import struct
import subprocess
import tempfile
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cli", required=True)
    args = parser.parse_args()
    executable = Path(args.cli).resolve()
    prefix = ["dotnet", str(executable)] if executable.suffix == ".dll" else [str(executable)]

    def run(*arguments: str, success: bool = True) -> str:
        result = subprocess.run([*prefix, *map(str, arguments)], text=True, capture_output=True)
        assert (result.returncode == 0) == success, (arguments, result.stdout, result.stderr)
        return result.stdout

    with tempfile.TemporaryDirectory(prefix="xbde-cli-") as directory:
        root = Path(directory)
        original = bytearray(0x153860)
        struct.pack_into("<H", original, 0x152318, 1)
        original[0x152330] = 1
        struct.pack_into("<I", original, 0x152368, 20)
        source = root / "bfsgame00.sav"
        output = root / "output.sav"
        source.write_bytes(original)
        assert run("--version").strip()
        assert json.loads(run("inspect", source))["PartyIds"] == [1]
        run("copy", source, output)
        assert output.read_bytes() == original
        run("resources", source, output, "--money", "123", "--noponstones", "456")
        changed = output.read_bytes()
        assert struct.unpack_from("<I", changed, 0x151B40)[0] == 123
        assert struct.unpack_from("<I", changed, 0x10)[0] == 456
        for index, (before, after) in enumerate(zip(original, changed)):
            assert before == after or 0x10 <= index < 0x14 or 0x151B40 <= index < 0x151B44
        for invalid in ("-1", "1.5", "4294967296", "wrong"):
            run("resources", source, output, "--money", invalid, success=False)
            assert output.read_bytes() == changed
        run("resources", source, output, "--money", "1", "--money", "2", success=False)
        run("resources", source, output, "--unknown", "1", success=False)
        run("copy", source, source, success=False)
        invalid = root / "invalid.sav"
        invalid.write_bytes(b"invalid")
        run("inspect", invalid, success=False)
        assert source.read_bytes() == original
        assert not list(root.glob(".xbde-*.tmp"))
    print("CLI tests passed.")


if __name__ == "__main__":
    main()
