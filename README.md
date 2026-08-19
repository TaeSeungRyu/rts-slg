# SanguoSLG

삼국지 11 스타일의 **턴제 전략 시뮬레이션(SLG)** 게임. 오프라인 싱글플레이 전용.

- 플랫폼: Windows 데스크톱 (우선), 이후 Linux/macOS 고려
- 시점: 헥사 그리드 기반 쿼터뷰(기울어진 3D 카메라)
- 표현: 실시간 3D 렌더링 (Godot 3D) — 저폴리 프로시저럴 에셋(Blender 스크립트 생성) + 실시간 조명·그림자

## 현재 상태 (2026-08-07)

- 헥사 맵·지형·성곽·항구 3D 표현, 병종 24종(세력 19 + 이벤트 5)의 모델·이동/공격 모션 완료
- 병종별 통행 규칙(성·항구 진입 금지, 산·물·얼음 판정) 구현 — Core + xUnit
- 전투 설계 확정: 피해 공식·장수 적성·성벽 3단계 공성
- 다음 작업: 이동 시뮬레이션 GUI 검증 — [doc/test/movement-cases.md](./doc/test/movement-cases.md)

## 설계 문서

| 문서 | 내용 |
|---|---|
| [doc/spec-unit.md](./doc/spec-unit.md) | 병종 카탈로그(24종)·분류·통행 규칙·구현 현황 |
| [doc/spec-general.md](./doc/spec-general.md) | 장수 능력치(병종별 통솔 F~SSS)·스킬 슬롯 |
| [doc/design-combat.md](./doc/design-combat.md) | 전투 공식·병종 수치·성/항구 공성·검증 시뮬레이션 |
| [doc/design-movement.md](./doc/design-movement.md) | 일(日) 단위 이동·탐지·추격·행군/공격 모드 |
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
# 로직 검증
dotnet build
dotnet test

# 밸런스 시뮬레이션
dotnet run --project SanguoSLG.Sandbox

# Godot 프로젝트 컴파일 검증 (Godot 4.7 필요)
godot --headless --path SanguoSLG.Game --build-solutions --quit
```

> `SanguoSLG.Game`은 Godot이 자체 솔루션으로 관리하므로 루트 `SanguoSLG.sln`에는 포함되지 않는다.
> 에디터로 한 번 열면 `.godot/` 메타데이터가 생성된다.

## 개발 규칙

자세한 작업 규칙은 [CLAUDE.md](./CLAUDE.md) 참고.
