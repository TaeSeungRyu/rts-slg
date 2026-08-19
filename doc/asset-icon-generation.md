# 아이콘 에셋 생성 가이드 (Fooocus)

명령 팔레트·정보 카드·모달 카드의 아이콘을 Fooocus로 생성하기 위한 작업 문서.
코드 생성 아이콘(`CampaignMapScene.Icon()`)을 이미지 텍스처로 교체하는 것이 목표.

- **상태**: 진행 중 — 톤 확정 시안 선별 중 (2026-08-19)
- **방식**: Fooocus **웹 UI 수동 생성**(B안). 스톡 Fooocus 2.5.0 Gradio API로는
  생성 구동이 불가함(→ 부록). 확정 후 자동화가 필요하면 Fooocus-API(REST 포크) 검토.

---

## 1. 확정 설정 (기병 7318 채택 기준 — 2026-08-19)

아래 값으로 세트 전체를 뽑는다. **이 값이 원본** — UI가 초기화되면 여기서 그대로 재입력한다.

| 항목 | 값 |
|---|---|
| 채택 시안 | 기병 seed **7318** (v2 프롬프트) |
| Performance | **Speed** |
| Styles | **Fooocus V2 + SAI Fantasy Art** |
| Resolution | **1024×1024 (1:1)** |
| Steps | 30 (Speed 기본) |
| Guidance Scale | 4 |
| Sharpness | 2 |
| Base Model | juggernautXL_v8Rundiffusion.safetensors |
| Sampler / Scheduler | dpmpp_2m_sde_gpu / karras |
| CLIP Skip | 2 |
| Seed | 소재별로 다름(랜덤 허용). 채택된 것만 기록 |
| Output Format | png |

> ⚠ **설정 보존 주의**: Fooocus **웹 UI를 새로고침하거나 앱을 재시작하면 위 값들이
> 전부 초기화**된다(프롬프트·Styles·seed 포함). 대응:
> 1. **이 문서가 원본** — 초기화되면 위 표·아래 프롬프트를 그대로 다시 입력.
> 2. **복구**: 생성 기록은 `outputs/<날짜>/log.html`에 **모든 생성의 전체 파라미터**가
>    남는다(프롬프트·네거티브·Styles·seed·샘플러 등). 잃어버리면 여기서 확인.
> 3. **권장**: Advanced → **`Save Metadata to Images` 체크** → 이후 PNG에 파라미터가
>    박혀서, 그 PNG를 Fooocus에 다시 넣으면 설정 재적용 가능(현재는 꺼져 있어 PNG엔 없음).

---

## 2. Fooocus UI 설정

- **Performance**: `Speed`(초안) → 확정 시 `Quality`
- **Aspect Ratios**: `1024×1024 ∣ 1:1` (정사각)
- **Image Number**: `4` (한 번에 4장 → 골라 쓰기)
- **Output Format**: `png`
- **Advanced → Styles**: `Fooocus V2` + `SAI Fantasy Art` (확정)
- **Advanced → `Save Metadata to Images` 체크 권장** (설정 복구용 — §1 주의 참고)
- 새로고침/재시작 시 초기화되므로, 세션 시작 때마다 §1·§3·§4를 재입력

---

## 표준 방식 (2026-08-19 확정): 오브젝트만 생성 → 프레임 합성

SDXL이 "테두리+오브젝트"를 한 문장으로 이해하는 능력이 약해(공성=마차, 링=실물 액자
등 실패 반복), **오브젝트만 뽑고 금테 프레임은 후처리로 합성**하는 §4-c를 기본으로 한다.
프레임은 단색이 아니라 **그라데이션 금테 + 홍옥 4개 + 라디얼 흑칠**로 Fooocus 느낌을 낸다
(`scratchpad/frame_icon.py`, `<src> <dst> <scale>`; 큰 오브젝트일수록 scale↓로 여백 확보).

### 오브젝트 전용 Positive (`{SUBJECT}`만 교체)
```
a game icon of a single {SUBJECT}, centered with clear margin, ancient Chinese Three Kingdoms style, gold and vermilion accents, matte painted relief, soft top-left light, clean readable silhouette, flat plain dark background
```

