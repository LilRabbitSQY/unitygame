#!/usr/bin/env python3
"""Import the checked-in Feishu enemy sheet into the runtime catalogue.

The sheet owns names, descriptions, base numbers, timings and geometry. Stable
IDs below connect each authored unit to its separately reviewed skill logic.
Enemy levels are not authored; elite and boss multipliers belong to spawn groups.
"""
import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Docs/Battle/Source/05-enemies.json"
OUTPUT = ROOT / "Assets/Resources/Battle/Enemies.json"
SKILLS = (
    ("direct_crit", "卷王驾到"), ("direct_speed", "开源学霸"),
    ("burn_stack", "历年真题"), ("burn_burst", "通宵王者"),
    ("poison_spread", "千手观音"), ("poison_amp", "蕉绿贩卖机"),
    ("weaken_def", "菜菜捞捞"), ("weaken_slow", "美式加茶"),
    ("control_retreat", "退课申请"), ("control_frozen", "季老心态"),
    ("counter_shield", "摸摸咸鱼"), ("counter_store", "热带风味"),
    ("pursuit_strike", "高考战魂"), ("pursuit_support", "赛博外援"),
    ("heal_aura", "宝藏网课"), ("heal_support", "红榜讲师"),
)


def number(text):
    match = re.match(r"\s*(\d+(?:\.\d+)?)", text)
    if not match:
        raise ValueError(f"Expected a number: {text!r}")
    return float(match.group(1))


def catalogue(source=SOURCE):
    document = json.loads(source.read_text(encoding="utf-8"))
    rows = {row["row"]: {c["column"]: c["text"] for c in row["cells"]}
            for row in document["rows"]}
    units = []
    for skill_id, name in SKILLS:
        starts = [row for row, cells in rows.items() if cells.get(3) == name]
        if len(starts) != 1:
            raise ValueError(f"Expected one source block for {name}, found {starts}")
        start = starts[0]
        headings = [(row, cells[3]) for row, cells in rows.items()
                    if row < start and re.match(r"2\.\d+ .*体系$", cells.get(3, ""))]
        category = max(headings)[1].split(" ", 1)[1].removeprefix("dot-").removesuffix("体系")
        basic, skill = {}, {}
        for row in range(start + 2, start + 13):
            cells = rows.get(row, {})
            if 3 in cells and 4 in cells:
                basic[cells[3]] = cells[4].strip()
            if 8 in cells and 9 in cells:
                skill[cells[8]] = cells[9].strip()
        attack_range = basic["攻击范围"]
        radial = "周围" in attack_range
        dimensions = re.search(r"(\d+)\*(\d+)", attack_range)
        width, depth = map(int, dimensions.groups()) if dimensions else (1, 1)
        no_attack = basic["攻击方式"] == "常态不攻击"
        blocked = "阻挡" in basic["攻击触发"]
        units.append({
            "id": skill_id, "name": name,
            "description": rows[start + 1][3], "tag": category,
            "highGround": False, "healing": False, "healAll": False,
            "projectile": basic["攻击弹道"] == "有", "radial": radial,
            "attackWhenBlocked": blocked,
            "targets": 0 if no_attack else (2 if "打二" in basic["攻击方式"] else 1),
            "block": 1,
            "attackFrames": 0 if no_attack else int(number(basic["攻击速度"])),
            "rangeWidth": width, "rangeDepth": depth,
            "deployCost": 0, "upgradeCost": [],
            "hp": [int(number(basic["生命值"]))],
            "attack": [int(number(basic["攻击力"]))],
            "defense": [int(number(basic["防御力"]))],
            "moveSpeed": number(basic["移动速度"]), "projectileSpeed": 8,
            "goalDamage": int(number(basic["攻入保护点削减点数"])),
            "costReward": 1, "goldReward": 1,
            "skill": {"id": skill_id, "name": skill["技能名"],
                      "description": skill["技能描述"], "initialSp": 0,
                      "spCost": 0, "interval": number(skill["施放间隔"]) / 30,
                      "duration": 3 if skill_id == "counter_store" else 0},
        })
    return {"units": units}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="fail if the catalogue is stale")
    args = parser.parse_args()
    content = json.dumps(catalogue(), ensure_ascii=False, indent=2) + "\n"
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text(encoding="utf-8") != content:
            raise SystemExit("Enemies.json differs from the authored source; rerun importer")
        print("Enemy catalogue matches the source: 16 units / 16 skills")
    else:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_text(content, encoding="utf-8")
        print(f"Imported 16 enemies into {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
