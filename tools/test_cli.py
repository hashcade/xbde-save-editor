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

        skills_original = bytearray(original)
        for character_id in (1, 2):
            record = 0x152368 + (character_id - 1) * 0x138
            struct.pack_into("<5I", skills_original, record + 0x7C, 123, 234, 45, 56, 67)
            struct.pack_into("<5I", skills_original, record + 0x90, 1, 0, 0, 0, 0)
        for flag in (1, 4):  # Shulk's fourth tree and Reyn's fifth tree.
            bit = 0x2CDD + flag
            skills_original[0x50 + (bit >> 3)] |= 1 << (bit & 7)
        skills_source = root / "skills.sav"
        skills_source.write_bytes(skills_original)
        skills = json.loads(run("skills", skills_source, "1"))
        assert [tree["Index"] for tree in skills] == [1, 2, 3, 4, 5]
        assert [tree["IsUnlocked"] for tree in skills] == [True, True, True, True, False]
        assert [tree["CanEdit"] for tree in skills] == [True, True, True, True, False]
        assert [tree["LearnedCount"] for tree in skills] == [1, 0, 0, 0, 0]
        assert [tree["Progress"] for tree in skills] == [123, 234, 45, 56, 67]
        assert [tree["MaximumProgress"] for tree in skills] == [699, 299, 299, 199, 199]
        assert [skill["RequiredSP"] for skill in skills[0]["Skills"]] == [0, 700, 1000, 2000, 3500]
        for count in (0, 3, 5):
            run("skill-tree", skills_source, output, "1", "2", "--learned", count)
            expected = bytearray(skills_original)
            struct.pack_into("<I", expected, 0x152368 + 0x80, 0)
            struct.pack_into("<I", expected, 0x152368 + 0x94, count)
            assert output.read_bytes() == expected
        for progress in (0, 299):
            run("skill-tree", skills_source, output, "1", "2", "--sp", progress)
            expected = bytearray(skills_original)
            struct.pack_into("<I", expected, 0x152368 + 0x80, progress)
            assert output.read_bytes() == expected
        skill_bytes = output.read_bytes()
        for field, invalid_value in (("--learned", "6"), ("--learned", "1.5"),
                                     ("--sp", "300"), ("--sp", "1.5"),
                                     ("--sp", "-1"), ("--sp", "4294967296")):
            run("skill-tree", skills_source, output, "1", "2", field, invalid_value, success=False)
            assert output.read_bytes() == skill_bytes
        run("skill-tree", skills_source, output, "1", "1", "--learned", "0", success=False)
        assert output.read_bytes() == skill_bytes
        for arguments in (("skill-tree", "--learned", "5"), ("skill-tree", "--sp", "0"),
                          ("max-skill-tree",)):
            run(arguments[0], skills_source, output, "1", "5", *arguments[1:], success=False)
            assert output.read_bytes() == skill_bytes
        for tree_index in ("0", "6"):
            run("max-skill-tree", skills_source, output, "1", tree_index, success=False)
            assert output.read_bytes() == skill_bytes
        run("max-skill-tree", skills_source, output, "1", "4")
        expected = bytearray(skills_original)
        struct.pack_into("<I", expected, 0x152368 + 0x88, 0)
        struct.pack_into("<I", expected, 0x152368 + 0x9C, 5)
        assert output.read_bytes() == expected
        completed_source = root / "completed-skills.sav"
        completed_source.write_bytes(expected)
        skill_bytes = output.read_bytes()
        run("skill-tree", completed_source, output, "1", "4", "--sp", "0", success=False)
        assert output.read_bytes() == skill_bytes
        run("max-skills", skills_source, output, "1")
        expected = bytearray(skills_original)
        for tree_index in (1, 2, 3, 4):
            struct.pack_into("<I", expected, 0x152368 + 0x7C + (tree_index - 1) * 4, 0)
            struct.pack_into("<I", expected, 0x152368 + 0x90 + (tree_index - 1) * 4, 5)
        assert output.read_bytes() == expected
        run("max-all-skills", skills_source, output)
        for tree_index in (1, 2, 3, 5):
            struct.pack_into("<I", expected, 0x1524A0 + 0x7C + (tree_index - 1) * 4, 0)
            struct.pack_into("<I", expected, 0x1524A0 + 0x90 + (tree_index - 1) * 4, 5)
        assert output.read_bytes() == expected
        skill_bytes = output.read_bytes()
        run("max-all-skills", output, output)
        assert output.read_bytes() == skill_bytes
        assert skills_source.read_bytes() == skills_original

        future = bytearray(skills_original)
        struct.pack_into("<4H", future, 0x152318, 1, 7, 14, 15)
        future[0x152330] = 4
        for character_id in (1, 7, 14, 15):
            struct.pack_into("<I", future, 0x152368 + (character_id - 1) * 0x138, 60)
        future_source = root / "future-skills.sav"
        future_source.write_bytes(future)
        for character_id in (1, 7, 14, 15):
            future_skills = json.loads(run("skills", future_source, character_id))
            if character_id in (1, 7):
                assert len(future_skills) == 5 and all(not tree["CanEdit"] for tree in future_skills)
            else:
                assert future_skills == []
            for arguments in (("skill-tree", "1", "--learned", "5"),
                              ("skill-tree", "1", "--sp", "0"),
                              ("max-skill-tree", "1"), ("max-skills",)):
                run(arguments[0], future_source, output, character_id, *arguments[1:], success=False)
                assert output.read_bytes() == skill_bytes
        run("max-all-skills", future_source, output, success=False)
        assert output.read_bytes() == skill_bytes
        assert future_source.read_bytes() == future
        assert source.read_bytes() == original

        ambiguous = bytearray(original)
        ambiguous[0x152330] = 1
        source.write_bytes(ambiguous)
        run("character", source, output, "1", "--coins", "1", success=False)
        run("max-arts", source, output, "1", success=False)
        run("max-skills", source, output, "1", success=False)
        run("max-all-skills", source, output, success=False)
        assert output.read_bytes() == skill_bytes
        assert source.read_bytes() == ambiguous
        equipment = bytearray(original)
        weapon = 0x3B10
        for index in range(3):
            offset = weapon + index * 0x30
            struct.pack_into("<5H", equipment, offset, index, 2, 15, 2, 1)
            equipment[offset + 0x10] = 1
            equipment[offset + 0x15] = 3
            struct.pack_into("<I", equipment, offset + 0x18, index | (3 << 16))
        struct.pack_into("<I", equipment, weapon + 0x24, 3297 | (1 << 16))
        struct.pack_into("<2H", equipment, 0x152368 + 0x28, 0, 2)
        struct.pack_into("<2H", equipment, 0x1524A0 + 0x28, 1, 2)
        for index in range(6):
            offset = 0x2C380 + index * 0x2C
            struct.pack_into("<5H", equipment, offset, index, 3, 0, 3, 1)
            equipment[offset + 0x10] = 1
            equipment[offset + 0x16] = 6
            struct.pack_into("<3H", equipment, offset + 0x1A, 1, 1, 100)
        equipment[0x2C380 + 5 * 0x2C + 0x19] = 1
        equipment_source = root / "equipment.sav"
        equipment_source.write_bytes(equipment)
        rows = json.loads(run("equipment", equipment_source, "1"))
        assert [row["Slot"] for row in rows] == ["Weapon", "Head", "Torso", "Arms", "Legs", "Feet"]
        assert rows[0]["Sockets"][0]["GemIndex"] == 0
        assert rows[0]["Sockets"][1]["FixedItemId"] == 3297
        assert not rows[0]["Sockets"][1]["CanEdit"]
        assert [gem["Index"] for gem in rows[0]["Sockets"][0]["AvailableGems"]] == [0, 3, 4]
        run("equipment-gem", equipment_source, output, "1", "Weapon", "1", "3")
        expected_equipment = bytearray(equipment)
        struct.pack_into("<I", expected_equipment, weapon + 0x18, 3 | (3 << 16))
        assert output.read_bytes() == expected_equipment
        for slot, socket, gem in (("Weapon", "1", "-1"), ("Weapon", "1", "1"),
                                  ("Weapon", "1", "2"), ("Weapon", "1", "5"),
                                  ("Weapon", "1", "499"), ("Weapon", "1", "500"),
                                  ("Weapon", "1", "1.5"), ("Weapon", "0", "3"),
                                  ("Weapon", "4", "3"), ("Weapon", "2", "3"),
                                  ("Head", "1", "3"), ("Unknown", "1", "3")):
            run("equipment-gem", equipment_source, output, "1", slot, socket, gem, success=False)
            assert output.read_bytes() == expected_equipment
        run("equipment-gem", equipment_source, output, "14", "Weapon", "1", "3", success=False)
        assert output.read_bytes() == expected_equipment
        run("equipment-gem", output, output, "1", "Weapon", "1", "none")
        struct.pack_into("<I", expected_equipment, weapon + 0x18, 0)
        assert output.read_bytes() == expected_equipment
        assert equipment_source.read_bytes() == equipment
        gems = json.loads(run("gems", equipment_source))
        assert len(gems) == 5 and all(gem["CanEdit"] for gem in gems)
        assert gems[0]["Strength"] == 100 and gems[0]["Chance"] == 0
        run("gem", equipment_source, output, "0", "--effect", "26", "--rank", "6", "--value", "150")
        expected_gem = bytearray(equipment)
        expected_gem[0x2C380 + 0x17] = 5
        struct.pack_into("<2H", expected_gem, 0x2C380 + 0x1C, 26, 150 | (25 << 8))
        assert output.read_bytes() == expected_gem
        run("max-gem", output, output, "0")
        struct.pack_into("<H", expected_gem, 0x2C380 + 0x1E, 6600)
        assert output.read_bytes() == expected_gem
        decoded = json.loads(run("gems", output))[0]
        assert decoded["Strength"] == 200 and decoded["Chance"] == 25 and decoded["HasValidValue"]
        for field, invalid_value in (("--value", "201"), ("--value", "149"), ("--value", "1.5"),
                                     ("--value", "-1"), ("--value", "4294967295"), ("--rank", "7"),
                                     ("--effect", "98"), ("--effect", "0"), ("--unknown", "1")):
            run("gem", output, output, "0", field, invalid_value, success=False)
            assert output.read_bytes() == expected_gem
        for invalid_index in ("5", "499", "500", "-1"):
            run("max-gem", equipment_source, output, invalid_index, success=False)
            assert output.read_bytes() == expected_gem
        run("gem", equipment_source, output, "0", "--value", "99", "--value", "98", success=False)
        assert output.read_bytes() == expected_gem
        assert equipment_source.read_bytes() == equipment
        switching = bytearray(equipment)
        struct.pack_into("<H", switching, weapon + 4, 2)
        struct.pack_into("<H", switching, weapon + 2 * 0x30 + 4, 2)
        switching_source = root / "switching.sav"
        switching_source.write_bytes(switching)
        rows = json.loads(run("equipment", switching_source, "1"))
        assert rows[0]["CanSwitch"]
        assert [item["Index"] for item in rows[0]["AvailableItems"]] == [0, 2]
        run("equip", switching_source, output, "1", "Weapon", "2")
        switched = bytearray(switching)
        struct.pack_into("<I", switched, 0x152368 + 0x28, 2 | (2 << 16))
        assert output.read_bytes() == switched
        for slot, index in (("Weapon", "-1"), ("Weapon", "1"), ("Weapon", "499"),
                            ("Weapon", "500"), ("Weapon", "1.5"), ("Unknown", "0"), ("Head", "2")):
            run("equip", switching_source, output, "1", slot, index, success=False)
            assert output.read_bytes() == switched
        run("equip", output, output, "1", "Weapon", "0")
        assert output.read_bytes() == switching
        assert switching_source.read_bytes() == switching
        inventory_source = root / "inventory.sav"
        inventory_source.write_bytes(original)
        inventory = json.loads(run("inventory", inventory_source, "Collectables"))
        assert inventory["Capacity"] == 500 and not inventory["Items"]
        item_id = inventory["Catalog"][0]["Id"]
        run("add-item", inventory_source, output, str(item_id), "--quantity", "5")
        added_item = bytearray(original)
        struct.pack_into("<5H", added_item, 0x31970, 0, 10, item_id, 10, 5)
        struct.pack_into("<I", added_item, 0x3197C, 1)
        added_item[0x31980] = 1
        struct.pack_into("<I", added_item, 0x46928, 1)
        assert output.read_bytes() == added_item
        run("item", output, output, "Collectables", "0", "--quantity", "8")
        struct.pack_into("<H", added_item, 0x31978, 8)
        assert output.read_bytes() == added_item
        run("max-items", output, output, "Collectables")
        struct.pack_into("<H", added_item, 0x31978, 99)
        assert output.read_bytes() == added_item
        for invalid in ("0", "100", "-1", "1.5", "4294967295"):
            run("item", output, output, "Collectables", "0", "--quantity", invalid, success=False)
            assert output.read_bytes() == added_item
        run("add-item", output, output, str(item_id), "--quantity", "1", success=False)
        run("max-items", output, output, "KeyItems", success=False)
        assert output.read_bytes() == added_item
        run("delete-item", output, output, "Collectables", "0")
        added_item[0x31980] = 0
        assert output.read_bytes() == added_item
        run("add-gem", equipment_source, output, "--effect", "26", "--rank", "6", "--value", "200")
        created_gem = bytearray(equipment)
        gem_offset = 0x2C380 + 6 * 44
        struct.pack_into("<5H", created_gem, gem_offset, 6, 3, 0, 3, 1)
        struct.pack_into("<I", created_gem, gem_offset + 12, 1)
        created_gem[gem_offset + 16] = 1
        created_gem[gem_offset + 22] = 6
        created_gem[gem_offset + 23] = 5
        struct.pack_into("<3H", created_gem, gem_offset + 26, 1, 26, 6600)
        struct.pack_into("<I", created_gem, 0x4690C, 1)
        assert output.read_bytes() == created_gem
        for index in ("0", "2", "5", "499", "-1"):
            run("delete-gem", output, output, index, success=False)
            assert output.read_bytes() == created_gem
        run("delete-gem", output, output, "6")
        created_gem[gem_offset + 16] = 0
        assert output.read_bytes() == created_gem
        assert inventory_source.read_bytes() == original
        assert not list(root.glob(".xbde-*.tmp"))
    print("CLI tests passed.")


if __name__ == "__main__":
    main()