### 오브젝트 전용 Negative
```
gold ring, circular frame, border, medallion, ornate frame, heraldic crest, coat of arms, rosette, chinese characters, kanji, hanzi, calligraphy, text, letters, symbols, glyphs, cropped subject, parchment background, multiple objects, cluttered, blurry, low quality, photograph, realistic human face, watermark
```

> 프레임을 아예 안 그리게 `gold ring, circular frame, border, medallion, ornate frame`를
> 네거티브에 둔다. **여러 개 소재(돌무더기·낟알)는 `multiple objects` 제외.**

---

## 3. (구) 네거티브 — Fooocus가 프레임까지 그리게 하던 v4 (강한 소재 전용)

```
3d object on a table, physical ring, mirror stand, tripod stand, standing frame, diorama, heraldic crest, coat of arms, rosette, chinese characters, kanji, hanzi, calligraphy, text, letters, symbols, glyphs, object overflowing the rim, cropped subject, parchment background, multiple objects, cluttered, blurry, low quality, photograph, realistic human face, watermark
```

> 추상 소재(금괴·코인)에서 글자가 계속 새면 앞에 `korean text, japanese text, inscription,
> engraved text,`를 추가.
>
> 기계류(공성 등)가 장식 구조물로 뭉개지면 `tripod stand, candelabra, chandelier,
> bell frame, decorative structure, abstract ornament`를 추가하고, 소재를 더 또렷한
> 실루엣 단어로 바꾼다. **핵심 부위를 문장의 주어로** 서술할 것 — 예: 충차는 "cart"가
> 아니라 "a long horizontal wooden log tipped with a bronze ram head"를 앞세운다.
> "cart/wheeled cart"는 화려한 마차로, "ship"류는 배로 뭉개지므로 네거티브에
> `carriage, wagon, chariot, cart, palanquin, sedan chair, boat, ship, pagoda roof` 추가.
>
> **원칙**: 사실적 기계(투석기·충차 등)가 계속 실패하면 억지로 밀지 말고, 그 병종을
> **단순 상징**으로 대체한다(공성 = 투석용 돌무더기, 실루엣이 단순할수록 아이콘으로 잘 읽힘).
>
> **여러 개로 구성된 소재**(돌무더기·낟알 등)는 §3 네거티브에서 **`multiple objects`를 빼야
> 한다**(소재와 충돌). 단일 오브젝트 소재에서는 그대로 둔다.
>
> **짧은 단어로 안 되면 "정의 문장"을 통째로** 넣는다. 예: 공성은 `catapult`/`ram` 단어로는
> 계속 실패했으나, "A battering ram is a wheeled siege engine carrying a long heavy wooden
> beam ... to smash through gates ..." 처럼 **기능·형태를 서술하는 긴 문장**을 넣자 인식됨
> (seed 1494). 배경·인물이 섞여도 §4-c 원형 크롭으로 중앙만 살린다.

---

## 4. (구) 포지티브 v4 — Fooocus가 프레임까지 그리게 (강한 소재 전용, 참고용)

> 표준은 위 "오브젝트만 생성 → 합성"이다. 아래 v4는 검·말처럼 형태가 강한 소재에서
> Fooocus가 테두리까지 예쁘게 뽑을 때만 참고로 쓴다(약한/기계 소재는 실패가 잦음).

`{SUBJECT}`만 교체:

```
a game UI icon of a single {SUBJECT} in the center, ancient Chinese Three Kingdoms style, framed by an ornate circular gold rim with small red gems and fine engraving, dark lacquer background inside, vermilion accents, clean flat emblem, centered with clear margin, strong readable silhouette, matte painted relief, soft top-left light
```

- **왜 v4인가**: 잘 나온 것들(기병 7318·코인 8428·검 1090)은 모두 **소재를 문장의 주어**로
  두고 테두리를 수식으로 붙인 v2 구조였다. v3처럼 "ornate ring frame ... and inside the
  ring ..."로 테두리를 별도 명사로 강조하면 SDXL이 **받침대 위 실물 링(액자)**으로
  그려버린다(seed 6615). v4는 v2 구조를 유지하되 **테두리를 "ornate ... with red gems,
  fine engraving"으로 더 화려하게** 수식하고, **`clean flat emblem`으로 평면**을 강조,
  **`centered with clear margin`으로 여백**을 준다.
- 네거티브에 `3d object on a table, physical ring, mirror stand, tripod, diorama`를 넣어
  v3의 실물-링 사고를 차단한다.
