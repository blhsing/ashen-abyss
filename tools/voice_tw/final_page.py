"""Builds Y:\\voice_samples\\final\\index.html: one delivered line per character, for listening.
Copies the game's OGG files (the shipped files, unchanged) next to the page; numbers from report.json and audit.json.
Run with any Python 3.10+:  final_page.py"""
from __future__ import annotations

import html
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import voices  # noqa: E402

GAME_VO = r"Y:\game\assets\audio\vo"
SCRIPT = r"Y:\game\data\vo_lines.json"
OUT = r"Y:\voice_samples\final"
AUDIT = os.path.join(os.environ.get("VT_ROOT") or os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "voice_tw"),
                     "stage", "prod", "audit.json")

# the line that shows the character best (falls back to the voice's first line with audio)
PREFER = {"narrator": "intro_01", "elder": "elder_first_02", "boss_osaros": "boss_osaros_intro"}

NAMES = {
    "narrator": "旁白", "elder": "長老 艾爾溫", "smith": "鐵匠", "merchant": "行商", "occultist": "秘術師", "jeweler": "寶石匠",
    "villager_m": "鎮民（男）", "villager_f": "鎮民（女）",
}


def main():
    lines = json.load(open(SCRIPT, encoding="utf-8"))
    rep = json.load(open(os.path.join(HERE, "report.json"), encoding="utf-8"))
    by_id = {l["id"]: l for l in rep["lines"]}
    audit = json.load(open(AUDIT, encoding="utf-8")) if os.path.exists(AUDIT) else {}
    cons = rep.get("consistency", {}).get("per_voice", {})
    os.makedirs(OUT, exist_ok=True)
    order = list(dict.fromkeys(l["voice"] for l in lines))
    rows, missing = [], []
    for v in order:
        vl = [l for l in lines if l["voice"] == v]
        have = [l for l in vl if os.path.exists(os.path.join(GAME_VO, l["id"] + ".ogg"))]
        if not have:
            missing.append(v)
            continue
        pick = next((l for l in have if l["id"] == PREFER.get(v)), None) or next((l for l in have if l["id"].endswith("_intro")), None) or have[0]
        shutil.copy2(os.path.join(GAME_VO, pick["id"] + ".ogg"), os.path.join(OUT, pick["id"] + ".ogg"))
        r = by_id.get(pick["id"], {})
        a = audit.get(v, {})
        spec = voices.VOICES.get(v, {})
        lufs = [by_id[l["id"]]["lufs"] for l in have if by_id.get(l["id"], {}).get("lufs") is not None]
        name = NAMES.get(v) or pick.get("speaker") or v
        gate = "通過" if a.get("passed") else ("使用者以耳朵選定（自動檢查未過）" if v == "boss_osaros" else "未通過，請聽" if a else "—")
        rows.append(f"""<tr><td><b>{html.escape(name)}</b><br><code>{v}</code><br><small>{html.escape(spec.get('who', ''))}</small></td>
<td>{html.escape(pick['text'])}<br><small><code>{pick['id']}</code></small><br><audio controls preload="none" src="{pick['id']}.ogg"></audio></td>
<td>{len(have)} / {len(vl)}</td>
<td>{a.get('anchor_f0', '—')} / {a.get('lines_f0', '—')} Hz<br><small>目標 {spec.get('f0', ('', ''))[0]}–{spec.get('f0', ('', ''))[1]}</small></td>
<td>{a.get('anchor_p_male', '—')} / {a.get('lines_p_male', '—')}</td>
<td>{gate}</td>
<td>{r.get('final_cer', '—')}</td>
<td>{cons.get(v, {}).get('to_anchor_mean', '—')}</td>
<td>{(f"{min(lufs):.1f} … {max(lufs):.1f}") if lufs else '—'}</td></tr>""")
    page = f"""<!doctype html><html lang="zh-Hant-TW"><head><meta charset="utf-8"><title>燼淵 配音 — 每個角色一句</title>
<style>body{{font-family:"Microsoft JhengHei",sans-serif;background:#16130f;color:#e8dcc4;margin:2em;font-size:18px}}
table{{border-collapse:collapse;width:100%}}td,th{{border:1px solid #4a3f30;padding:.5em;vertical-align:top}}th{{background:#2a231a}}
code{{color:#c9a86a}}small{{color:#a99a80}}audio{{width:100%;margin-top:.4em}}</style></head><body>
<h1>《燼淵 Ashen Abyss》配音 — 每個角色一句</h1>
<p>遊戲裡實際使用的檔案（<code>game/assets/audio/vo</code>，未經修改）。VoxCPM2 聲音設計，沒有任何真人錄音或參考音檔。
欄位：音高 = 錨點 / 前三句的中位數；p(男) = 男女分類器（男聲至少 0.60，女聲至多 0.45）；字錯率 = 成品檔由語音辨識讀回的錯誤率（上限 0.15）；
相似度 = 該角色所有台詞與其錨點的平均說話者相似度；響度 = 該角色所有檔案的 LUFS 範圍。</p>
<table><tr><th>角色</th><th>台詞</th><th>已完成</th><th>音高</th><th>p(男)</th><th>聲音檢查</th><th>字錯率</th><th>相似度</th><th>響度 LUFS</th></tr>
{''.join(rows)}</table>
{('<p>尚無音檔的角色：' + '、'.join(missing) + '</p>') if missing else ''}
<p><small>由 <code>Y:\\tools\\voice_tw\\final_page.py</code> 產生；全部數字見 <code>Y:\\tools\\voice_tw\\report.json</code>，聲音檢查表見 <code>DECISION.md</code>。</small></p>
</body></html>"""
    open(os.path.join(OUT, "index.html"), "w", encoding="utf-8", newline="\n").write(page)
    print(f"{len(rows)} characters, missing: {missing} -> {OUT}\\index.html")


if __name__ == "__main__":
    main()
