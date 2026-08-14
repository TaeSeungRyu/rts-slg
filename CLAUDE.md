# CLAUDE.md

이 문서는 Claude Code가 이 저장소에서 작업할 때 반드시 따라야 할 규칙이다.

## 프로젝트 개요

삼국지 11 스타일의 **턴제 전략 시뮬레이션(SLG)** 게임. 오프라인 싱글플레이 전용이며 네트워크 기능은 없다.

- 플랫폼: Windows 데스크톱 (우선), 이후 Linux/macOS 고려
- 시점: 헥사 그리드 기반 쿼터뷰(기울어진 3D 카메라)
- 표현 방식: **실시간 3D 렌더링 (Godot 3D)**. 저폴리/에셋 기반 3D 헥사 맵 + 실시간 조명·그림자. (2026-08-04 결정: 원래 "2D 스프라이트 + 노멀맵" 계획이었으나, 무료 카툰 2D의 시각적 한계로 3D 전환. 사용자 판단 — UI/비주얼 만족이 프로젝트 지속의 관문)
- **Core는 여전히 렌더링 방식을 모른다**: 헥사 좌표·A*·시뮬레이션은 2D/3D와 무관. 3D 전환은 Game(표현) 계층만 바꾼다. axial↔월드 좌표 변환은 Game에서만.
- 개발자는 1인이며 C#/.NET 백엔드 경험이 주력이다. 게임 엔진 관용구보다 **일반적인 C# 설계 원칙**을 우선한다.

## 기술 스택

| 항목 | 버전 / 선택 |
|---|---|
| 언어 | C# (nullable enable, implicit usings 사용) |
| 런타임 | .NET 9 SDK |
| 엔진 | Godot 4.7 (.NET / Mono 빌드) |
| 테스트 | xUnit |
| 직렬화 | `System.Text.Json` (Newtonsoft 사용 금지) |
| 에셋 파이프라인 | 무료 CC0 3D 에셋(Kenney 등) + Godot 3D. 필요 시 Blender 편집 |

GDScript는 사용하지 않는다. 모든 게임 코드는 C#으로 작성한다.

## 솔루션 구조

```
SanguoSLG/
├─ SanguoSLG.Core/          # 순수 C# 클래스 라이브러리 — 엔진 의존성 0
│   ├─ Domain/              # 무장, 도시, 부대, 세력 등 도메인 모델
│   ├─ Simulation/          # 턴 루프, 전투 계산, 내정 처리
│   ├─ AI/                  # 세력별 의사결정
│   └─ Data/                # JSON 로딩, 리포지토리
├─ SanguoSLG.Core.Tests/    # xUnit 테스트
├─ SanguoSLG.Sandbox/       # 콘솔 앱 — 밸런스 시뮬레이션 / 대량 턴 검증용
├─ SanguoSLG.Game/          # Godot 프로젝트 — 표현 계층
└─ data/                    # JSON 게임 데이터 (무장, 도시, 병종, 이벤트)
```

---

## 절대 규칙

### 1. Core는 엔진을 몰라야 한다

`SanguoSLG.Core`, `SanguoSLG.Core.Tests`, `SanguoSLG.Sandbox` 안에 **`using Godot;`이 단 한 줄도 들어가서는 안 된다.**

- `Vector2`가 필요하면 Core 안에 자체 `HexCoord`, `Point2` 등을 정의해서 쓴다
- `GD.Print` 대신 `Microsoft.Extensions.Logging` 또는 Core가 정의한 로깅 인터페이스를 쓴다
- 파일 접근에 `FileAccess`(Godot) 대신 `System.IO`를 쓴다
- Core → Game 방향 참조는 금지. **Game이 Core를 참조**하는 단방향만 허용한다

이 규칙이 깨지면 콘솔 밸런스 시뮬레이션과 단위 테스트가 전부 무너진다. 위반 소지가 있는 요청을 받으면 코드를 쓰기 전에 먼저 지적하고 대안을 제시할 것.

### 2. 게임 로직은 반드시 테스트를 동반한다

`Simulation/`, `AI/`, `Domain/`에 로직을 추가하거나 수정하면 같은 커밋에 xUnit 테스트를 함께 작성한다. 전투 계산식, 내정 수치 변동, 턴 전이 조건은 예외 없이 테스트 대상이다.

작업 완료 전 반드시 실행:

```bash
dotnet build
dotnet test
```

테스트가 실패한 상태로 "완료했다"고 보고하지 않는다.

### 3. 게임 데이터는 하드코딩하지 않는다