- 잘 나오면 **원형 크롭만** 하면 된다. 그래도 테두리가 안 잡히거나 오브젝트만 나오는
  까다로운 소재는 §4-b(오브젝트만) + §4-c(내 프레임 합성)로 폴백.

### 4-b. 어려운(추상) 소재용 — 소재 우선 + 가중치 + 문장 차단

검·말처럼 형태가 강한 소재는 §4로 잘 나오지만, **책·군량·광석처럼 약한 소재는 §4의
"emblem badge/gold rim/ornament" 문구에 밀려 문장(crest·동심원·로제트)으로 그려진다**
(icon_book seed 1103·9199·7455·4416 전부 책이 안 나옴). 이때는 소재를 **앞에 가중치로**
강조하고, 프레임 문구를 약화하고, 문장류를 네거티브로 막는다.

**Positive (`{SUBJECT}` 가중치 강조):**
```
({SUBJECT}:1.5), big and clearly centered, a simple thin round gold ring frame around it, dark lacquer background, vermilion accents, Three Kingdoms strategy game icon, matte painted relief, soft top-left light, clean readable silhouette, flat dark background
```

**Negative (문장·장식 차단 추가):**
```
heraldic crest, coat of arms, medallion, rosette, mandala, concentric rings, laurel wreath, fleur-de-lis, ornament emblem, seal, chinese characters, kanji, hanzi, calligraphy, text, letters, symbols, glyphs, full frame subject, diagonal composition, parchment background, multiple objects, cluttered, blurry, low quality, photograph, watermark
```

예) 책: `(an open ancient book with visible pages:1.5), big and clearly centered, a simple thin
round gold ring frame around it, dark lacquer background, vermilion accents, Three Kingdoms
strategy game icon, matte painted relief, soft top-left light, clean readable silhouette, flat
dark background`

> 그래도 안 나오는 소재는 **절차적(코드) 아이콘을 유지**한다(현재도 파일 없으면 자동 폴백).
> AI 아트는 잘 나오는 소재(검·말·코끼리·배 등)에 우선 적용.

### 4-c. 오브젝트만 생성 → 금테 프레임 합성 (가장 안정적) ⭐

책처럼 프레임이 계속 깨지는 소재는, **오브젝트만** 뽑고(§4-b 소재 가중치로 배경 어둡게)
**금테 원형 프레임은 후처리로 합성**한다. 오브젝트 생성(쉬움)과 프레임(내가 완벽히 통일)
을 분리하는 방식 — icon_book(seed 9016)이 이 방식으로 배선됨.

- 후처리(`scratchpad/frame_icon.py`): **그라데이션 금테(좌상 밝음→우하 어두움) + 홍옥 4개
  (상하좌우, 금 베젤·하이라이트) + 라디얼 흑칠 안쪽 + 경계 음영선**을 그리고, 안쪽 원에
  오브젝트를 페더 마스크로 합성 → 256px. 단색이 아니라 Fooocus 프레임 느낌을 낸다.
  (구버전은 단색 금링이었음.)
- 오브젝트 프롬프트는 배경을 `flat dark background`로 두면 흑칠 안쪽과 자연스럽게 섞인다.
- 세트의 금테·주홍·흑칠 톤이 100% 일치하므로 통일감이 가장 좋다.
- **여백/중앙정렬**: 오브젝트가 원을 침범하면 합성 시 안쪽 스케일을 줄인다(예: 코끼리는
  `0.84`, 가로로 넓거나 큰 오브젝트일수록 더 축소). 가장자리는 가우시안 블러로 흑칠에
  부드럽게 블렌드해 사각 이음선을 숨긴다.
- 직접 원형으로 뽑을 때(§4/§4-b)는 프롬프트에 `small and fully inside the circle with
  clear margin, centered`를 넣으면 오브젝트가 원 밖으로 삐져나오는 걸 줄일 수 있다.

---

## 5. 아이콘별 SUBJECT

### 명령·정보 아이콘 (팔레트 세트)

