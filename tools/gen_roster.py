# -*- coding: utf-8 -*-
import json, io

import os
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA_DIR = os.path.join(ROOT, "data")
generals = json.load(io.open(os.path.join(DATA_DIR, "generals.json"), encoding="utf-8"))
regions = json.load(io.open(os.path.join(DATA_DIR, "regions.json"), encoding="utf-8"))
actives = json.load(io.open(os.path.join(DATA_DIR, "active-skills.json"), encoding="utf-8"))
passives = json.load(io.open(os.path.join(DATA_DIR, "passive-skills.json"), encoding="utf-8"))

active_names = {a["code"]: a["name"] for a in actives}
passive_names = {p["code"]: p["name"] for p in passives}
region_map = {r["code"]: r for r in regions}

rows = []
for g in generals:
    reg = region_map[g["region"]]
    rows.append({
        "id": g["id"], "name": g["name"], "birth": g["birth"],
        "region": reg["name"], "realm": reg["realm"], "desc": g["desc"],
        "apt": [g["aptitudes"][c] for c in ["infantry","archer","cavalry","elephant","siege","naval"]],
        "m": g["might"], "i": g["intellect"], "p": g["politics"],
        "act": active_names.get(g.get("battle_active"), ""),
        "pas": [f'{passive_names[s["code"]]} {s["tier"]}' for s in g.get("battle_passives", [])],
    })

HTML = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>장수 명감 — 152인</title>
<style>
:root {
  --bg: #F4F4F1; --panel: #FFFFFF; --ink: #23272B; --muted: #6E7378;
  --line: #DDDED8; --seal: #B8402F; --seal-soft: #B8402F1A;
  --china: #A0522D; --korea: #2E6E5E; --japan: #4E5590;
  --gF:#9DA2A6; --gD:#8B9AA4; --gC:#7792A3; --gB:#5E86A8; --gA:#3E7D5E;
  --gAP:#2E8B57; --gS:#B8860B; --gSS:#C05A1E; --gSSS:#B8402F;
  --bar: #E4E5E0;
}
@media (prefers-color-scheme: dark) { :root {
  --bg: #16191D; --panel: #1E2227; --ink: #E6E7E3; --muted: #979CA1;
  --line: #31363C; --seal: #E06A55; --seal-soft: #E06A5526;
  --china: #C98B62; --korea: #5FA893; --japan: #8B92C9;
  --gF:#7A8085; --gD:#7E8D97; --gC:#7E9DB2; --gB:#6E96BC; --gA:#5FA383;
  --gAP:#57B383; --gS:#D2A429; --gSS:#D97B3F; --gSSS:#E06A55;
  --bar: #2C3138;
} }
:root[data-theme="dark"] {
  --bg: #16191D; --panel: #1E2227; --ink: #E6E7E3; --muted: #979CA1;
  --line: #31363C; --seal: #E06A55; --seal-soft: #E06A5526;
  --china: #C98B62; --korea: #5FA893; --japan: #8B92C9;
  --gF:#7A8085; --gD:#7E8D97; --gC:#7E9DB2; --gB:#6E96BC; --gA:#5FA383;
  --gAP:#57B383; --gS:#D2A429; --gSS:#D97B3F; --gSSS:#E06A55;
  --bar: #2C3138;
}
:root[data-theme="light"] {
  --bg: #F4F4F1; --panel: #FFFFFF; --ink: #23272B; --muted: #6E7378;
  --line: #DDDED8; --seal: #B8402F; --seal-soft: #B8402F1A;
  --china: #A0522D; --korea: #2E6E5E; --japan: #4E5590;
  --gF:#9DA2A6; --gD:#8B9AA4; --gC:#7792A3; --gB:#5E86A8; --gA:#3E7D5E;
  --gAP:#2E8B57; --gS:#B8860B; --gSS:#C05A1E; --gSSS:#B8402F;
  --bar: #E4E5E0;
}
* { box-sizing: border-box; }
body { background: var(--bg); color: var(--ink); margin: 0;
  font-family: "Apple SD Gothic Neo", "Malgun Gothic", "맑은 고딕", sans-serif;
  font-size: 14px; line-height: 1.45; }
.wrap { max-width: 1240px; margin: 0 auto; padding: 28px 20px 60px; }
header { display: flex; align-items: baseline; gap: 14px; flex-wrap: wrap; margin-bottom: 6px; }
h1 { font-family: "Source Han Serif K", "Noto Serif CJK KR", Batang, serif;
  font-size: 26px; margin: 0; letter-spacing: 0.02em; }
