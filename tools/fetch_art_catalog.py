#!/usr/bin/env python3
"""Print supported character art definitions from the extracted DE table."""

from urllib.request import urlopen

from bs4 import BeautifulSoup


def main():
    url = "https://xenoblade.github.io/xb1de/bdat/bdat_common/pc_arts.html"
    with urlopen(url, timeout=30) as response:
        table = BeautifulSoup(response.read(), "html.parser").find("table")
    print("Id\tCharacterId\tName\tTalent\tOrder\tLearnType\tLearnLevel")
    for row in table.find_all("tr", recursive=False):
        cells = row.find_all("td", recursive=False)
        if not cells or not cells[3].find("a"):
            continue
        owner = int(cells[3].find("a")["href"].split("#")[-1])
        if owner not in {*range(1, 9), 14, 15}:
            continue
        values = [cell.get_text(" ", strip=True) for cell in cells]
        fields = [values[0], str(owner), values[2], values[10], values[42], values[43], values[44]]
        assert all("\t" not in field and "\n" not in field for field in fields)
        print("\t".join(fields))


if __name__ == "__main__":
    main()
