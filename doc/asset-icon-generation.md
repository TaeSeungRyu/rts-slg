# 아이콘 에셋 생성 가이드 (Fooocus)

명령 팔레트·정보 카드·모달 카드 아이콘을 Fooocus(로컬 SDXL)로 만들어, 코드 생성
아이콘(`CampaignMapScene`)을 실제 이미지로 교체하는 작업 문서.

- **상태(2026-08-19)**: 스타일·워크플로우 확정. **병종 6종 + 명령/정보 3종(검·코인·책)
  배선 완료.** 남은 것 = 정보 아이콘 8종·계략 6종(§4 표).
- **방식**: Fooocus 웹 UI 수동 생성(스톡 Gradio API 자동화는 불가 → 부록).
- **핵심 워크플로우**: **오브젝트만 뽑고 → 금테 프레임은 `frame_icon.py`로 합성**(§3).
  프롬프트로 테두리까지 그리게 하는 방식은 실패가 잦아 폐기(강한 소재 한정 참고: §6).

---

## 1. Fooocus 설정 (확정 · 이 표가 원본)

새로고침/재시작하면 UI 값(프롬프트·Styles·seed)이 **전부 초기화**된다. 세션마다 이 표와
§3 프롬프트를 다시 입력한다.

| 항목 | 값 |
|---|---|
| Performance | **Speed** (전 아이콘 Speed로 완료) |
| Styles | **Fooocus V2 + SAI Fantasy Art** |
| Aspect Ratios | **1024×1024 (1:1)** |
| Image Number | 2 |
| Steps / Guidance / Sharpness | 30 / 4 / 2 |
| Base Model | juggernautXL_v8Rundiffusion.safetensors |
| Sampler / Scheduler | dpmpp_2m_sde_gpu / karras |
| CLIP Skip | 2 |
| Output Format | png |

> **설정 복구**: `outputs/<날짜>/log.html`에 모든 생성의 전체 파라미터가 남는다.
> Advanced → **`Save Metadata to Images` 체크**를 켜두면 이후 PNG에 설정이 박혀,
> 그 PNG를 Fooocus에 다시 넣어 재적용할 수 있다.

---

## 2. 작업 흐름 요약

1. §3 **오브젝트 전용 프롬프트**로 소재만 생성(어두운 배경 + 여백, 테두리 없이).
2. 잘 안 나오면 §5 **트러블슈팅**.
3. 뽑은 PNG를 담당에게 전달 → `frame_icon.py`로 **금테 프레임 합성 + 배선 + 빌드 검증**.
4. 적용할 때마다 §4 표의 **완료 열 ✅** + §7 **배선 기록** 갱신.

---

## 3. 표준 프롬프트 — 오브젝트 생성 → 프레임 합성 ⭐

SDXL은 "테두리+오브젝트"를 한 문장으로 잘 못 그린다(공성=마차, 링=실물 액자 등). 그래서
**오브젝트만 생성**하고, 금테 프레임은 후처리로 합성한다.

### Positive (`{SUBJECT}`만 교체)
```
a game icon of a single {SUBJECT}, centered with clear margin, ancient Chinese Three Kingdoms style, gold and vermilion accents, matte painted relief, soft top-left light, clean readable silhouette, flat plain dark background
```

### Negative
```
gold ring, circular frame, border, medallion, ornate frame, heraldic crest, coat of arms, rosette, chinese characters, kanji, hanzi, calligraphy, text, letters, symbols, glyphs, cropped subject, parchment background, multiple objects, cluttered, blurry, low quality, photograph, realistic human face, watermark
```
> `gold ring, circular frame, border, medallion, ornate frame`로 **프레임을 아예 안 그리게** 한다.
> **여러 개 소재(돌무더기·낟알)는 `multiple objects`를 뺀다**(소재와 충돌).

### 프레임 합성 — `scratchpad/frame_icon.py`
```
python frame_icon.py <src> <dst> <scale>
```
- **그라데이션 금테(좌상 밝음→우하 어두움) + 라디얼 흑칠 안쪽 + 경계 음영선**을 그리고,
  안쪽 원에 오브젝트를 페더(가우시안 블러) 마스크로 합성 → 256px. (홍옥/빨강점 없음.)
- 오브젝트 배경을 `flat plain dark background`로 두면 흑칠 안쪽과 자연스럽게 섞인다.
- **여백**: 오브젝트가 원을 침범하면 `scale`을 줄인다(코끼리 0.84, 넓은 배 0.94 등).
- 세트의 금테·흑칠 톤이 100% 일치해 통일감이 가장 좋다.

---

## 4. 아이콘별 SUBJECT + 완료

### 명령·정보 아이콘 (팔레트/정보 카드)