| 완료 | 파일명 | 용도 | `{SUBJECT}` |
|:---:|---|---|---|
| ✅ | `icon_sword` | 모병/전투 | `a Chinese jian sword blade pointing up` |
| ✅ | `icon_coin` | 자금 | `an ancient Chinese gold ingot yuanbao` |
| ✅ | `icon_book` | 연구 | seed 9016 오브젝트만 생성 → **금테 프레임 합성** 배선 (죽간·배지 프롬프트는 실패, §4-c 합성 방식) |
| ⬜ | `icon_wall` | 성벽 수리 | `a stone castle battlement wall` |
| ⬜ | `icon_scroll` | 계략 | `a rolled paper scroll with a red seal` |
| ⬜ | `icon_grain` | 군량 | `a sack of rice grain` |
| ⬜ | `icon_flag` | 세력/성 | `a hanging war banner flag` |
| ⬜ | `icon_people` | 인구 | `two stylized peasant figures` |
| ⬜ | `icon_shield` | 치안 | `a round bronze war shield` |
| ⬜ | `icon_ore` | 광물 | `a chunk of raw silver ore crystal` |
| ⬜ | `icon_officer` | 장수 | `a helmeted general bust silhouette` |

### 병종 엠블럼 (모달 카드)

| 완료 | 파일명 | 병종 | `{SUBJECT}` |
|:---:|---|---|---|
| ✅ | `troop_infantry` | 보병 | seed 1984(교차 검) 채택 · 원형 크롭 · `ClassEmblem(Infantry)` 배선 |
| ✅ | `troop_archer` | 궁병 | seed 6675(홍금 활) 오브젝트만 → §4-c 금테 합성 · `ClassEmblem(Archer)` 배선 |
| ✅ | `troop_cavalry` | 기병 | `a rearing war horse` |
| ✅ | `troop_elephant` | 상병 | seed 4343(장식 코끼리) 오브젝트만 → §4-c 금테 합성(0.84 축소+여백) · `ClassEmblem(Elephant)` 배선 |
| ✅ | `troop_siege` | 공성 | seed 1494 채택 · **아주 자세한 설명 프롬프트**(충차 정의 문장 전체)로 겨우 인식됨 · §4-c 합성(0.92, 배경·병사 크롭) · `ClassEmblem(Siege)` 배선. 짧은 단어(catapult/ram/boulders)는 전부 실패 |
| ⬜ | `troop_naval` | 해상 | `an ancient Chinese war junk ship` |

### 계략 아이콘 (모달, 톤 확정 후)

| 완료 | 파일명 | 계략 | `{SUBJECT}` |
|:---:|---|---|---|
| ⬜ | `strat_scout` | 정찰 | `a bronze spyglass and eye` |
| ⬜ | `strat_wallbreak` | 성벽파괴 | `a crumbling breached castle wall` |
| ⬜ | `strat_incite` | 선동 | `a raised fist with flames` |
| ⬜ | `strat_arson` | 방화 | `a burning torch with fire` |
| ⬜ | `strat_steal` | 절취 | `a bag of gold coins` |
| ⬜ | `strat_discord` | 이간 | `two opposing masks split apart` |

---

## 6. 완성형 예시 (오브젝트 전용 — 바로 붙여넣기, 프레임은 합성)

공성(= 투석용 돌무더기 상징 · **`multiple objects` 네거티브에서 제외**):
```
a game icon of a single pile of grey catapult boulders stacked in a pyramid, centered with clear margin, ancient Chinese Three Kingdoms style, gold and vermilion accents, matte painted relief, soft top-left light, clean readable silhouette, flat plain dark background
```

해상(전선):
```
a game icon of a single ancient Chinese war junk ship, centered with clear margin, ancient Chinese Three Kingdoms style, gold and vermilion accents, matte painted relief, soft top-left light, clean readable silhouette, flat plain dark background
```

> 오브젝트만 뽑으면 되고, 금테 프레임은 `frame_icon.py`로 합성한다(§4-c). 프롬프트에
> 프레임을 넣지 않는다.
> (기채택: 기병·보병·검·코인은 Fooocus가 프레임까지 그린 v2, 책·궁병·상병은 오브젝트+합성.
> v3/v4처럼 프레임을 프롬프트로 강제하던 방식은 실패가 잦아 폐기.)

---

## 7. 저장 & 파일명

- 위 파일명(`icon_coin.png` 등)으로 저장 → **`SanguoSLG.Game/assets/icons/`** 에 배치.
- 전부 안 뽑아도 됨 — 먼저 2~3개만 넣고 알려주면 톤 보정·배선 진행.

---

## 8. 후처리 (배선 담당 작업)

