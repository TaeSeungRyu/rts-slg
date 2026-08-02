# SanguoSLG

삼국지 11 스타일의 **턴제 전략 시뮬레이션(SLG)** 게임. 오프라인 싱글플레이 전용.

- 플랫폼: Windows 데스크톱 (우선), 이후 Linux/macOS 고려
- 시점: 헥사 그리드 기반 쿼터뷰
- 표현: 2D 스프라이트 + 노멀맵 조명으로 3D 느낌 구현

## 기술 스택

- 언어: C# (.NET 9)
- 엔진: Godot 4.7 (.NET 빌드)
- 테스트: xUnit
- 직렬화: `System.Text.Json`

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
