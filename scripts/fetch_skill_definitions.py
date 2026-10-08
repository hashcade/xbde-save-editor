#!/usr/bin/env python3
"""Print skill learning costs, link shapes and coin costs from the DE table."""

from urllib.request import urlopen

from bs4 import BeautifulSoup


def main():
    url = "https://xenoblade.github.io/xb1de/bdat/bdat_common/BTL_PSVskill.html"
    with urlopen(url, timeout=30) as response:
        table = BeautifulSoup(response.read(), "html.parser").find("table")
    print("Id\tName\tRequiredSP\tShape\tAffinityCoins")
    for row in table.find_all("tr", recursive=False):
        cells = row.find_all("td", recursive=False)
        if not cells:
            continue
        values = [cell.get_text(" ", strip=True) for cell in cells]
        fields = [values[0], values[2], str(int(values[9]) * 100), values[3], values[10]]
        assert all("\t" not in field and "\n" not in field for field in fields)
        print("\t".join(fields))


if __name__ == "__main__":
    main()