h1::before { content: "印"; color: var(--seal); font-size: 20px; margin-right: 10px;
  border: 1.5px solid var(--seal); border-radius: 4px; padding: 1px 5px; vertical-align: 2px; }
.sub { color: var(--muted); font-size: 13px; }
.toolbar { display: flex; gap: 10px; flex-wrap: wrap; align-items: center;
  margin: 18px 0 14px; }
.tabs { display: flex; gap: 2px; background: var(--panel); border: 1px solid var(--line);
  border-radius: 8px; padding: 3px; }
.tabs button { border: 0; background: transparent; color: var(--muted); font: inherit;
  padding: 5px 14px; border-radius: 6px; cursor: pointer; }
.tabs button.on { background: var(--seal-soft); color: var(--seal); font-weight: 700; }
.tabs button:focus-visible, input:focus-visible, th:focus-visible { outline: 2px solid var(--seal); outline-offset: 1px; }
input[type=search] { background: var(--panel); border: 1px solid var(--line); color: var(--ink);
  border-radius: 8px; padding: 7px 12px; font: inherit; width: 220px; }
.count { color: var(--muted); font-size: 13px; margin-left: auto; }
.tablebox { background: var(--panel); border: 1px solid var(--line); border-radius: 10px;
  overflow-x: auto; }
table { border-collapse: collapse; width: 100%; min-width: 1080px; }
thead th { position: sticky; top: 0; background: var(--panel); z-index: 2;
  text-align: left; font-size: 11.5px; letter-spacing: 0.06em; color: var(--muted);
  padding: 10px 10px 8px; border-bottom: 2px solid var(--line); white-space: nowrap;
  cursor: pointer; user-select: none; }