무장 능력치, 도시 정보, 병종 상성, 지형 보정치, 이벤트는 전부 `data/*.json`에 둔다. C# 코드에 매직 넘버로 박지 않는다.

밸런스 상수(예: 기본 병력 소모율)는 `data/balance.json` 한 곳에 모으고, 코드에서는 설정 객체로 주입받는다.

### 4. 결정론을 유지한다

같은 입력과 같은 시드는 항상 같은 결과를 내야 한다. 밸런스 시뮬레이션의 전제 조건이다.

- `System.Random`을 전역이나 `new Random()`으로 즉석 생성하지 않는다
- 시드 기반 `IRandomSource`를 주입받아 사용한다
- `DateTime.Now`, `Guid.NewGuid()`를 시뮬레이션 로직 안에서 호출하지 않는다
- 순회 순서가 결과에 영향을 주는 곳에서 `Dictionary` 순서에 의존하지 않는다

---

## 코딩 규약

### 일반

- nullable reference types 활성화. 경고를 무시하지 말 것
- 도메인 모델은 가능한 한 불변(immutable)으로. 상태 변경은 명시적 메서드를 통해서만
- 원시 타입 대신 강타입 ID 사용: `int` 대신 `GeneralId`, `CityId` (record struct)
- 도메인 개념은 영어 식별자로 쓰되, 한국어 용어와의 대응은 아래 용어집을 따른다
- 주석과 XML 문서 주석은 한국어로 작성한다
- 파일 하나에 public 타입 하나

### Godot C# (Game 프로젝트 한정)

- 생명주기 메서드는 PascalCase 오버라이드: `_Ready()`, `_Process(double delta)`, `_PhysicsProcess(double delta)`
- 시그널 연결은 C# 이벤트 문법 사용: `button.Pressed += OnPressed;`
- 노드 참조는 `[Export]`로 에디터 노출하거나 `GetNode<T>("%UniqueName")` 사용. 긴 경로 문자열 하드코딩 금지
- 씬 파일(`.tscn`)과 리소스(`.tres`)는 텍스트 포맷이므로 직접 편집 가능하다. 다만 편집 후 반드시 헤드리스 빌드로 검증할 것
- Godot 노드 클래스에는 게임 규칙을 넣지 않는다. Core를 호출하고 결과를 화면에 반영하는 역할만 한다

### 명명

| 대상 | 규칙 | 예 |
|---|---|---|
| 클래스/메서드/프로퍼티 | PascalCase | `BattleResolver` |
| private 필드 | `_camelCase` | `_currentTurn` |
| 상수 | PascalCase | `MaxTroopCount` |
| 테스트 메서드 | `대상_조건_기대결과` | `Resolve_방어측이산지에있으면_방어보정이적용된다` |
| JSON 키 | snake_case | `"leadership_stat"` |

---

## 도메인 용어집

코드에서는 영어를 쓰되 아래 대응을 지킨다. UI 문자열과 주석은 한국어를 쓴다.

| 한국어 | 코드 식별자 |
|---|---|
| 무장 | `General` |
| 세력 | `Faction` |
| 군주 | `Ruler` |
| 도시 | `City` |
| 부대 | `Unit` |
| 병종 | `TroopType` |
| 특기 | `Skill` |
| 내정 | `Administration` |
| 병종별 통솔(적성) / 무력 / 지력 / 정치 | `Aptitudes`(`AptitudeGrade`) / `Might` / `Intellect` / `Politics` |
| 선봉 / 부관 | `Vanguard` / `Adjutant` |
| 병력 | `Troops` |
| 사기 | `Morale` |
| 군량 | `Provisions` |
| 자금 | `Gold` |
| 충성도 | `Loyalty` |
| 계략 | `Stratagem` |
| 턴 / 개월 | `Turn` / `Month` |
| 헥사 좌표 | `HexCoord` |

## 헥사 그리드 규약

- **flat-top 육각형**, **axial 좌표계**(`q`, `r`) 사용. offset 좌표계와 섞지 않는다
- `HexCoord`는 Core에 정의된 `readonly record struct`
- 화면 좌표 변환은 Game 프로젝트에서만 수행한다. Core는 화면 픽셀을 모른다
- 길찾기는 Core에 자체 A* 구현. Godot의 `AStarGrid2D`는 Core에서 사용 불가

---

## 설계 문서 맵 (doc/)

게임 규칙·수치는 코드가 아니라 `doc/`가 원본이다. **관련 영역을 건드리기 전에 반드시 해당
문서를 먼저 읽고, 규칙이 바뀌면 같은 커밋에서 문서를 갱신한다.**