| 완료 | 파일명 | 용도 | `{SUBJECT}` |
|:---:|---|---|---|
| ✅ | `icon_sword` | 모병/전투 | `a Chinese jian sword blade pointing up` |
| ✅ | `icon_coin` | 자금 | `an ancient Chinese gold ingot yuanbao` |
| ✅ | `icon_book` | 연구 | `an open ancient book with pages` |
| ✅ | `icon_wall` | 성벽 수리 | seed 2927(성벽·누각·아치문 장면) · `icon` 빼고 서술형으로 인식 · 합성 0.98 · `Icon(Sym.Wall)` 배선 |
| ✅ | `icon_scroll` | 계략 | seed 1534(펼친 두루마리·홍인) · 합성 0.98 · `Icon(Sym.Scroll)` 배선 |
| ✅ | `icon_grain` | 군량 | seed 8987(붉은 군량 자루) · 합성 0.9 · `Icon(Sym.Grain)` 배선 (정보 카드 금/군량 분리) |
| ⬜ | `icon_flag` | 세력/성 | `a hanging war banner flag` — **현재 UI 미사용**(표시 위치 없음). 쓰려면 자리 먼저 정해야 함(예: 정보 카드 성 이름 앞, HUD 세력 표시) |
| ✅ | `icon_people` | 인구 | seed 4138(두 인물, 자체 금테 포함) · 원형 크롭만 · `Icon(Sym.People)` 배선 |
| ✅ | `icon_shield` | 치안 | seed 9701(원형 방패) · 합성 0.84 · `Icon(Sym.Shield)` 배선 |
| ✅ | `icon_ore` | 광물 | seed 2563(주황 결정+금맥 광석) · 합성 0.9 · `Icon(Sym.Ore)` 배선 |
| ✅ | `icon_officer` | 장수 | seed 4364(장수 흉상) · 합성 0.9 · `Icon(Sym.Officer)` 배선 (주둔 행·모든 장수 카드·이간 계략에 표시) |

### 병종 엠블럼 (모달 카드) — 6종 전부 완료

| 완료 | 파일명 | 병종 | `{SUBJECT}` |
|:---:|---|---|---|
| ✅ | `troop_infantry` | 보병 | `a crossed sword and spear` |
| ✅ | `troop_archer` | 궁병 | `a drawn recurve bow with arrow` |
| ✅ | `troop_cavalry` | 기병 | `a rearing war horse` |
| ✅ | `troop_elephant` | 상병 | `an armored war elephant` |
| ✅ | `troop_siege` | 공성 | 긴 정의 문장(충차) — §5 참고 |
| ✅ | `troop_naval` | 해상 | `an ancient Chinese war junk ship` |

### 계략 아이콘 (모달) — 미착수

| 완료 | 파일명 | 계략 | `{SUBJECT}` |
|:---:|---|---|---|
| ⬜ | `strat_scout` | 정찰 | `a perched hunting hawk with sharp eyes` (구 `bronze spyglass and eye`는 너무 고급스러움. 대안: `a trail of footprints tracks` / `a single stylized watching eye` / `a simple wooden folding spyglass`) |
| ⬜ | `strat_wallbreak` | 성벽파괴 | `a crumbling breached castle wall` |
| ⬜ | `strat_incite` | 선동 | `a raised fist with flames` |
| ⬜ | `strat_arson` | 방화 | `a burning torch with fire` |
| ⬜ | `strat_steal` | 절취 | `a bag of gold coins` |
| ⬜ | `strat_discord` | 이간 | `two opposing masks split apart` |

---

## 5. 트러블슈팅 (소재가 안 나올 때)

- **여러 개 소재**(돌무더기·낟알 등): 네거티브에서 `multiple objects` 제외.
- **중앙에 글자/도장이 생김**(금괴·코인 등 추상 소재): 네거티브 앞에 `korean text,
  japanese text, inscription, engraved text,` 추가. 소재를 `({SUBJECT}:1.4)`처럼 가중치로 강조.
- **마차·배로 뭉개짐**: 네거티브에 `carriage, wagon, chariot, cart, palanquin, boat, ship,
  pagoda roof` 추가. **핵심 부위를 문장의 주어로** 서술(예: 충차는 "cart"가 아니라
  "a long horizontal wooden log tipped with a bronze ram head").
- **기계가 장식 구조물로 뭉개짐**(투석기 등): `tripod stand, candelabra, chandelier,
  decorative structure` 추가.
- **짧은 단어로 계속 실패 → 정의 문장 통째로**: 공성은 `catapult`/`ram`/`boulders`가 전부
  실패했으나, "A battering ram is a wheeled siege engine carrying a long heavy wooden beam …
  to smash through gates …" 같은 **기능·형태 서술 긴 문장**으로 인식됨(seed 1494).
- **그래도 안 되면 단순 상징으로 대체**하거나(실루엣이 단순할수록 아이콘으로 잘 읽힘),
  **Kenney CC0 3D 모델을 Blender 렌더**로 대체한다(`D:\dev\assets\kenney`에 배(`unit-ship`)·
  성·탑 등 있음).
- 배경·인물이 섞여 나와도 §3 프레임 합성의 **원형 크롭**으로 중앙만 살린다.
- **건축/텍스처 소재**(성벽 등)는 `game icon` 단어를 **빼야** 한다(넣으면 벽 위에 빈 장식
  액자를 그림). "wall"은 배경 텍스처로 화면을 꽉 채우므로 `a lone section … standing
  isolated as a single structure`처럼 **독립 구조물**로 서술하고, 네거티브에 `brick wall
  filling the whole image, seamless wall texture, tiled bricks background` 추가.

