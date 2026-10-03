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
        struct.pack_into("<H", original, 0x15231A, 2)
        original[0x152330] = 2
        struct.pack_into("<I", original, 0x152368, 20)
        struct.pack_into("<I", original, 0x1524A0, 20)
        original[0x1536E8] = 1
        original[0x1536E8 + 22] = 3
        original[0x1536E8 + 20] = 1
        source = root / "bfsgame00.sav"
        output = root / "output.sav"
        source.write_bytes(original)
        assert run("--version").strip()
        assert json.loads(run("inspect", source))["PartyIds"] == [1, 2]
        run("copy", source, output)
        assert output.read_bytes() == original
        run("resources", source, output, "--money", "123", "--noponstones", "456")
        changed = output.read_bytes()
        assert struct.unpack_from("<I", changed, 0x151B40)[0] == 123
        assert struct.unpack_from("<I", changed, 0x10)[0] == 456
        for index, (before, after) in enumerate(zip(original, changed)):
            assert before == after or 0x10 <= index < 0x14 or 0x151B40 <= index < 0x151B44
        run("character", source, output, "1", "--ap", "321", "--coins", "999")
        character_bytes = output.read_bytes()
        assert struct.unpack_from("<II", character_bytes, 0x152370) == (321, 999)
        assert character_bytes[:0x152370] == original[:0x152370]
        assert character_bytes[0x152378:] == original[0x152378:]
        run("character", source, output, "1", "--reserve-exp", "199999998")
        reserve_bytes = output.read_bytes()
        assert struct.unpack_from("<I", reserve_bytes, 0x15245C)[0] == 199999998
        assert reserve_bytes[:0x15245C] == original[:0x15245C]
        assert reserve_bytes[0x152460:] == original[0x152460:]
        run("progression", source, output, "1", "--level", "1", "--exp", "101")
        progression = output.read_bytes()
        assert struct.unpack_from("<II", progression, 0x152368) == (3, 1)
        assert struct.unpack_from("<II", progression, 0x152454) == (3, 0)
        for index, (before, after) in enumerate(zip(original, progression)):
            assert before == after or 0x152368 <= index < 0x152370 or 0x152454 <= index < 0x15245c
        for field, invalid_value in (("--level", "0"), ("--level", "100"), ("--exp", "100000000"), ("--exp", "1.5")):
            run("progression", source, output, "1", field, invalid_value, success=False)
            assert output.read_bytes() == progression
        output.write_bytes(reserve_bytes)
        for field, invalid_value in (("--ap", "100000000"), ("--reserve-exp", "199999999")):
            run("character", source, output, "1", field, invalid_value, success=False)
            assert output.read_bytes() == reserve_bytes
        run("character", source, output, "1", "--ap", "1", "--reserve-exp", "199999999", success=False)
        assert output.read_bytes() == reserve_bytes
        run("max-ap", source, output)
        bulk = output.read_bytes()
        for record in (0x152368, 0x1524A0):
            assert struct.unpack_from("<I", bulk, record + 8)[0] == 99999999
        for index, (before, after) in enumerate(zip(original, bulk)):
            assert before == after or 0x152370 <= index < 0x152374 or 0x1524A8 <= index < 0x1524AC
        output.write_bytes(character_bytes)
        run("character", source, output, "1", "--ap", "1", "--coins", "1000", success=False)
        assert output.read_bytes() == character_bytes
        run("character", source, output, "14", "--ap", "1", success=False)
        run("character", source, output, "1", "--ap", "-1", success=False)
        output.write_bytes(changed)
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
        arts = json.loads(run("arts", source, "1"))
        slash = next(art for art in arts if art["Id"] == 12)
        assert slash["Level"] == 3 and slash["MaximumLevel"] == 12
        run("art", source, output, "1", "12", "--level", "8")
        art_bytes = output.read_bytes()
        assert art_bytes[0x1536E8 + 22:0x1536E8 + 24] == bytes((8, 3))
        assert art_bytes[:0x1536E8 + 22] == original[:0x1536E8 + 22]
        assert art_bytes[0x1536E8 + 24:] == original[0x1536E8 + 24:]
        for invalid in ("0", "13", "1.5", "-1", "4294967295", "4294967296"):
            run("art", source, output, "1", "12", "--level", invalid, success=False)
            assert output.read_bytes() == art_bytes
        for art_id in ("1", "17", "20", "189", "invalid"):
            run("max-art", source, output, "1", art_id, success=False)
            assert output.read_bytes() == art_bytes
        run("max-art", source, output, "1", "12")
        assert output.read_bytes()[0x1536E8 + 22:0x1536E8 + 24] == bytes((12, 7))
        run("max-arts", source, output, "1")
        bulk_arts = output.read_bytes()
        assert bulk_arts[0x1536E8 + 20:0x1536E8 + 24] == bytes((12, 7, 12, 7))
        assert bulk_arts[:0x1536E8 + 20] == original[:0x1536E8 + 20]
        assert bulk_arts[0x1536E8 + 24:] == original[0x1536E8 + 24:]
        assert source.read_bytes() == original
        ambiguous = bytearray(original)
        ambiguous[0x152330] = 1
        source.write_bytes(ambiguous)
        run("character", source, output, "1", "--coins", "1", success=False)
        run("max-arts", source, output, "1", success=False)
        assert output.read_bytes() == bulk_arts
        assert source.read_bytes() == ambiguous
    print("CLI tests passed.")


if __name__ == "__main__":
    main()
