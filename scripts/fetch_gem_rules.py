#!/usr/bin/env python3
"""Print gem effect/rank limits from the extracted DE skill table as JSON."""

import json
from urllib.request import urlopen

from bs4 import BeautifulSoup


def main():
    url = "https://xenoblade.github.io/xb1de/bdat/bdat_common/BTL_skilllist.html"
    with urlopen(url, timeout=30) as response:
        table = BeautifulSoup(response.read(), "html.parser").find("table")
    rows = table.find_all("tr", recursive=False)
    headers = [cell.get_text(" ", strip=True) for cell in table.find("tr").find_all(["th", "td"], recursive=False)]
    result = []
    for row in rows:
        cells = row.find_all("td", recursive=False)
        if not cells:
            continue
        fields = dict(zip(headers, [cell.get_text(" ", strip=True) for cell in cells]))
        if int(fields["atr_type"]) == 0:
            continue
        for rank, suffix in enumerate("EDCBAS", start=1):
            low, high, chance = (int(fields[key + suffix]) for key in ("lower_", "upper_", "percent_"))
            if high == 0 and chance == 0:
                continue
            assert 0 <= low <= high <= 255 and 0 <= chance <= 100
            result.append([int(fields["ID"]), rank, low, high, chance,
                           int(fields["atr_type"]), int(fields["rvs_type"]), int(fields["attach"]), fields["name"]])
    print(json.dumps(result))


if __name__ == "__main__":
    main()
