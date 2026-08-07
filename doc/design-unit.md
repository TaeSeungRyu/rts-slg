# 설계 노트 — 부대(유닛) 표현 (2026-08-04)

> 부대의 3D 표현·모션 설계. 프로토타입(기병대)으로 핵심 가능성 검증 완료.
> 게임 규칙(병종 상성·전투 공식)은 별개 영역 — 여기는 **표현 계층** 설계만 다룬다.

## 검증된 사실 (기병대 프로토타입, 2026-08-04)

- **저폴리 기마 유닛 제작 가능**: Blender 스크립트로 말+기수+창 생성 (`tools/blender/make_cavalry.py` → `cavalry.glb`)
- **이동/공격 모션 분리 가능** — 프로시저럴 방식 확정:
  - 모델을 부위별 노드로 내보냄(`u{i}_body` 부모 ← 목/머리/다리4/기수/창 자식, 다리 원점=고관절)
  - 이동(갤럽): 진행 방향 회전 + 몸통 바운스 + 대각 트롯 다리 스윙 (기수별 위상차)
  - 공격: 창 겨눔 → 전방 돌진 → 복귀 (`UnitController3D.PlayAttackMotion()` — 전투 시스템이 호출 예정)
  - 스켈레탈 리깅 불필요 → 반복 조정 빠름, 결정론 유지 용이

> ✅ **`cavalry.glb`는 폐기됐다(2026-08-06).** `make_troop_cavalry.py` → `troop-cavalry.glb`로
> 처음부터 다시 만들었다. 옛 모델이 안고 있던 문제(전부 사각형·3기 고정·피벗 버그)는
> 전부 해소됐다. 아직 참조하는 코드가 없으므로 `cavalry.glb`는 지워도 된다.
>
> **모션 완료(2026-08-06).** `UnitController3D`가 `leg_fl` 유무로 규약을 판별한다.
> 이동은 다리마다 위상·진폭을 따로 주는 `SwingPart` 구조로 보병 행군과 기병 갤럽이
> 같은 코드를 쓰고, 공격은 보병(제자리 휘두름+방패 밀기)과 기병(전방 돌격)이 갈린다.

## 계획 4 — 공격 시 대상을 바라본다 (2026-08-06 사용자 요구)

- **모든 병종 공통.** 공격 모션이 시작되기 전에 부대가 대상 쪽으로 회전한다.
  옆이나 뒤를 향한 채 휘두르는 그림을 금지한다
- 지금의 `F` 키 데모는 대상이 없어 제자리 방향으로 친다. **전투 시스템이 대상 좌표를
  넘겨주게 되면** `PlayAttackMotion(target)`으로 바꿔 회전을 먼저 트윈하고 모션에 들어간다
- 이동 중 회전(진행 방향 보간)이 이미 있으므로 같은 방식을 쓰면 된다

## 계획 5 — 발사체 (2026-08-06 사용자 요구)

- 사거리가 있는 병종(궁병 2, 투석기·공성탑·대궁병 3)은 공격 모션에서 **발사체가 날아간다**
- `ProjectileView`(표현 전용): 포물선 비행, 발사 순간 손의 화살이 사라지고
  같은 자리에서 발사체로 이어진다. 낙점은 편대원마다 좌우로 흩는다
- 비행 거리는 사거리(`range_unit`)와 일치시킨다 — 지금은 상수, 병종 데이터가 생기면 그쪽에서
- 변형: **Basic**(화살·돌 구현됨). **Fire**(불화살·불덩이)는 효과 단계([design-effect.md](./design-effect.md))에서
  `ProjectileView.Variant`에 값을 더해 구현 — 발광 재질 + 꼬리 파티클. 들고 있는 쪽(바구니 돌,
  손의 화살)은 노드 이름(`stone`·`arrow`)으로 이미 참조하고 있어 재질 교체로 맞춘다
- 명중·피해 판정은 Core의 영역이다. 발사체는 그림일 뿐 판정과 무관하다

## 계획 1 — 병력 비례 편대 (1·3·5·7·9기, 사용자 요구)

