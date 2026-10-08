#!/usr/bin/env python3
"""Print equipment initializer fields from the extracted DE data tables."""

from urllib.request import urlopen

from bs4 import BeautifulSoup

BASE = "https://xenobladedata.github.io/xb1de/bdat/bdat_common/"


def rows(name):
    with urlopen(BASE + name + ".html", timeout=30) as response:
        table = BeautifulSoup(response.read(), "html.parser").find("table")
    return [row.find_all("td", recursive=False) for row in table.find_all("tr", recursive=False)
            if row.find("td", recursive=False)]


def item_references(cell):
    return [int(link["href"].split("#")[1]) for link in cell.find_all("a")
            if link.get("href", "").startswith("ITM_itemlist.html#")]


def fixed_gem(cell):
    ids = item_references(cell)
    if ids:
        assert len(ids) == 1
        return ids[0]
    value = cell.get_text(strip=True)
    assert not value or value.isdecimal(), value
    return int(value or 0)


def main():
    definitions = []
    for table in ("ITM_wpnlist", "ITM_equiplist"):
        for cells in rows(table):
            if table == "ITM_wpnlist":
                count = int(cells[17].get_text(strip=True))
                gems = [fixed_gem(cell) for cell in cells[18:21]]
            else:
                assert len(cells) == 28
                count = int(cells[9].get_text(strip=True))
                gems = [fixed_gem(cells[10]), 0, 0]
            assert 0 <= count <= 3 and all(0 <= gem <= 65535 for gem in gems)
            for item_id in item_references(cells[1]):
                definitions.append((item_id, count, *gems))
    assert len(definitions) == len({row[0] for row in definitions}) == 1572
    print("ItemId\tGemSlots\tFixedGem1\tFixedGem2\tFixedGem3")
    for row in sorted(definitions):
        print("\t".join(map(str, row)))


if __name__ == "__main__":
    main()