경로를 받으면:

1. **원형 크롭 + 투명(알파) 처리**
2. 삼국지 팔레트(금테·주홍)에 맞춘 **톤 보정**(필요 시)
3. 크기 정규화 후 **`CampaignMapScene.Icon()` 을 텍스처 로드로 교체**
4. `dotnet build` + 헤드리스 스모크런 **검증**

---

## 9. 진행 현황

| 파일명 | 상태 | 비고 |
|---|---|---|
| troop_cavalry | **배선 완료** | seed 7318 채택 · 원형 크롭+투명 256px · `ClassEmblem(Cavalry)`에 로드 배선 |
| icon_coin | **배선 완료** | seed 8428(금괴) 채택 · 원형 크롭+투명 256px · `Icon(Sym.Coin)`에 로드 배선 |
| icon_sword | **배선 완료** | seed 1090(검) 채택 · 원형 크롭+투명 256px · `Icon(Sym.Sword)`에 로드 배선 (8399는 비원형이라 스킵) |
| icon_book | **배선 완료** | seed 9016 오브젝트만 → §4-c 금테 프레임 합성 · `Icon(Sym.Book)`에 로드 배선 |
| troop_infantry | **배선 완료** | seed 1984(교차 검) 채택 · 원형 크롭 · `ClassEmblem(Infantry)` 배선 |
| troop_archer | **배선 완료** | seed 6675(홍금 활) 오브젝트만 → §4-c 금테 합성 · `ClassEmblem(Archer)` 배선 |
| troop_elephant | **배선 완료** | seed 4343(장식 코끼리) 오브젝트만 → §4-c 금테 합성(0.84 여백) · `ClassEmblem(Elephant)` 배선 |
| troop_siege | **배선 완료** | seed 1494(충차 장면) · 상세 설명 프롬프트로 인식 · §4-c 합성(0.92) · `ClassEmblem(Siege)` 배선 |
| (나머지) | 대기 | |

> 상태 값: 대기 / 시안 진행 중 / 후보 확보 / 배선 완료

### 관찰 (v1 vs v2)
- **구체적 오브젝트**(말·코끼리·배·검 등)는 v1 프롬프트로도 글자 없이 또렷하게 나옴.
- **추상 소재**(금괴·코인)만 모델이 "글자 새긴 도장"으로 반복 → v2 네거티브(글자 차단) 필요.
- 프레임/색감(금테·흑칠·주홍)은 두 버전 모두 일관되게 우수 → 스타일은 확정급.

### 배선 방식
- 로더: `EmblemFiles` 사전(`TroopClass → res://assets/icons/troop_*.png`).
  파일이 있으면 `Image.LoadFromFile(GlobalizePath)` 로 로드, 없으면 절차적 엠블럼 폴백.
- 새 병종 이미지 추가 = PNG를 `assets/icons/`에 넣고 `EmblemFiles`에 한 줄 추가.

> **규칙**: 이미지를 생성해 GUI에 적용(배선)할 때마다 §5 작업 대상 표의 **완료 열을 ✅**로,
> §9 진행 현황 표에서 해당 항목을 **배선 완료**로 표기한다.

---

## 부록: 왜 수동(B안)인가 — 스톡 Fooocus API 검증 기록

스톡 Fooocus 2.5.0의 Gradio API로 생성을 외부 스크립트로 구동하려 했으나 불가함을 확인:

- 생성은 버튼 클릭 시 서버 내부 **3단계 체인**(`dep 65` 클릭 → `dep 67` 파라미터를
  세션 state에 패킹 → `dep 68` 실제 생성·스트리밍).
- `dep 67`이 만든 **state(생성 태스크)가 외부 클라이언트로 전달되지 않음**
  (Gradio 3.x는 state를 서버 세션에만 보관). 실측: 67은 빈 `()` 반환, 68은 state 없이
  구동 불가.
- 즉 **UI 클릭 한 번 안에서만 성립**하는 구조라 외부에서 단계를 쪼개 호출하면 태스크 유실.
- 자동화가 필요하면 **Fooocus-API**(mrhan1993/Fooocus-API, REST 포크)를 설치해
  기존 모델을 재사용하는 방식이 정석.

(생성 함수: `fn_index=68`, 파라미터 패킹: `fn_index=67`, 입력 153개 중 `[0]`은 숨김 state.)
