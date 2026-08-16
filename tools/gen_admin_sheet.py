# -*- coding: utf-8 -*-
"""data/balance.json + command-balance.json을 읽어 내정 밸런스 시트(doc/admin-sheet.html)를 만든다."""
import json, io, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
b = json.load(io.open(os.path.join(DATA, "balance.json"), encoding="utf-8"))
c = json.load(io.open(os.path.join(DATA, "command-balance.json"), encoding="utf-8"))

sizes = [("소성", "small"), ("중성", "medium"), ("대성", "large")]
gold = {k: b[f"gold_base_{k}"] for _, k in sizes}
prov = {k: b[f"provisions_base_{k}"] for _, k in sizes}
popmax = {k: b[f"population_max_{k}"] for _, k in sizes}
slots = {k: c[f"build_slots_{k}"] for _, k in sizes}

def paddy_max(k): return prov[k] + slots[k] * b["paddy_provisions"]
def village_max(k): return gold[k] + slots[k] * b["village_gold"]

rows_castle = [
    ("인구 최대", {k: f"{popmax[k]:,}" for _, k in sizes}),
    ("시설 슬롯(논·밭·마을)", {k: str(slots[k]) for _, k in sizes}),
    ("기본 금 / 월", {k: f"{gold[k]:,}" for _, k in sizes}),
    ("기본 군량 / 월", {k: f"{prov[k]:,}" for _, k in sizes}),
    ("슬롯 전부 논 → 군량 / 월", {k: f"{paddy_max(k):,}" for _, k in sizes}),
    ("슬롯 전부 마을 → 금 / 월", {k: f"{village_max(k):,}" for _, k in sizes}),
]

facilities = [
    ("논 (Paddy)", b["gold_base_small"] and c["build_cost_paddy"], f"군량 +{b['paddy_provisions']}", "4개월"),
    ("밭 (Farm)", c["build_cost_farm"], f"군량 +{b['farm_provisions']}", "5.3개월"),
    ("마을 (Village)", c["build_cost_village"], f"금 +{b['village_gold']}", "8개월"),
    ("공방 (Workshop)", c["build_cost_workshop"], "공성 병기·연구·성벽수리 게이트 (수입 X)", "—"),
]

recruit = [
    ("보병", "광석 1만", "광석 산출 2,500/월 + 시장(1금/개)", "~3개월"),
    ("기병", "+말 3,333", "말 산출 500/월 · 시장 6금/필", "~5–6개월"),
    ("상병", "+코끼리 10", "코끼리 산출 10/월 · 시장 3,000금/두", "~1–2개월"),
]

