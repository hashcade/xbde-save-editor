#!/usr/bin/env python3
"""Print the five skill-link shapes for every recipient and source pairing."""

from urllib.request import urlopen

from bs4 import BeautifulSoup


def main():
    url = "https://xenoblade.github.io/xb1de/bdat/bdat_menu_psv/MNU_PSset.html"
    with urlopen(url, timeout=30) as response:
        table = BeautifulSoup(response.read(), "html.parser").find("table")
    shapes = {0: "None", 1: "Circle", 2: "Square", 3: "Hexagon", 4: "Octagram", 5: "Diamond"}
    print("CharacterId\tSourceCharacterId\tSlot1\tSlot2\tSlot3\tSlot4\tSlot5")
    for row in table.find_all("tr", recursive=False):
        cells = row.find_all("td", recursive=False)
        if not cells:
            continue
        values = [cell.get_text(" ", strip=True) for cell in cells]
        row_id = int(values[0])
        fields = [str((row_id - 1) // 8 + 1), str((row_id - 1) % 8 + 1)]
        fields.extend(shapes[int(value)] for value in values[2:7])
        print("\t".join(fields))


if __name__ == "__main__":
    main()
