# 장수 초상 계획 (Fooocus Image Prompt)

정의된 ~100명 장수의 얼굴을, 지금 공용 아이콘 하나(`icon_officer`) 대신 **장수별 초상**으로
교체하는 계획. **현재 아이콘 세트의 테마(삼국지·금테·주홍·다크·회화 relief)를 유지**한다.

- 상태: **계획 단계**(미착수). 콘텐츠 단계에서 진행.
- 관련: [asset-icon-generation.md](asset-icon-generation.md)(공통 파이프라인·프레임 합성)

---

## 1. 원칙

- **정확도는 목표 아님** — SDXL은 무명 장수 실제 얼굴을 모른다(→ 아이콘 문서 참고). 목표는
  **서로 구분되는 + 화풍 통일된** 초상.
- **테마 유지가 최우선** — 모든 초상이 같은 재질·조명·팔레트·구도를 공유해야 한다.
- **프레임은 기존과 동일** — `frame_icon.py`의 금테 원형 프레임을 그대로 씌워 세트와 통일.

---

## 2. 화풍 고정 = Image Prompt(스타일 앵커)

Fooocus **Input Image → Image Prompt**에 **스타일 앵커 이미지 1장**을 넣어 화풍을 고정한다.

- **앵커**: 채택된 장수 흉상 `icon_officer`(seed 4364) 원본. 이 재질·조명·톤을 기준으로 삼는다.
- Image Prompt **Stop At ≈ 0.5**, **Weight ≈ 0.4~0.6**(너무 높이면 앵커 얼굴에 끌려가 소재가 흐려짐).
- 이렇게 하면 프롬프트로 얼굴 속성만 바꿔도 **재질/톤이 앵커와 일치**한다.

---

## 3. 프롬프트 템플릿 (오브젝트 전용 — 프레임은 후처리)

```
a portrait bust of an ancient Chinese Three Kingdoms {AGE} general, {BEARD}, {EXPRESSION}, wearing {ARMOR} lacquer armor and helmet, 3/4 view, painterly matte relief, soft top-left light, plain dark background, clean, centered with clear margin
```
네거티브(아이콘 문서 §3 오브젝트 전용과 동일 + 얼굴용):
```
gold ring, circular frame, border, medallion, deformed face, extra faces, text, letters, watermark, photograph, modern
```

### 속성 매핑(장수 데이터 → 외형)
| 데이터 | 슬롯 | 값 예 |
|---|---|---|
| 나이(추정) | `{AGE}` | 노장 `old`(흰수염·주름) / `middle-aged` / `young` |
| 무력·병과 | `{BEARD}` `{EXPRESSION}` | 맹장 `thick beard, fierce stern look` / 지장 `neat short beard, calm wise look, scholar cap` / 청년 `clean-shaven, resolute` |
| 세력 | `{ARMOR}` | 위 `dark blue`, 촉 `crimson red`, 오 `deep green`, 그 외 `brown` |

> **seed만 바꿔도** 같은 속성에서 얼굴이 달라져 → 유니크 확보. 속성×seed 조합으로 다양성.

---

## 4. 후처리 & 배선 (계획)

- **후처리**: 뽑은 초상(오브젝트, 다크 배경) → `frame_icon.py`로 금테 프레임 합성(scale ~0.9),
  256px, `SanguoSLG.Game/assets/portraits/general_{id}.png`.
- **배선(예정)**: `GeneralId → res://assets/portraits/general_{id}.png` 로더 추가.
  - 파일 있으면 장수 초상, 없으면 기존 `icon_officer` 폴백(현 `SymFiles`/`EmblemFiles` 패턴과 동일).
  - 적용 위치: 명령 모달 **장수 카드**, 정보 카드 **주둔** 행, 컨펌창.
  - `BuildOfficerCards`가 장수별 초상을 쓰도록 `GeneralId`로 로드하게 수정.

---

## 5. 규모 전략 (100명)

- **수작업 100장은 과중** → 두 방법 중 택:
  1. **아키타입 풀(권장 시작)**: 나이×무력/지력×세력 조합으로 **15~25종 원형 초상**을 만들어
     장수에 배분. 전략게임 통례, 어떤 도구로도 감당 가능.
  2. **풀 100 유니크**: **Fooocus-API(REST) 자동화**로 속성표→배치 생성(무료·톤 고정). 그때
     스크립트 별도 작성.
- 유명 장수 소수만 공들이고 나머지는 아키타입으로 하이브리드도 가능.

---

## 6. 진행 체크

| 단계 | 상태 |
|---|---|
| 스타일 앵커(Image Prompt) 확정 | ⬜ |
| 속성 템플릿 시험(5~10장) | ⬜ |
| per-general 로더 배선(+폴백) | ⬜ |
| 아키타입 풀 or 자동화 결정 | ⬜ |
| 초상 채우기 | ⬜ |