- **1기짜리 GLB 하나**만 두고, Godot에서 병력 상황에 따라 N개 인스턴스를 대형 오프셋으로 조립
- 대형 패턴: 1=단독, 3=쐐기, 5=쐐기+후열 2, 7=쐐기 2열, 9=마름모(1·2·3·2·1)
  — `TroopFormation`에 구현. 자리는 육각 안(변심거리 0.5·꼭짓점 0.5774)에 들어가게 잡았다
- 애니메이션 구조가 부위 리스트 순회 방식이라 N 확장에 코드 변경 불요
- 전투 손실 → 기수 실시간 제거(병력 비례 표현, 삼국지11 방식) 가능
- N↔병력 구간 매핑은 밸런스 영역 — 사용자가 정의

## 계획 2 — 곡선 다듬기 ("반곡선" 방침, 사용자 요구)

- 몸통·엉덩이·목만 타원체/캡슐화 + **스무스 셰이딩**(폴리 증가 없음), 갑옷·창은 각지게 유지
- 이유: ① 유닛 수십 개 배치 대비 저폴리 유지(성능) ② 성·산의 각진 로우폴리 스타일과 통일성
- 서브디비전 과용 금지

## 계획 3 — 세력별 색 (가능 확인, 2026-08-04)

- 모델에 **세력색 전용 머티리얼**(투구술·깃발·안장천 등, 현재 `red`)을 지정해 두고,
  유닛 생성 시 소유 `FactionId`에 따라 해당 머티리얼 표면만 런타임 색 교체
  (대하 깊은 물의 MaterialOverride 기법과 동일 — glTF 머티리얼 이름 기반 표면 탐색)
- 세력 색 값은 `data/factions.json`에 `"color"` 필드로 데이터화 (하드코딩 금지 원칙)
- 도시 성곽·라벨에도 같은 세력색을 쓸 수 있음(추후)

---

# 병종 카탈로그 (2026-08-06 사용자 정의)

> 추가할 병종 **후보군**(20종). 한국어 이름은 **바뀔 수 있다** — 코드 식별자를 기준으로 삼는다.
> 수치(속도·탐지·사거리)와 통행 규칙은 게임 데이터이므로 `data/troop-types.json`에 두고
> C# 코드에 박지 않는다(CLAUDE.md 규칙 3).

## 표기 규약

- **지형**: `육지` = 육지 타일만 / `대하` = 대하(깊은 물) 타일만 / `육지+산악` = 육지 + 원래 이동 불가인 산악
- **속도**: **하루에** 갈 수 있는 칸수(1·2·3). "진행" 1회 = 7일이다.
  자세한 것은 [design-movement.md](./design-movement.md)
- **탐지**: 이 범위 안에 적이 들어오면 공격모드는 원래 목표를 버리고 추격한다
- **사거리**: `유닛 / 건물 / 성` 순서. 단위는 헥사 칸 수(1 = 인접). 기본값과 다른 값은 굵게 표시.
  자세한 규칙은 아래 "공격 사거리" 절 참조
- **모델**: 1기짜리 GLB 하나. 편대(1·3·5·7·9기)는 Godot에서 인스턴스를 복제해 조립한다(위 "계획 1")
- **작업 상태** (O = 완료, X = 미착수)
  - **기본작업**: 1기짜리 GLB 제작. 부위 노드 이름이 애니메이션 규약을 따라야 한다
  - **이동모션**: 이동 중 재생되는 프로시저럴 모션(보병=행군, 기병=갤럽, 배=흔들림 등)
  - **공격모션**: `PlayAttackMotion()`이 재생하는 모션

## 기본 유닛

