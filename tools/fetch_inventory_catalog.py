#!/usr/bin/env python3
"""Print named stackable-item definitions from the extracted DE item table."""

from urllib.request import urlopen

from bs4 import BeautifulSoup


def main():
    url = "https://xenoblade.github.io/xb1de/bdat/bdat_common/ITM_itemlist.html"
    kinds = {"Collectable": 10, "Material": 11, "KeyItem": 12, "ArtBook": 13}
    with urlopen(url, timeout=30) as response:
        table = BeautifulSoup(response.read(), "html.parser").find("table")
    print("id\ttype\tname")
    for row in table.find_all("tr", recursive=False):
        cells = [cell.get_text(" ", strip=True) for cell in row.find_all("td", recursive=False)]
        if not cells or cells[2] not in kinds:
            continue
        name = cells[3]
        if not name or name.isdecimal():
            continue
        if cells[2] == "ArtBook":
            for tier in ("Intermediate", "Advanced", "Master"):
                if cells[9].startswith(tier + "."):
                    name += f" ({tier})"
                    break
        assert "\t" not in name and "\n" not in name
        print(f"{int(cells[0])}\t{kinds[cells[2]]}\t{name}")


if __name__ == "__main__":
    main()
