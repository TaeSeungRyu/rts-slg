# 아이콘 에셋 생성 가이드 (Fooocus)

명령 팔레트·정보 카드·모달 카드의 아이콘을 Fooocus로 생성하기 위한 작업 문서.
코드 생성 아이콘(`CampaignMapScene.Icon()`)을 이미지 텍스처로 교체하는 것이 목표.

- **상태**: 진행 중 — 톤 확정 시안 선별 중 (2026-08-19)
- **방식**: Fooocus **웹 UI 수동 생성**(B안). 스톡 Fooocus 2.5.0 Gradio API로는
  생성 구동이 불가함(→ 부록). 확정 후 자동화가 필요하면 Fooocus-API(REST 포크) 검토.

---

## 1. 확정 설정 (톤 결정되면 채운다)

| 항목 | 값 |
|---|---|
| 채택 시안 | _(미정)_ |
| Performance | _(미정: Speed / Quality)_ |
| Styles | _(미정: Fooocus V2 + ?)_ |
| Seed | _(미정 — 확정 시 고정)_ |
| 비고 | |

> 좋은 이미지가 나오면 위를 채우고, 그 설정·시드로 나머지 아이콘을 일괄 생성해 톤을 통일한다.

---

## 2. Fooocus UI 설정

- **Performance**: `Speed`(초안) → 확정 시 `Quality`
- **Aspect Ratios**: `1024×1024 ∣ 1:1` (정사각)
- **Image Number**: `4` (한 번에 4장 → 골라 쓰기)
- **Output Format**: `png`
- **Advanced → Styles**: `Fooocus V2`만 켜고 시작 → 더 회화적이면 `SAI Fantasy Art`
  또는 `Ornate And Intricate` 추가 실험
- 마음에 드는 결과의 **Seed 고정** → 나머지도 같은 시드로 뽑아 톤 통일

---

## 3. 네거티브 프롬프트 (모든 아이콘 공통)

```
text, letters, numbers, watermark, signature, multiple objects, cluttered background, blurry, low quality, jpeg artifacts, modern objects, photograph, realistic human face, cropped, extra frames, ui buttons
```

---

## 4. 포지티브 템플릿 (`{SUBJECT}`만 교체)

```
a circular game icon medallion of {SUBJECT}, ancient Chinese Three Kingdoms era theme, ornate engraved gold rim border, dark lacquer black-brown center, vermilion red and warm parchment accents, embossed bronze relief, painterly matte finish, soft top-left studio lighting, subtle inner shadow, single centered object, clean silhouette, highly detailed, crisp edges, flat solid dark charcoal background around the medallion, no text
```

원형 금테 배지 형태라 배경 제거 없이 원형 크롭만으로 UI에 바로 쓸 수 있다.

---

## 5. 아이콘별 SUBJECT

### 명령·정보 아이콘 (팔레트 세트)

| 파일명 | 용도 | `{SUBJECT}` |
|---|---|---|
| `icon_sword` | 모병/전투 | `a Chinese jian sword blade pointing up` |
| `icon_coin` | 자금 | `an ancient Chinese gold ingot yuanbao` |
| `icon_book` | 연구 | `a bamboo strip scroll book` |
| `icon_wall` | 성벽 수리 | `a stone castle battlement wall` |
| `icon_scroll` | 계략 | `a rolled paper scroll with a red seal` |
| `icon_grain` | 군량 | `a sack of rice grain` |
| `icon_flag` | 세력/성 | `a hanging war banner flag` |
| `icon_people` | 인구 | `two stylized peasant figures` |
| `icon_shield` | 치안 | `a round bronze war shield` |
| `icon_ore` | 광물 | `a chunk of raw silver ore crystal` |
| `icon_officer` | 장수 | `a helmeted general bust silhouette` |

### 병종 엠블럼 (모달 카드)

| 파일명 | 병종 | `{SUBJECT}` |
|---|---|---|
| `troop_infantry` | 보병 | `a crossed sword and spear` |
| `troop_archer` | 궁병 | `a drawn recurve bow with arrow` |
| `troop_cavalry` | 기병 | `a rearing war horse` |
| `troop_elephant` | 상병 | `an armored war elephant` |
| `troop_siege` | 공성 | `a wooden trebuchet catapult` |
| `troop_naval` | 해상 | `an ancient Chinese war junk ship` |

### 계략 아이콘 (모달, 톤 확정 후)

| 파일명 | 계략 | `{SUBJECT}` |
|---|---|---|
| `strat_scout` | 정찰 | `a bronze spyglass and eye` |
| `strat_wallbreak` | 성벽파괴 | `a crumbling breached castle wall` |
| `strat_incite` | 선동 | `a raised fist with flames` |
| `strat_arson` | 방화 | `a burning torch with fire` |
| `strat_steal` | 절취 | `a bag of gold coins` |
| `strat_discord` | 이간 | `two opposing masks split apart` |

---

## 6. 완성형 예시 (바로 붙여넣기)

```
a circular game icon medallion of an ancient Chinese gold ingot yuanbao, ancient Chinese Three Kingdoms era theme, ornate engraved gold rim border, dark lacquer black-brown center, vermilion red and warm parchment accents, embossed bronze relief, painterly matte finish, soft top-left studio lighting, subtle inner shadow, single centered object, clean silhouette, highly detailed, crisp edges, flat solid dark charcoal background around the medallion, no text
```

```
a circular game icon medallion of a rearing war horse, ancient Chinese Three Kingdoms era theme, ornate engraved gold rim border, dark lacquer black-brown center, vermilion red and warm parchment accents, embossed bronze relief, painterly matte finish, soft top-left studio lighting, subtle inner shadow, single centered object, clean silhouette, highly detailed, crisp edges, flat solid dark charcoal background around the medallion, no text
```

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
| icon_coin | 시안 진행 중 | 완성형 예시 ①로 테스트 중 |
| troop_cavalry | 대기 | 완성형 예시 ② |
| (나머지) | 대기 | |

> 상태 값: 대기 / 시안 진행 중 / 확정 / 배선 완료

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
