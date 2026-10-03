#!/usr/bin/env python3
"""Print equipment eligibility metadata from the extracted DE data tables."""

import json
from urllib.request import urlopen

from bs4 import BeautifulSoup

BASE = "https://xenoblade.github.io/xb1de/bdat/bdat_common/"


def rows(name):
    with urlopen(BASE + name + ".html", timeout=30) as response:
        table = BeautifulSoup(response.read(), "html.parser").find("table")
    return table.find_all("tr", recursive=False)


def main():
    result = []
    slots = {"HeadArmor": 1, "BodyArmor": 2, "ArmArmor": 3, "LegArmor": 4, "FootArmor": 5}
    item_slots = {}
    for row in rows("ITM_itemlist"):
        cells = row.find_all("td", recursive=False)
        if cells and cells[2].get_text(strip=True) in slots:
            item_slots[int(cells[0].get_text(strip=True))] = slots[cells[2].get_text(strip=True)]
    owners = [*range(1, 11), 14, 15, 16]
    for table in ("ITM_wpnlist", "ITM_equiplist"):
        for row in rows(table):
            cells = row.find_all("td", recursive=False)
            if not cells:
                continue
            text = [cell.get_text(" ", strip=True) for cell in cells]
            if table == "ITM_wpnlist":
                slot, armor, flag = 0, 0, int(text[6])
                characters = [owner for owner, value in zip(owners, text[24:37]) if int(value)]
            else:
                assert len(cells) == 28
                slot, armor, flag = int(text[3]), int(text[8]), 0
                characters = [index + 1 for index, value in enumerate(text[11:27]) if int(value)]
            for link in cells[1].find_all("a"):
                href = link.get("href", "")
                if href.startswith("ITM_itemlist.html#"):
                    item_id = int(href.split("#")[1])
                    item_slot = item_slots[item_id] if table == "ITM_equiplist" else slot
                    result.append([item_id, item_slot, armor, flag, ",".join(map(str, characters))])
    assert len({row[0] for row in result}) == len(result)
    print(json.dumps(sorted(result)))


if __name__ == "__main__":
    main()
