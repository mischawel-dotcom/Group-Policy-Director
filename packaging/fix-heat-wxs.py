#!/usr/bin/env python3
"""Normalize wixl-heat output for msitools/wixl (unwrap Name='.' dirs, Win64=yes)."""
from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

NS = "http://schemas.microsoft.com/wix/2006/wi"
ET.register_namespace("", NS)


def fix(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
    text = text.replace('Win64="$(var.Win64)"', 'Win64="yes"')
    root = ET.fromstring(text)

    def unwrap(parent: ET.Element) -> None:
        for child in list(parent):
            tag = child.tag.split("}")[-1]
            if tag == "Directory" and child.get("Name") == ".":
                for gc in list(child):
                    parent.append(gc)
                parent.remove(child)
                for gc in list(parent):
                    # newly appended nodes need processing; recurse whole tree below
                    pass
                unwrap(parent)
                return
            unwrap(child)

    unwrap(root)
    path.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        + ET.tostring(root, encoding="unicode")
        + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(f"usage: {sys.argv[0]} FILE.wxs", file=sys.stderr)
        sys.exit(2)
    fix(Path(sys.argv[1]))
