#!/usr/bin/env python3
"""Print achievement metadata from the extracted Definitive Edition tables."""

from urllib.request import urlopen

from bs4 import BeautifulSoup


def rows(path):
    url = f"https://xenoblade.github.io/xb1de/bdat/{path}.html"
    with urlopen(url, timeout=30) as response:
        table = BeautifulSoup(response.read(), "html.parser").find("table")
    result = {}
    for row in table.find_all("tr", recursive=False):
        cells = [cell.get_text(" ", strip=True) for cell in row.find_all("td", recursive=False)]
        if cells:
            result[int(cells[0])] = cells
    assert set(result) == set(range(1, 201))
    return result


def main():
    journal = rows("bdat_common/JNL_playaward")
    names = rows("bdat_common_ms/JNL_playaward_ms")
    conditions = rows("MNU_playaward")
    print("id\tname\tcategory\torder\tcondition\treward_exp\trequired\tcondition_type")
    for identifier, condition in conditions.items():
        fields = [str(identifier), names[identifier][3], condition[2], condition[3],
                  condition[5], journal[identifier][3], condition[9], condition[10]]
        assert all("\t" not in field and "\n" not in field for field in fields)
        print("\t".join(fields))


if __name__ == "__main__":
    main()