명명 규칙: `design-*` = 설계 논의·확정 규칙, `spec-*` = 확정 사양 + 구현 현황,
`plan-*` = 단계별 계획(이력), `test/*` = GUI·통합 검증 케이스 정의.

| 문서 | 이럴 때 참고 |
|---|---|
| [design-movement.md](doc/design-movement.md) | 이동·탐지·추격·정지·우회·성 입성/출격·지형 이동 패널티 |
| [design-combat.md](doc/design-combat.md) | 피해 공식·병종 공/방·지형 전투 보정·성 전투(성벽/붕괴/반격/함락) |
| [design-stratagem.md](doc/design-stratagem.md) | 계략 11종 수치·시전 사거리·지속 상태·정화 |
| [design-skill.md](doc/design-skill.md) + skill-actives/passives | 특기 체계, 액티브 게이지·발동, 패시브 버킷 |
| [design-skill-admin.md](doc/design-skill-admin.md) | 내정 스킬 13종(상재·둔전·진무 등) — 효과 배선은 내정 구현과 함께 |
| [design-unit-state.md](doc/design-unit-state.md) | 사기·훈련도·군량 시스템 계획(초안, ❓=미확정) |
| [design-administration.md](doc/design-administration.md) | 내정 — 도시 속성·시간 축·수입·모집 명령(초안, ❓=미확정) |
| [design-terrain.md](doc/design-terrain.md) | 지형 종류·타일 배치 |
| [design-effect.md](doc/design-effect.md) | 시각 효과 계획(구현 O/X 표 포함) |
| [design-ui.md](doc/design-ui.md) | UI 개선 4건 계획(미구현) |
| [design-water.md](doc/design-water.md) | 소하천/대하 표현 |
| [spec-unit.md](doc/spec-unit.md) | 병종 11종 확정 스탯·모델·이동/사거리 데이터 |
| [spec-general.md](doc/spec-general.md) | 무장 스탯·특기 슬롯 사양 |
| [spec-city.md](doc/spec-city.md) | 도시 속성 스키마·성곽 등급·도시 흐름 구현 현황 |
| [test/movement-cases.md](doc/test/movement-cases.md) | 이동 검증 케이스 1~8 정의·구현 현황 |
| [test/combat-movement-cases.md](doc/test/combat-movement-cases.md) | 이동→전투 통합 케이스·공성 하베스트 케이스 |

---

## 작업 절차

### 진행 순서

현재 우선순위는 **Core 우선, 엔진 나중**이다.

1. 도메인 모델 + 턴 루프
2. 내정 시스템
3. 전투 계산
4. 세력 AI
5. Sandbox로 1000턴 밸런스 검증
6. Godot 표현 계층

3~5단계가 안정되기 전에 Godot 화면 작업을 먼저 하자고 제안하지 말 것. 요청받은 경우에도 순서상의 위험을 먼저 언급한다.

### 검증 명령

```bash
# 로직 검증 (가장 자주 쓰는 명령)
dotnet build
dotnet test

# 밸런스 시뮬레이션
dotnet run --project SanguoSLG.Sandbox -- --turns 1000 --seed 42

# Godot 프로젝트 컴파일 검증
godot --headless --path SanguoSLG.Game --build-solutions --quit
```

### 커밋

- Conventional Commits 사용: `feat:`, `fix:`, `refactor:`, `test:`, `chore:`
- 커밋 메시지 본문은 한국어
- 빌드가 깨진 상태나 테스트 실패 상태로 커밋하지 않는다

---

## 하지 말아야 할 것

- Core에 Godot 타입 반입
- 테스트 없이 시뮬레이션 로직 추가
- 게임 수치를 C# 코드에 하드코딩
- Godot 노드 클래스에 전투 계산이나 AI 로직 작성
- 시드 없는 난수 사용
- 요청하지 않은 리팩터링을 대규모로 수행 — 발견한 문제는 보고하고 승인을 받은 뒤 진행
- Newtonsoft.Json, AutoMapper, MediatR 등 무거운 라이브러리 임의 도입 — 새 NuGet 패키지는 먼저 물어볼 것
- 스텁이나 `NotImplementedException`을 남기고 완료 보고

## 애매할 때

명세가 불분명하면 추측해서 구현하지 말고 질문한다. 특히 게임 밸런스와 규칙(전투 공식, 성장 곡선, AI 행동 우선순위)은 설계자의 의도가 있는 영역이므로 임의로 정하지 않는다.