| # | 병종(잠정) | 식별자 | JSON 키 | 지형 | 속도(칸/일) | 탐지 | 사거리 유닛/건물/성 | 기본작업 | 이동모션 | 공격모션 | 모습 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 도검병 | `Swordsman` | `swordsman` | 육지 | 2 | 2 | 1 / 1 / 1 | O | O | O | 칼과 방패를 들고 있다 |
| 2 | 기병 | `Cavalry` | `cavalry` | 육지 | 3 | 3 | 1 / 1 / 1 | O | O | O | 말을 타고 칼을 들고 있다 |
| 3 | 궁병 | `Archer` | `archer` | 육지 | 2 | 2 | **2** / 1 / 1 | O | O | O | 활을 들고 있다 |
| 4 | 벽력거 | `ThunderCart` | `thunder_cart` | 육지 | 1 | 1 | 1 / 1 / 1 | O | O | O | **거대한 연필 모양 말뚝**을 실은 수레를 병사들이 끈다(2026-08-06 확정). 사거리가 `Catapult`와 갈린다 |
| 5 | 투석기 | `Catapult` | `catapult` | 육지 | 1 | 1 | **3** / **2** / 1 | O | O | O | 투석기를 끌고 있다. 사거리가 `ThunderCart`와 갈린다 |
| 6 | 공성탑 | `SiegeTower` | `siege_tower` | 육지 | 1 | 1 | **3** / **2** / 1 | O | O | O | 바퀴 달린 네모 상자 위에 작은 궁병이 서 있다(2026-08-06 확정) |
| 7 | 상병 | `WarElephant` | `war_elephant` | 육지 | 2 | 2 | 1 / 1 / 1 | O | O | O | 코끼리 좌우에 아주 작은 병사. 공격은 코를 치켜들었다 내리찍는 들이받기(2026-08-06 확정) |
| 8 | 소선 | `SmallBoat` | `small_boat` | 대하 | 3 | 3 | 1 / 1 / 1 | X | X | X | 중국식 작은 배 |
| 9 | 중선 | `MediumShip` | `medium_ship` | 대하 | 2 | 2 | 1 / 1 / 1 | X | X | X | 중국식 중간 배 |
| 10 | 대선 | `LargeShip` | `large_ship` | 대하 | 1 | 2 | 1 / 1 / 1 | X | X | X | 중국식 큰 배 |

## 특수 유닛

| # | 병종(잠정) | 식별자 | JSON 키 | 지형 | 속도(칸/일) | 탐지 | 사거리 유닛/건물/성 | 기본작업 | 이동모션 | 공격모션 | 모습 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 11 | 장창병 | `Pikeman` | `pikeman` | 육지 | 2 | 2 | 1 / 1 / 1 | X | X | X | 긴 창을 들고 있다 |
| 12 | 낫병 | `Scytheman` | `scytheman` | 육지 | 2 | 2 | 1 / 1 / 1 | X | X | X | 낫을 들고 있다 |
| 13 | 대방패병 | `Shieldbearer` | `shieldbearer` | 육지 | 1 | 2 | 1 / 1 / 1 | X | X | X | 큰 방패를 들고 있다 |
| 14 | 산악병 | `Axeman` | `axeman` | 육지+산악 | 2 / 산악 1 | 2 | 1 / 1 / 1 | X | X | X | 도끼와 방패를 들고 있다 |
| 15 | 창기병 | `Lancer` | `lancer` | 육지 | 3 | 3 | 1 / 1 / 1 | X | X | X | 말을 타고 큰 창을 들고 있다 |
| 16 | 궁기병 | `HorseArcher` | `horse_archer` | 육지 | 3 | 3 | **2** / 1 / 1 | X | X | X | 작은 말을 타고 활을 들고 있다 |
| 17 | 대궁병 | `GreatBow` | `great_bow` | 육지 | 1 | 2 | **3** / **2** / 1 | X | X | X | 정말 큰 활을 들고 있다 |
| 18 | 판옥선 | `Panokseon` | `panokseon` | 대하 | 2 | 2 | 1 / 1 / 1 | X | X | X | 판옥선 모양의 배 |
| 19 | 거북선 | `Turtleship` | `turtleship` | 대하 | 1 | 2 | 1 / 1 / 1 | X | X | X | 거북선 모양의 배 |
| 20 | 왜선 | `Waeseon` | `waeseon` | 대하 | 2 | 2 | 1 / 1 / 1 | X | X | X | 왜선 모양의 배 |

## 공격 사거리 (2026-08-06 사용자 정의)

사거리는 **대상 종류마다 따로** 정한다. 같은 병종이라도 유닛을 칠 때와 성을 칠 때가 다르다.
단위는 헥사 칸 수(1 = 인접 타일).

| 대상 | 기본 | 예외 |
|---|---|---|
| 유닛 | 1 | `Archer` 2 · `Catapult` 3 · `SiegeTower` 3 · `HorseArcher` 2 · `GreatBow` 3 |
| 건물 | 1 | `Catapult` 2 · `SiegeTower` 2 · `GreatBow` 2 |
| 성 | 1 | 없음 — 모든 병종이 인접해야 한다 |