thead th.num { text-align: right; }
thead th .dir { color: var(--seal); }
tbody td { padding: 9px 10px; border-bottom: 1px solid var(--line); vertical-align: top; }
tbody tr:last-child td { border-bottom: 0; }
tbody tr:hover td { background: var(--seal-soft); }
.name { font-weight: 700; font-size: 15px; white-space: nowrap; }
.desc { color: var(--muted); font-size: 12px; max-width: 300px; }
.chip { display: inline-block; font-size: 11px; font-weight: 700; border-radius: 4px;
  padding: 1px 7px; color: #fff; margin-right: 6px; vertical-align: 1px; }
.chip.china { background: var(--china); } .chip.korea { background: var(--korea); }
.chip.japan { background: var(--japan); }
.region { white-space: nowrap; }
.birth { text-align: right; font-variant-numeric: tabular-nums; color: var(--muted); white-space: nowrap; }
.grades { display: flex; gap: 3px; }
.gd { width: 30px; text-align: center; font-size: 11px; font-weight: 800; border-radius: 4px;
  padding: 2px 0; color: #fff; font-variant-numeric: tabular-nums; }
.stat { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
.statbar { display: inline-block; width: 44px; height: 5px; background: var(--bar);
  border-radius: 3px; margin-left: 6px; vertical-align: 2px; overflow: hidden; }
.statbar i { display: block; height: 100%; background: var(--seal); border-radius: 3px; }
.skills { font-size: 12px; }
.skills .a { color: var(--seal); font-weight: 700; }
.skills .p { color: var(--muted); }
.legend { display: flex; gap: 10px; flex-wrap: wrap; margin-top: 12px;
  color: var(--muted); font-size: 12px; align-items: center; }
@media (prefers-reduced-motion: no-preference) {
  tbody tr { transition: background 0.1s; }
}
</style></head><body>
<div class="wrap">
<header>
  <h1>장수 명감</h1>
  <span class="sub">중국 112 · 한국 30 · 일본 10 — data/generals.json 기준</span>
</header>
<div class="toolbar">
  <div class="tabs" id="tabs">
    <button data-realm="" class="on">전체</button>
    <button data-realm="china">중국</button>
    <button data-realm="korea">한국</button>
    <button data-realm="japan">일본</button>
  </div>
  <input type="search" id="q" placeholder="이름·지역·스킬·소개 검색" />
  <span class="count" id="count"></span>
</div>
<div class="tablebox">
<table>
  <thead><tr id="head">
    <th data-k="name">장수</th>
    <th data-k="region">출신</th>
    <th data-k="birth" class="num">출생</th>
    <th data-k="apt">보 · 궁 · 기 · 상 · 공 · 해</th>
    <th data-k="m" class="num">무력</th>
    <th data-k="i" class="num">지력</th>
    <th data-k="p" class="num">정치</th>
    <th data-k="skill">전투 스킬</th>
  </tr></thead>
  <tbody id="body"></tbody>
</table>
</div>
<div class="legend">
  <span>통솔 등급</span>
  <span class="gd" style="background:var(--gSSS)">SSS</span>
  <span class="gd" style="background:var(--gSS)">SS</span>
  <span class="gd" style="background:var(--gS)">S</span>
  <span class="gd" style="background:var(--gAP)">A+</span>
  <span class="gd" style="background:var(--gA)">A</span>
  <span class="gd" style="background:var(--gB)">B</span>
  <span class="gd" style="background:var(--gC)">C</span>
  <span class="gd" style="background:var(--gD)">D</span>
  <span class="gd" style="background:var(--gF)">F</span>
  <span style="margin-left:6px">열 머리글을 누르면 정렬 · 출생 음수 = 기원전</span>
</div>
</div>
<script>
const DATA = /*__DATA__*/;
const REALM_KO = { china: "중국", korea: "한국", japan: "일본" };
const GRADE_ORDER = { F:0, D:1, C:2, B:3, A:4, "A+":5, S:6, SS:7, SSS:8 };
const GRADE_VAR = { F:"--gF", D:"--gD", C:"--gC", B:"--gB", A:"--gA", "A+":"--gAP", S:"--gS", SS:"--gSS", SSS:"--gSSS" };
let realm = "", query = "", sortKey = "id", sortDir = 1;

function birthLabel(b) { return b < 0 ? "BC " + (-b) : String(b); }
function aptScore(g) { return g.apt.reduce((s, x) => s + GRADE_ORDER[x], 0); }

function render() {
  const q = query.toLowerCase();
  let rows = DATA.filter(g => (!realm || g.realm === realm) &&
    (!q || (g.name + g.region + g.desc + g.act + g.pas.join(" ")).toLowerCase().includes(q)));
  rows.sort((a, b) => {
    let va, vb;
    if (sortKey === "apt") { va = aptScore(a); vb = aptScore(b); }
    else if (sortKey === "skill") { va = a.act ? 1 : 0; vb = b.act ? 1 : 0; }
    else { va = a[sortKey]; vb = b[sortKey]; }
    if (typeof va === "string") return sortDir * va.localeCompare(vb, "ko");
    return sortDir * (va - vb);
  });
  document.getElementById("count").textContent = rows.length + "명";
  document.getElementById("body").innerHTML = rows.map(g => `
    <tr>
      <td><div class="name">${g.name}</div><div class="desc">${g.desc}</div></td>
      <td class="region"><span class="chip ${g.realm}">${REALM_KO[g.realm]}</span>${g.region}</td>
      <td class="birth">${birthLabel(g.birth)}</td>
      <td><div class="grades">${g.apt.map(x =>
        `<span class="gd" style="background:var(${GRADE_VAR[x]})">${x}</span>`).join("")}</div></td>
      <td class="stat">${g.m}<span class="statbar"><i style="width:${g.m}%"></i></span></td>
      <td class="stat">${g.i}<span class="statbar"><i style="width:${g.i}%"></i></span></td>
      <td class="stat">${g.p}<span class="statbar"><i style="width:${g.p}%"></i></span></td>
      <td class="skills">${g.act ? `<span class="a">${g.act}</span>` : ""}${g.act && g.pas.length ? " · " : ""}<span class="p">${g.pas.join(" · ")}</span></td>
    </tr>`).join("");
  document.querySelectorAll("#head th").forEach(th => {
    const base = th.textContent.replace(/ [▲▼]$/, "");
    th.innerHTML = th.dataset.k === sortKey
      ? base + ' <span class="dir">' + (sortDir > 0 ? "▲" : "▼") + "</span>" : base;
  });
}
document.getElementById("tabs").addEventListener("click", e => {
  if (e.target.tagName !== "BUTTON") return;
  realm = e.target.dataset.realm;
  document.querySelectorAll("#tabs button").forEach(b => b.classList.toggle("on", b === e.target));
  render();
});
document.getElementById("q").addEventListener("input", e => { query = e.target.value; render(); });
document.getElementById("head").addEventListener("click", e => {
  const th = e.target.closest("th"); if (!th) return;
  const k = th.dataset.k;
  if (sortKey === k) { sortDir = -sortDir; } else { sortKey = k; sortDir = k === "name" || k === "region" ? 1 : -1; }
  render();
});
render();
</script></body></html>
"""

html = HTML.replace("/*__DATA__*/", json.dumps(rows, ensure_ascii=False))
out = os.path.join(ROOT, "doc", "roster.html")
io.open(out, "w", encoding="utf-8", newline="\n").write(html)
print("written", len(html), "bytes,", len(rows), "generals")
