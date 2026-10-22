# SanguoSLG

> **클론해서 이어서 작업한다면 [ONBOARDING.md](ONBOARDING.md)를 먼저 읽는다** — 준비·빌드·실행·문서 맵·현황·다음 작업.

삼국지 전략 게임에서 영감을 받은 **전투 중심 오프라인 싱글플레이 SLG**. 명령 후 7일을 진행하며, 매일 연속 좌표 이동턴과 공격턴을 순서대로 처리한다.

- 플랫폼: Windows 데스크톱 (우선), 이후 Linux/macOS 고려
- 시점: 헥사 지형 기반 쿼터뷰(기울어진 3D 카메라). 부대 목적지와 실제 위치는 연속 좌표다.
- 표현: 실시간 3D 렌더링 (Godot 3D) — 저폴리 프로시저럴 에셋(Blender 스크립트 생성) + 실시간 조명·그림자

## 현재 상태 (2026-10-22)

- v2 방향은 **내정 과잉을 줄이고 전투·보급·성장·탐색·외교 최소화에 집중**한다.
- 캠페인 루프는 이동·전투·공성·함락·약탈·자동 담당자 내정·연구·탐색·외교·임명·저장/불러오기·HUD/보고 패널을 중심으로 유지한다.
- 제거/축소 방향: 인구, 세율, 시장, 모병/징병 분리, 충성, 급여, 배신, 포로교환, 과도한 시설 건설/업그레이드.
- 구현 축: 자동 담당자·병력 생산·연구·최소 외교·위인/탐색, 생산 작전·보급·수송·집단군·항구, 장수 성장, 액티브 연출, 유적 점령/병종 해금, 야전 건축, 연속 좌표 이동, 대호·용·봉황 이벤트와 보물 장착·소환.
- Phase 18D 이동 개편은 1차 검수를 마쳤으나 실제 플레이 회귀 QA는 계속한다. Phase 19·19A·20과 고대유물 10종의 획득·장착·효과는 구현되었고, 다음 기능 단계는 **진행 중인 Phase 21 AI 조정**, 이후 Phase 9B 문·무과시험과 Phase 22 통합 QA다.
- AI는 공세·연구·담당자·건축 판단을 일부 자동화했다. 야전 건축은 전선 후보 외에 안전한 성 주변의 60~90일 주기 후보도 검토한다. 야생 신수는 탐색 위치에서 대기·반격만 하고 150일 뒤 사라지며, 소환 신수만 이동·선제공격한다.
- `run-maptest.bat`의 신수 흔적 확률은 테스트 편의를 위한 50%이며, 일반 캠페인의 기준값은 1%다.
- **상세 현황·다음 작업은 [ONBOARDING.md](ONBOARDING.md) · [v2 구현 로드맵](./doc/plan-v2-implementation-roadmap.md) · [UI 개선 목록](./doc/plan-ui-improvement.md)** 참조

## 설계 문서

| 문서 | 내용 |
|---|---|
| [doc/README.md](./doc/README.md) | 현행 기준·과거 계획·QA 문서의 구분과 전체 문서 지도 |
| [doc/spec-unit.md](./doc/spec-unit.md) | 병종·이벤트 유닛 카탈로그, 분류·통행 규칙·구현 현황 |
| [doc/spec-general.md](./doc/spec-general.md) | 장수 능력치(병종별 통솔 F~SSS)·스킬 슬롯 |
| [doc/design-combat.md](./doc/design-combat.md) | 전투 공식·병종 수치·성/항구 공성·검증 시뮬레이션 |
| [doc/plan-renewal-movement.md](./doc/plan-renewal-movement.md) · [design-movement.md](./doc/design-movement.md) | 연속 좌표 이동의 확정 규칙과 이동·교전 설계 이력 |
| [doc/design-event-hunts.md](./doc/design-event-hunts.md) | 신수 사냥·보상·장착·소환의 현행 규칙 |
| [doc/design-terrain.md](./doc/design-terrain.md) · [design-water.md](./doc/design-water.md) · [design-effect.md](./doc/design-effect.md) | 지형·물/강·효과 |
| [doc/asset-icon-generation.md](./doc/asset-icon-generation.md) | Fooocus 아이콘 생성 — 프롬프트·설정·진행 현황 |

## 기술 스택

- 언어: C# (.NET 9)
- 엔진: Godot 4.7 (.NET 빌드)
- 테스트: xUnit
- 직렬화: `System.Text.Json`
- 에셋: 3D 모델은 Blender 헤드리스 스크립트(저폴리), UI 아이콘은 로컬 Fooocus(SDXL) 생성 + 후처리

## 에셋 파이프라인

- **3D 모델**: 저폴리 프로시저럴 — Blender 헤드리스 스크립트로 생성.
- **UI 아이콘**: 로컬 **Fooocus**(SDXL / juggernautXL)로 삼국지풍 **원형 금테 배지** 아이콘을
  생성한 뒤 → **원형 크롭·투명(알파)·크기 정규화**(Pillow) → `SanguoSLG.Game/assets/icons/`에
  배치 → `CampaignMapScene`의 엠블럼/아이콘 로더에 배선한다.
  - 파일이 있으면 실제 이미지를, 없으면 코드 생성(절차적) 아이콘을 폴백으로 사용.
  - 프롬프트 템플릿·확정 생성 설정(Styles·Performance·Seed 등)·진행 현황은
    [doc/asset-icon-generation.md](./doc/asset-icon-generation.md) 참조.
  - 오프라인 로컬 생성이라 비용·라이선스 부담이 없다.

## 솔루션 구조

```
SanguoSLG.Core/        # 순수 C# 라이브러리 — 엔진 의존성 0 (도메인, 시뮬레이션, AI, 데이터)
SanguoSLG.Core.Tests/  # xUnit 테스트
SanguoSLG.Sandbox/     # 콘솔 밸런스 시뮬레이션
SanguoSLG.Game/        # Godot 프로젝트 — 표현 계층
data/                  # JSON 게임 데이터
```

핵심 원칙: `Core`는 Godot을 참조하지 않는다. `Game`이 `Core`를 참조하는 단방향만 허용한다.

## 빌드 / 테스트

```bash
# 로직 검증 (Core·Tests를 명시적으로 지정)
dotnet test SanguoSLG.Core.Tests/SanguoSLG.Core.Tests.csproj

# 밸런스 시뮬레이션 (필요할 때)
dotnet run --project SanguoSLG.Sandbox

# Godot 프로젝트 컴파일 검증 (Godot 4.7 .NET/mono 필요)
dotnet build SanguoSLG.Game/SanguoSLG.Game.csproj
```

> `SanguoSLG.Game`은 Godot이 자체 솔루션으로 관리하므로 루트 `SanguoSLG.sln`에는 포함되지 않는다.
> 에디터로 한 번 열면 `.godot/` 메타데이터가 생성된다.

Windows 캠페인 확인은 `run-maptest.bat`, Phase 21 AI 전용 3세력 검증은 `run-aitest.bat`(`qa` 인자로 초기 배치 자동 검사), 액티브 스킬 연출은 `run-activeeffecttest.bat`, 장수 데이터 편집은 `run-general-editor.bat`을 사용한다. 실행 BAT의 Godot 경로는 로컬 설치 위치에 맞춰 확인한다. 이 저장소의 문서 작업 기준과 QA 범위는 [ONBOARDING.md](./ONBOARDING.md), [작업 노트](./doc/ai-working-notes.md)를 따른다.

## 개발 규칙

자세한 작업 규칙은 [CLAUDE.md](./CLAUDE.md) 참고.