기본값과 다른 병종만 모으면 아래와 같다. 표에 없는 15종은 세 대상 모두 1이다.

| # | 병종 | 유닛 | 건물 | 성 |
|---|---|---|---|---|
| 3 | `Archer` | 2 | 1 | 1 |
| 5 | `Catapult` | 3 | 2 | 1 |
| 6 | `SiegeTower` | 3 | 2 | 1 |
| 16 | `HorseArcher` | 2 | 1 | 1 |
| 17 | `GreatBow` | 3 | 2 | 1 |

- **건물**: 마을·논·밭·공방·항구 등 지물 타일([design-terrain.md](./design-terrain.md)의 건물 계열)
- **성**: 도시의 성곽. 성만 별도 대상으로 두는 이유는 사거리가 병종과 무관하게 1로 고정되기 때문
- 사거리도 게임 데이터다 — `data/troop-types.json`에 대상별 필드로 둔다

**`ThunderCart`의 사거리는 세 대상 모두 1이다(의도된 값, 2026-08-06 확인).** 공성 병기이면서도
인접해야 공격한다 — 이것이 `Catapult`(유닛 3 / 건물 2)와 갈리는 지점이다. 둘은 지형·속도가
같으므로, 사거리가 실질적인 차이가 된다.

## 통행 규칙 연결

기존 물 규칙([design-water.md](./design-water.md))과 맞물린다.

| 지형 | 육지 병종 | 배 병종 |
|---|---|---|
| 평지·마을·건물 | 가능 | 불가 |
| 소하천 | 가능 | 불가 |
| 늪 | 가능 | 불가 |
| 대하 | 불가 | 가능 |
| 산악 | 불가 (`Axeman`만 가능) | 불가 |

## 유닛 데이터 (`data/troop-types.json`)

병종 수치는 전부 여기에 둔다. C# 코드에 박지 않는다(CLAUDE.md 규칙 3).
JSON 키는 snake_case, 코드에서는 `TroopType`으로 로딩한다.

### 필드

| 필드 | 형 | 뜻 |
|---|---|---|
| `id` | string | 병종 식별자(snake_case) |
| `name_ko` | string | 화면 표기용 한국어 이름. **바뀔 수 있다** — 로직은 `id`만 본다 |
| `category` | string | `basic` / `special` |
| `terrain` | string | `land` / `deep_water` / `land_mountain` |
| `movement_per_day` | int | 하루에 갈 수 있는 칸수(1·2·3). 정수라 누적 계산이 없다 |
| `mountain_movement_per_day` | int? | 산악에서의 속도. `land_mountain`에만 있다 |
| `detection` | int | 탐지 범위. 이 안에 적이 들어오면 공격모드는 추격한다([design-movement.md](./design-movement.md)) |
| `range_unit` | int | 유닛 대상 사거리 |
| `range_building` | int | 건물 대상 사거리 |
| `range_castle` | int | 성 대상 사거리 |
| `model` | string | 1기짜리 GLB 파일명 |

### 전체 값 (20종)

속도·탐지는 [design-movement.md](./design-movement.md)에서 확정된 값이다.

| # | `id` | 분류 | 지형 | 속도 | 산악 | 탐지 | 유닛 | 건물 | 성 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `swordsman` | basic | land | 2 | — | 2 | 1 | 1 | 1 |
| 2 | `cavalry` | basic | land | 3 | — | 3 | 1 | 1 | 1 |
| 3 | `archer` | basic | land | 2 | — | 2 | **2** | 1 | 1 |
| 4 | `thunder_cart` | basic | land | 1 | — | 1 | 1 | 1 | 1 |
| 5 | `catapult` | basic | land | 1 | — | 1 | **3** | **2** | 1 |
| 6 | `siege_tower` | basic | land | 1 | — | 1 | **3** | **2** | 1 |
| 7 | `war_elephant` | basic | land | 2 | — | 2 | 1 | 1 | 1 |
| 8 | `small_boat` | basic | deep_water | 3 | — | 3 | 1 | 1 | 1 |
| 9 | `medium_ship` | basic | deep_water | 2 | — | 2 | 1 | 1 | 1 |
| 10 | `large_ship` | basic | deep_water | 1 | — | 2 | 1 | 1 | 1 |
| 11 | `pikeman` | special | land | 2 | — | 2 | 1 | 1 | 1 |
| 12 | `scytheman` | special | land | 2 | — | 2 | 1 | 1 | 1 |
| 13 | `shieldbearer` | special | land | 1 | — | 2 | 1 | 1 | 1 |
| 14 | `axeman` | special | land_mountain | 2 | **1** | 2 | 1 | 1 | 1 |
| 15 | `lancer` | special | land | 3 | — | 3 | 1 | 1 | 1 |
| 16 | `horse_archer` | special | land | 3 | — | 3 | **2** | 1 | 1 |
| 17 | `great_bow` | special | land | 1 | — | 2 | **3** | **2** | 1 |
| 18 | `panokseon` | special | deep_water | 2 | — | 2 | 1 | 1 | 1 |
| 19 | `turtleship` | special | deep_water | 1 | — | 2 | 1 | 1 | 1 |
| 20 | `waeseon` | special | deep_water | 2 | — | 2 | 1 | 1 | 1 |