---

## 6. (참고) v4 — Fooocus가 프레임까지 한 번에 (강한 소재 전용)

검·말처럼 **형태가 강한 소재**는 Fooocus가 테두리까지 예쁘게 뽑을 때가 있다. 이때만 참고로
쓰고, 결과가 좋으면 §3 합성 없이 원형 크롭만 한다(기병 7318·코인 8428·검 1090이 이 방식).

**Positive:**
```
a game UI icon of a single {SUBJECT} in the center, ancient Chinese Three Kingdoms style, framed by an ornate circular gold rim with small red gems and fine engraving, dark lacquer background inside, vermilion accents, clean flat emblem, centered with clear margin, strong readable silhouette, matte painted relief, soft top-left light
```
**Negative:**
```
3d object on a table, physical ring, mirror stand, tripod stand, standing frame, diorama, heraldic crest, coat of arms, rosette, chinese characters, kanji, hanzi, calligraphy, text, letters, symbols, glyphs, object overflowing the rim, cropped subject, parchment background, multiple objects, cluttered, blurry, low quality, photograph, realistic human face, watermark
```
> 소재를 문장의 **주어**로 둔다. "ornate ring frame … and inside the ring …"처럼 테두리를
> 별도 명사로 강조하면 **받침대 위 실물 링(액자)**으로 그려진다(폐기된 v3, seed 6615).

---

## 7. 배선 기록 (seed · 방식)

> **규칙**: 이미지를 적용(배선)할 때마다 §4 표 완료 열 ✅ + 아래 기록 추가.

| 파일 | seed | 방식 |
|---|---|---|
| troop_cavalry | 7318 | v4(프레임 포함) → 원형 크롭. `ClassEmblem(Cavalry)` |
| troop_infantry | 1984 | v4(교차 검) → 원형 크롭. `ClassEmblem(Infantry)` |
| icon_coin | 8428 | v4(금괴) → 원형 크롭. `Icon(Sym.Coin)` |
| icon_sword | 1090 | v4(검) → 원형 크롭. `Icon(Sym.Sword)` (8399 비원형 스킵) |
| icon_book | 9016 | 오브젝트 → 프레임 합성. `Icon(Sym.Book)` |
| icon_wall | 2927 | 서술형(성벽 장면) → 합성 0.98. `Icon(Sym.Wall)` |
| icon_scroll | 1534 | 오브젝트(펼친 두루마리) → 합성 0.98. `Icon(Sym.Scroll)` |
| icon_grain | 8987 | 오브젝트(군량 자루) → 합성 0.9. `Icon(Sym.Grain)` (정보 카드 금/군량 분리) |
| icon_people | 4138 | 자체 금테 포함 → 원형 크롭만(프레임 합성 X). `Icon(Sym.People)` |
| icon_shield | 9701 | 원형 방패 → 합성 0.84. `Icon(Sym.Shield)` |
| icon_ore | 2563 | 오브젝트(광석) → 합성 0.9. `Icon(Sym.Ore)` |
| icon_officer | 4364 | 오브젝트(장수 흉상) → 합성 0.9. `Icon(Sym.Officer)` |
| troop_archer | 6675 | 오브젝트(홍금 활) → 합성 0.92. `ClassEmblem(Archer)` |
| troop_elephant | 4343 | 오브젝트(장식 코끼리) → 합성 0.84. `ClassEmblem(Elephant)` |
| troop_siege | 1494 | 정의 문장(충차 장면) → 합성 0.92. `ClassEmblem(Siege)` |
| troop_naval | 7746 | 오브젝트(홍금 범선) → 합성 0.94. `ClassEmblem(Naval)` |

**배선 코드**: `CampaignMapScene`의 `SymFiles`(심볼) / `EmblemFiles`(병종) 사전에
`res://assets/icons/*.png` 한 줄 추가. 파일이 있으면 `Image.LoadFromFile(GlobalizePath)`로
로드, 없으면 절차적 아이콘 폴백. PNG는 `SanguoSLG.Game/assets/icons/`에 둔다.

---

## 부록: 왜 수동인가 — 스톡 Fooocus API 불가 검증

스톡 Fooocus 2.5.0 Gradio API로 외부 스크립트 생성 구동은 불가함을 실측 확인:

- 생성은 버튼 클릭 시 서버 내부 **3단계 체인**(`dep 65` 클릭 → `dep 67` 파라미터를 세션
  state에 패킹 → `dep 68` 실제 생성).
- `dep 67`이 만든 **state가 외부 클라이언트로 전달되지 않음**(Gradio 3.x는 서버 세션에만
  보관). 실측: 67은 빈 `()` 반환, 68은 state 없이 구동 불가.
- 자동화가 필요하면 **Fooocus-API**(mrhan1993/Fooocus-API, REST 포크)로 기존 모델 재사용.

(생성 함수 `fn_index=68`, 파라미터 패킹 `fn_index=67`, 입력 153개 중 `[0]`은 숨김 state.)
