# 개발 계획 01 — Walking Skeleton (1단계)

> CLAUDE.md 우선순위의 **1단계(도메인 + 턴 루프)**를 "걷는 뼈대"로 얇게 관통시키는 계획.
> 시스템을 깊게 파기 전에 4대 절대 규칙이 코드로 지켜지는지 먼저 증명한다.

## 확정된 설계 결정

- **접근 방식**: walking skeleton — 데이터 로드 → 월 단위 턴 진행 → Sandbox 결정론 실행을 얇게 관통
- **턴 모델**: 월 = 턴, 전 세력이 매월 고정 순서로 순차 행동 (삼국지 11식)
- **맵 표현**: 처음부터 `HexCoord` (axial 좌표, flat-top) 기반

## 이 단계에서 증명할 것 (4대 규칙)

1. Core에 `using Godot;` 없이 시뮬레이션이 돈다 (엔진 독립)
2. 로직에 테스트가 동반된다
3. 게임 수치가 코드가 아니라 `data/*.json`에 있다
4. 같은 시드 + 같은 초기 상태 → 항상 같은 결과 (결정론)

---

## 단계 세분화

한 번에 다 하지 않는다. 각 단계는 **독립적으로 빌드·테스트되는 1개 커밋**이며, 완료 시 `dotnet build` / `dotnet test` green 확인 후 원격에 푸시한다.

범례: 🔍 = **사용자가 직접 확인할 수 있는 지점**

### Step 1 — Spatial: HexCoord
- **내용**: `SanguoSLG.Core/Spatial/HexCoord.cs` — `readonly record struct`, axial `(q, r)`, `Distance`, `Neighbors`
- **테스트**: 거리 계산, 6방향 이웃
- **확인**: 자동 테스트 통과로 검증 (길찾기 A*는 이동이 생기는 이후 단계로 연기)

### Step 2 — 강타입 ID + Domain 엔티티
- **내용**: `FactionId` / `CityId` / `GeneralId` (record struct), `City`(HexCoord 위치·소유 세력·Gold·Provisions), `Faction`(군주·보유 도시), `General`(오6능력치 최소 필드). 그릇 수준, 가급적 불변
- **테스트**: 동등성, 생성 불변성 최소 검증
- **확인**: 자동 테스트/빌드

### Step 3 — Data 계층 + 더미 시나리오
- **내용**: `System.Text.Json` 로더/리포지토리, `data/factions.json`·`cities.json`·`generals.json`·`balance.json` (검증용 더미 시나리오)
- **테스트**: JSON 라운드트립 로딩, 로드된 엔티티 개수/필드 검증
- 🔍 **사용자 확인 지점**: `data/*.json`을 직접 열어 시나리오(세력·도시 배치)를 눈으로 확인·수정 가능

### Step 4 — 결정론 기반 + 턴 엔진
- **내용**: `IRandomSource` + 시드 구현, `GameState`(현재 Turn/Month·세력·도시·무장), `TurnEngine.AdvanceMonth()` — 고정 순서(FactionId 오름차순)로 전 세력 순회 → 세력별 사소한 세수 틱(상태가 실제로 변하게) → 월 증가(12월→익년 1월 롤오버)
- **테스트**: 월 롤오버(12→1, 연도+1), 세력 순회 순서 고정, RNG 시드 재현성
- **확인**: 자동 테스트

### Step 5 — Sandbox 실행기 🔍🔍
- **내용**: `--turns N --seed S` 파싱 → 데이터 로드 → N턴 실행 → 세력별 최종 상태(Gold 등) 요약 출력
- 🔍 **가장 뚜렷한 사용자 확인 지점**: 직접 실행해서 턴 진행을 눈으로 보고, **같은 시드로 두 번 돌려 출력이 동일한지** 확인
  ```bash
  dotnet run --project SanguoSLG.Sandbox -- --turns 12 --seed 42
  ```

### Step 6 — 결정론 통합 테스트
- **내용**: 같은 시드 + 같은 초기 상태로 N턴 실행 → 최종 상태 완전 동일(스냅샷 비교). 규칙 #4를 테스트로 못박음
- **확인**: 자동 테스트

---

## 이 단계에서 명시적으로 제외

전투 계산 · AI 의사결정 · 실제 내정 밸런스 수치 · 부대/이동 · A* 길찾기 · Godot 표현 계층. 전부 이후 단계.

## 열린 결정 (설계자 확인 필요 — 임의로 정하지 않음)

- **balance.json의 세수 계수**: 파이프 검증용 임시값. 실제 밸런스는 설계 영역
- **초기 시나리오 데이터**: 검증용 더미. 실제 삼국지 세력/무장 데이터는 이후에 채움
- **게임 테마 · 컨셉 · 세계관 · 실제 콘텐츠**: 1단계(이 스켈레톤)가 완료된 뒤에 착수한다 (사용자 결정)

## 진행 상태

- [x] Step 1 — HexCoord
- [x] Step 2 — ID + Domain
- [x] Step 3 — Data + 더미 시나리오 🔍
- [ ] Step 4 — 턴 엔진 + 결정론 기반
- [ ] Step 5 — Sandbox 실행기 🔍🔍
- [ ] Step 6 — 결정론 통합 테스트