### 형태 예시

```json
{
  "troop_types": [
    {
      "id": "archer",
      "name_ko": "궁병",
      "category": "basic",
      "terrain": "land",
      "movement_per_day": 2,
      "detection": 2,
      "range_unit": 2,
      "range_building": 1,
      "range_castle": 1,
      "model": "troop-archer.glb"
    },
    {
      "id": "axeman",
      "name_ko": "산악병",
      "category": "special",
      "terrain": "land_mountain",
      "movement_per_day": 2,
      "mountain_movement_per_day": 1,
      "detection": 2,
      "range_unit": 1,
      "range_building": 1,
      "range_castle": 1,
      "model": "troop-axeman.glb"
    }
  ]
}
```

> 아직 파일을 만들지 않았다. 구현 시 `ScenarioLoader`와 같은 방식으로 Core에서 읽고,
> 알 수 없는 `terrain`·`category` 값은 예외를 던진다(기존 `ParseCondition`과 같은 규약).

## 모델 제작 메모

19종을 전부 새로 만들 필요는 없다. 몸통을 공유하고 장비만 바꾸면 되는 묶음이 있다.

| 묶음 | 병종 | 공유 |
|---|---|---|
| 보병 | `Swordsman` `Pikeman` `Scytheman` `Shieldbearer` `Axeman` `Archer` `GreatBow` | 몸통·다리, 무기만 교체 |
| 기병 | `Cavalry` `Lancer` `HorseArcher` | 기존 `make_cavalry.py`의 말+기수 재사용, 무기·말 크기만 조정 |
| 공성 | `ThunderCart` `Catapult` `SiegeTower` | 끄는 병사 + 바퀴 대차 공유, 상부 구조만 교체 |
| 배 | `SmallBoat` `MediumShip` `LargeShip` `Panokseon` `Turtleship` `Waeseon` | 선체 기본형 공유, 갑판 구조·크기 차등 |
| 단독 | `WarElephant` | 공유 없음 |

---

## 확정된 해석 (2026-08-06 사용자 확인)

- **`SmallBoat`의 지형** — 원문의 `[육지만]`은 오타. 다른 배 2종과 같이 **대하**다.
- **`Axeman`의 산악 이동** — 산악에 들어가면 **그 턴의 이동이 1칸으로 제한**된다.
  산악 타일의 진입 비용이 1이라는 뜻이 아니다.
- **`ThunderCart` / `Catapult`** — 외형이 다르고, 사거리가 갈린다(`ThunderCart` 전부 1 /
  `Catapult` 유닛 3·건물 2). 지형·속도만 같다. 이후 전투 수치에서도 더 갈라질 수 있다.

## 열린 결정 (기존)

- 병력↔기수 수 매핑, 대형 모양 확정
- 병종별 모션 차별화(궁병=사격, 공성=정지 후 발사 등)
- 세력 색 팔레트 값
- 피격/사망(패주) 모션 필요 여부
- 병종 상성·전투 수치 (밸런스 영역, 별도 문서 예정)
- 특기(`Skill`) 목록 — 기병 관통은 **행군모드**로 일반화되어 특기에서 빠졌다
  ([design-movement.md](./design-movement.md) 참조). 첫 후보를 다시 정해야 한다
