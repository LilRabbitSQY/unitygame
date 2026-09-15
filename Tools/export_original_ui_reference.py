#!/usr/bin/env python3
"""Record the committed UI reference without checking out or running that revision."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ref", default="HEAD")
    parser.add_argument("--output", type=Path, default=ROOT / "Docs/OriginalUIVerification/reference/head-ui-manifest.json")
    args = parser.parse_args()
    commit = git("rev-parse", args.ref).decode().strip()
    files = git("ls-tree", "-r", "--name-only", commit, "Assets/_Game/Scenes", "Assets/_Game/Scripts/UI").decode().splitlines()
    selected = [path for path in files if path.endswith(".unity") or path.endswith(".cs")]
    records = {}
    pattern = re.compile(r"m_Name:|SetCenteredRect|SetTopRect|EnsurePageBackground|EnsureBackdrop|new GameObject|public (?:Color|float|int) |(?:anchorMin|anchorMax|anchoredPosition|sizeDelta) =")
    for path in selected:
        raw = git("show", f"{commit}:{path}")
        lines = raw.decode().splitlines()
        records[path] = {
            "sha256": hashlib.sha256(raw).hexdigest(),
            "referenceLines": [{"line": index, "text": line.strip()} for index, line in enumerate(lines, 1) if pattern.search(line)],
        }
    result = {
        "commit": commit,
        "scope": "Committed scene names and UI layout/theme declarations; this is a static source reference, not a rendered screenshot or a gameplay verification.",
        "files": records,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(f"Recorded {len(records)} committed scene/UI sources from {commit} in {args.output}")


if __name__ == "__main__":
    main()
