"""从解包的游戏数据（gamedata/json2、json3）生成内置的条件文字和「决斗输了会死」的关卡表（conditions.json.gz）。

游戏的条件表只挂在剧情场景上，读档后直接在养成地图打开剧情回顾时拿不到；Mod 先用这份内置的，
进过一次剧情场景后改用游戏里现读的（ConditionBook）。游戏更新后重新解包、重跑这个脚本。
用法：python tools/make-conditions.py <gamedata 目录>
"""
import gzip
import json
import os
import sys

gamedata = os.path.abspath(sys.argv[1])
sys.path.insert(0, gamedata)
sys.argv = ["x"]
from ann import CONDS, SWITCH, cond_item  # noqa: E402

# 和 Mod 里的写法一致：「名字 需>=35」，多项之间「且 / 或」
switches = {}
for name, s in SWITCH.items():
    if s.get("_useCount"):
        continue
    switches[name] = [cond_item(ci).replace(" ≥ ", " 需>=").replace(" ≤ ", " 需<=").replace(" > ", " 需>").replace(" < ", " 需<")
                      .replace(" ≠ ", " 需!=").replace(" = ", " 需=") for ci in s.get("_items", [])]
conditions = {}
for name, c in CONDS.items():
    items = [cond_item(ci) for ci in c.get("_items", [])]
    conditions[name] = " 或 ".join(items)

# 决斗输了会死的关卡：关卡名去掉 CL_ 前缀就是剧本里 ChangeScene("Combat", "...") 的那个名字
combat_deaths = {}
levels = os.path.join(gamedata, "json3", "sharedassets6__CombatLevel.json")
if os.path.exists(levels):
    for level in json.load(open(levels, encoding="utf-8")):
        dead = (level.get("_deadLibrary") or "").lstrip("@")
        if level.get("_deadEnd") and dead:
            combat_deaths[level["m_Name"][3:] if level["m_Name"].startswith("CL_") else level["m_Name"]] = dead

book = {"Version": "builtin", "Switches": switches, "Conditions": conditions, "CombatDeaths": combat_deaths}
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "LOMAssistant", "conditions.json.gz")
with gzip.open(out, "wt", encoding="utf-8") as f:
    json.dump(book, f, ensure_ascii=False)
print(f"switches {len(switches)}, conditions {len(conditions)}, combat deaths {len(combat_deaths)}")
