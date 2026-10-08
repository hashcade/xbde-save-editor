#!/usr/bin/env python3
"""Print Collectopaedia definitions or reward rows from the extracted DE tables."""

import argparse
from urllib.request import urlopen

from bs4 import BeautifulSoup


def referenced_id(cell, table_name: str) -> int:
    links = [link for link in cell.find_all("a") if table_name in link["href"]]
    if len(links) != 1:
        raise ValueError(f"Expected one {table_name} reference.")
    return int(links[0]["href"].split("#")[-1])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rewards", action="store_true")
    args = parser.parse_args()
    path = "bdat_menu_item/MNU_col.html" if args.rewards else "bdat_common/ITM_collectlist.html"
    with urlopen("https://xenobladedata.github.io/xb1de/bdat/" + path, timeout=30) as response:
        soup = BeautifulSoup(response.read(), "html.parser")
    rows = []
    for row in soup.select("tr"):
        cells = row.find_all("td", recursive=False)
        if not cells:
            continue
        row_id = int(cells[0].get_text())
        if args.rewards:
            rows.append([row_id, referenced_id(cells[2], "ITM_itemlist"), cells[2].get_text(strip=True)])
        elif row_id <= 300 or 319 <= row_id <= 346:
            category = int(cells[4].get_text())
            if not 1 <= category <= 8:
                raise ValueError("Unknown Collectopaedia category.")
            rows.append([row_id, referenced_id(cells[1], "ITM_itemlist"),
                         referenced_id(cells[3], "FLD_maplist"), cells[3].get_text(strip=True), category])
    expected = list(range(1, 134)) if args.rewards else list(range(1, 301)) + list(range(319, 347))
    if [row[0] for row in rows] != expected:
        raise ValueError("The extracted table has changed; review the native ID ranges before updating.")
    if args.rewards:
        with urlopen("https://xenobladedata.github.io/xb1de/bdat/bdat_common/ITM_itemlist.html", timeout=30) as response:
            item_soup = BeautifulSoup(response.read(), "html.parser")
        item_rows = {int(row["id"]): row.find_all("td", recursive=False)
                     for row in item_soup.select("tr[id]") if row["id"].isdigit()}
        types = {"Weapon": 2, "Gem": 3, "HeadArmor": 4, "BodyArmor": 5,
                 "ArmArmor": 6, "LegArmor": 7, "FootArmor": 8}
        for row in rows:
            cells = item_rows[row[1]]
            item_type = types[cells[2].get_text(strip=True)]
            effect, rank, strength = 0, 0, 0
            if item_type == 3:
                effect = referenced_id(cells[3], "BTL_skilllist")
                rank, strength = (int(cells[index].get_text()) for index in (6, 7))
            row.extend([item_type, effect, rank, strength])
    print("id\titem_id\tname\ttype\teffect_id\trank\tfixed_strength" if args.rewards
          else "id\titem_id\tmap_id\tmap_name\tcategory")
    for row in rows:
        fields = [str(value) for value in row]
        if any("\t" in value or "\n" in value for value in fields):
            raise ValueError("Unexpected table delimiter in a game label.")
        print("\t".join(fields))


if __name__ == "__main__":
    main()