HTML = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>내정 밸런스 시트</title>
<style>
:root {
  --bg:#F4F3EE; --panel:#FFFFFF; --ink:#242A24; --muted:#6B726A; --line:#DEDCD2;
  --accent:#4E6E4A; --accent-soft:#4E6E4A18; --gold:#B08828; --grain:#A0522D; --bar:#E7E5DC;
}
@media (prefers-color-scheme: dark){ :root{
  --bg:#15181A; --panel:#1E2224; --ink:#E7E9E4; --muted:#9BA29A; --line:#31363A;
  --accent:#7FB877; --accent-soft:#7FB87722; --gold:#D8B45A; --grain:#C98B62; --bar:#2B3033; } }
:root[data-theme="dark"]{ --bg:#15181A; --panel:#1E2224; --ink:#E7E9E4; --muted:#9BA29A; --line:#31363A;
  --accent:#7FB877; --accent-soft:#7FB87722; --gold:#D8B45A; --grain:#C98B62; --bar:#2B3033; }
:root[data-theme="light"]{ --bg:#F4F3EE; --panel:#FFFFFF; --ink:#242A24; --muted:#6B726A; --line:#DEDCD2;
  --accent:#4E6E4A; --accent-soft:#4E6E4A18; --gold:#B08828; --grain:#A0522D; --bar:#E7E5DC; }
*{box-sizing:border-box;}
body{background:var(--bg);color:var(--ink);margin:0;
  font-family:"Apple SD Gothic Neo","Malgun Gothic","맑은 고딕",sans-serif;line-height:1.45;}
.wrap{max-width:1000px;margin:0 auto;padding:32px 22px 64px;}
h1{font-family:"Source Han Serif K","Noto Serif CJK KR",Batang,serif;font-size:26px;margin:0 0 2px;letter-spacing:.02em;}
h1::before{content:"政";color:var(--accent);border:1.5px solid var(--accent);border-radius:5px;
  padding:1px 7px;margin-right:11px;font-size:19px;vertical-align:3px;}
.sub{color:var(--muted);font-size:13px;margin-bottom:26px;}
h2{font-size:14px;letter-spacing:.08em;color:var(--accent);margin:30px 0 10px;text-transform:none;
  border-bottom:2px solid var(--accent-soft);padding-bottom:5px;}
table{border-collapse:collapse;width:100%;background:var(--panel);border:1px solid var(--line);
  border-radius:10px;overflow:hidden;font-size:14px;}
th,td{padding:9px 13px;border-bottom:1px solid var(--line);text-align:left;}
thead th{background:var(--accent-soft);color:var(--accent);font-size:12px;letter-spacing:.04em;white-space:nowrap;}
tbody tr:last-child td{border-bottom:0;}
td.num,th.num{text-align:right;font-variant-numeric:tabular-nums;}
td.name{font-weight:700;white-space:nowrap;}
.tag{color:var(--muted);font-size:12px;}
.formula{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:14px 16px;
  font-size:13.5px;line-height:1.7;}
.formula b{color:var(--accent);}
.formula .k{color:var(--gold);font-weight:700;font-variant-numeric:tabular-nums;}
.note{color:var(--muted);font-size:12px;margin-top:8px;}
.gov{border-left:3px solid var(--accent);}
</style></head><body>
<div class="wrap">
<h1>내정 밸런스 시트</h1>
<div class="sub">data/balance.json · command-balance.json 기준 자동 생성 · 세율 20% · 인구 만충 100% · 유효 담당관(정치≥60) 기준</div>

<h2>성곽 등급별 월 수입</h2>
<table><thead><tr><th>항목</th><th class="num">소성</th><th class="num">중성</th><th class="num">대성</th></tr></thead>
<tbody>__CASTLE__</tbody></table>

<h2>시설 (등급 무관 · 회수는 금 환산)</h2>
<table><thead><tr><th>시설</th><th class="num">건설비</th><th>월 효과</th><th class="num">회수</th></tr></thead>
<tbody>__FAC__</tbody></table>

<h2>수입 배율 — 최종 수입 = 기본치 × 아래를 순서대로</h2>
<div class="formula gov">
<div>⓪ <b>담당관(태수)</b>: 없거나 정치&lt;<span class="k">__MINPOL__</span> → <b>수입 ×<span class="k">__NOGOV__</span></b> (급감).
 유효(정치≥<span class="k">__MINPOL__</span>) → 정치가 <b>세율을 증폭</b>: 정치 100 → 세율 효과 <span class="k">×2</span>
 (10% 세율이 20%처럼, 치안은 실세율 기준이라 안 깎임). + 내정 스킬(상재→금·둔전→군량·진무→치안).</div>
<div>① <b>세율</b>: 세율/20% → 10%=<span class="k">×0.5</span> · 20%=<span class="k">×1.0</span> · 50%=<span class="k">×2.5</span>(단 치안 −__TAXPEN__/월)</div>
<div>② <b>인구 충원율</b>: 바닥 <span class="k">__FLOOR__%</span> ~ 만충 <span class="k">100%</span> (인구/최대치 비례)</div>
<div>③ <b>저치안</b>: 치안 &lt;<span class="k">__SECLOW__</span> → 수입 ×<span class="k">__SECPEN__</span></div>
</div>
<div class="note">치안: 매월 자연 회복 +__SECREC__ + 세율 효과(20%기준 0, 낮으면 +, 50%면 −__TAXPEN__) + 진무 스킬. 인구 성장 = 매월 +__POPGROW__% × 치안/100.</div>

<h2>병사 모집 (rescale 후 · 대성 기준)</h2>
<table><thead><tr><th>병종</th><th>1만당 자원</th><th>조달</th><th class="num">재건</th></tr></thead>
<tbody>__RECRUIT__</tbody></table>
<div class="note">모병: 정치 비례 산출(정치×__RTP__/명령), 인구 __RPOP__% 캡, 훈련도 50 · 징병: 인구 __CPOP__% 캡, 훈련도 0, 치안 하락 · 명령 7일(건설 30일)·수행 장수 잠김·주관+보좌×__ASSIST__%+고향 +__HOME__%.</div>
</div></body></html>
"""

def castle_rows():
    out = []
    for label, vals in rows_castle:
        cells = "".join(f'<td class="num">{vals[k]}</td>' for _, k in sizes)
        out.append(f'<tr><td class="name">{label}</td>{cells}</tr>')
    return "\n".join(out)

def fac_rows():
    return "\n".join(
        f'<tr><td class="name">{n}</td><td class="num">{cost:,}금</td><td>{eff}</td><td class="num tag">{pay}</td></tr>'
        for n, cost, eff, pay in facilities)

def recruit_rows():
    return "\n".join(
        f'<tr><td class="name">{n}</td><td>{res}</td><td class="tag">{src}</td><td class="num">{t}</td></tr>'
        for n, res, src, t in recruit)

html = (HTML
    .replace("__CASTLE__", castle_rows())
    .replace("__FAC__", fac_rows())
    .replace("__RECRUIT__", recruit_rows())
    .replace("__MINPOL__", str(b["governor_min_politics"]))
    .replace("__NOGOV__", f'{b["no_governor_income_percent"]/100:.1f}')
    .replace("__FLOOR__", str(b["population_income_floor_percent"]))
    .replace("__SECLOW__", str(b["security_low_threshold"]))
    .replace("__SECPEN__", f'{b["security_low_income_percent"]/100:.1f}')
    .replace("__SECREC__", str(b["security_natural_recovery"]))
    .replace("__TAXPEN__", str(b["tax_max_security_penalty"]))
    .replace("__POPGROW__", str(b["population_growth_percent"]))
    .replace("__RTP__", str(c["recruit_troops_per_politics"]))
    .replace("__RPOP__", str(c["recruit_pop_cap_percent"]))
    .replace("__CPOP__", str(c["conscript_pop_cap_percent"]))
    .replace("__ASSIST__", str(c["assist_coefficient_percent"]))
    .replace("__HOME__", str(c["home_region_bonus_percent"])))

out = os.path.join(ROOT, "doc", "admin-sheet.html")
io.open(out, "w", encoding="utf-8", newline="\n").write(html)
print("written", out, len(html), "bytes")
