#!/usr/bin/env python3
"""Reproduce the 16 tower definitions from the saved Feishu sheet, without network access."""
import json
import re
import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Docs/Battle/Source/04-towers.json"
DESTINATION = ROOT / "Assets/Resources/Battle/Towers.json"
STARTS = [44, 64, 88, 108, 132, 152, 177, 197, 219, 239, 261, 281, 303, 323, 345, 365]
SKILLS = ["direct_crit", "direct_speed", "burn_stack", "burn_burst", "poison_spread", "poison_amp",
          "weaken_def", "weaken_slow", "control_retreat", "control_frozen", "counter_shield", "counter_store",
          "pursuit_strike", "pursuit_support", "heal_aura", "heal_support"]


def generate(check=False):
    rows = {row["row"]: {cell["column"]: cell["text"] for cell in row["cells"]}
            for row in json.loads(SOURCE.read_text())["rows"]}
    units = []
    for index, start in enumerate(STARTS):
        end = STARTS[index + 1] if index + 1 < len(STARTS) else 384
        fields = {rows[r].get(3): rows[r] for r in range(start, end) if r in rows}
        skill_fields = {rows[r].get(8): rows[r].get(9, "") for r in range(start, end) if r in rows}

        def text(field):
            return fields[field][4].strip()

        def integer(value):
            match = re.search(r"\d+", str(value))
            return int(match.group()) if match else 0

        def levels(field):
            return [integer(fields[field].get(column, 0)) for column in [4, 5, 6]]

        range_match = re.search(r"(\d+)(?:\*(\d+))?", text("攻击范围"))
        width = int(range_match[1]) if range_match[2] else 1
        depth = int(range_match[2] or range_match[1])
        mode = text("攻击方式")
        skill = SKILLS[index]
        unit = {
            "id": "tower_" + skill, "name": rows[start][3], "description": rows[start + 1][3],
            "tag": text("标签"), "highGround": text("部署位置") == "高台",
            "healing": "回复生命值" in mode or "施加治疗" in mode,
            "healAll": "持续为范围内" in mode, "projectile": text("攻击弹道") == "有",
            "radial": False, "attackWhenBlocked": True, "targets": 2 if "打二" in mode else 1,
            "block": integer(text("阻挡数")), "attackFrames": integer(text("攻击速度")),
            "rangeWidth": width, "rangeDepth": depth, "deployCost": integer(text("部署COST")),
            "hp": levels("生命值"), "attack": levels("攻击力"), "defense": levels("防御力"),
            "upgradeCost": levels("升级COST"), "moveSpeed": 0, "projectileSpeed": 8,
            "goalDamage": 0, "costReward": 0, "goldReward": 0,
            "skill": {"id": skill, "name": skill_fields["技能名"],
                      "description": skill_fields["技能描述"], "initialSp": integer(skill_fields["初始技力"]),
                      "spCost": integer(skill_fields["所需技力"]),
                      "duration": integer(skill_fields["持续时间"]), "interval": 0},
        }
        assert all(len(unit[key]) == 3 for key in ("hp", "attack", "defense", "upgradeCost"))
        units.append(unit)
    assert len(units) == 16 and len({u["id"] for u in units}) == 16
    if check:
        if not DESTINATION.exists() or json.loads(DESTINATION.read_text()) != {"units": units}:
            raise SystemExit("Tower catalogue differs from the saved design; run the importer to update it.")
        print(f"Tower catalogue matches the source: {len(units)} units / 16 skills")
        return
    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    DESTINATION.write_text(json.dumps({"units": units}, ensure_ascii=False, indent=2) + "\n")
    print(f"Imported {len(units)} towers from saved Feishu rows to {DESTINATION}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Compare the catalogue without writing files")
    generate(parser.parse_args().check)
