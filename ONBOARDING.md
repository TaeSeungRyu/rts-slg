# 이어가기 가이드 (ONBOARDING)

> 이 저장소를 **클론해 이어서 작업하는 사람**(사람·AI 모두)을 위한 진입점.
> 처음이면 **이 문서 → [CLAUDE.md](CLAUDE.md) → [doc/ai-working-notes.md](doc/ai-working-notes.md)** 순으로 읽는다.

삼국지 11 스타일 턴제 전략 SLG. 오프라인 싱글플레이. C#(.NET 9) + Godot 4.7(.NET/mono). Core는 엔진 비의존(순수 C#), Game이 표현.

---

## 1. 클론 직후 준비

- 설치: **.NET 9 SDK**, **Godot 4.7.1 mono(.NET) 빌드**(일반 빌드는 C# 실행 불가).
- **`run-*.bat`의 `GODOT` 경로**를 자기 PC 설치 위치로 수정한다(기본값은 이 개발 PC의 `D:\godot\...`).
  머신 의존 경로 전체는 [doc/ai-working-notes.md](doc/ai-working-notes.md) §5.
- Blender·Fooocus·Kenney 에셋 팩은 **새 아트를 만들 때만** 필요. 이미 쓰는 에셋은 저장소에 커밋돼 있어
  **게임 실행에는 불필요**하다.

## 2. 빌드 · 테스트 · 실행

```bash
dotnet build                                              # Core·Tests·Sandbox  (⚠ Game 미포함)
dotnet test                                               # xUnit (현재 536개)
dotnet build SanguoSLG.Game/SanguoSLG.Game.csproj         # Game(Godot) C# 검증 — 루트 build가 빼먹으므로 필수
"<godot-mono>" --headless --path SanguoSLG.Game --build-solutions --quit   # 씬/솔루션 컴파일 검증
```

- 플레이(주 진입 씬): **`run-maptest.bat`**(캠페인 맵). 내정 전용 씬은 `run-admin.bat`.
- 밸런스 검증: `dotnet run --project SanguoSLG.Sandbox -- --balance 42` (현재 42/42 수렴).

## 3. 어디를 읽고 이어가나 (문서 맵)

| 순서 | 문서 | 용도 |
|---|---|---|
| 1 | [CLAUDE.md](CLAUDE.md) | **절대 규칙**(Core 순수성·테스트 동반·데이터화·결정론)·용어집·설계 문서 인덱스 |
| 2 | [doc/ai-working-notes.md](doc/ai-working-notes.md) | 작업 방식(상시 규칙)·조용히 통과하는 **검증 함정**·머신 의존 경로 |
| 3 | [doc/plan-roadmap.md](doc/plan-roadmap.md) | **전체 현황·단계 계획·다음 작업**(살아있는 문서) |
| 4 | [doc/test/qa-checklist.md](doc/test/qa-checklist.md) | 최근 배치 수동 QA 체크리스트 |
| — | `doc/design-*.md` · `doc/spec-*.md` | 영역별 설계 논의·확정 사양 |

## 4. 현재 상태 (2026-08-28)

- **캠페인 루프 완성**: 이동(다중 경유지)·전투·공성·함락·약탈, 내정 명령(모병·징병·훈련·건설·세율·
  연구·수리·도시계략), 태수/군사 임명, 등용(적 성·출전중·포로), 충성 운영(급여·미지급·배신·포상),
  시장(계절 랜덤 시세·구매), 게임 저장/불러오기, 좌상단 HUD + 시스템 팔레트(장수·도시·보물 목록).
- **시설 건설 타일 배치**(2026-08-27): 성 주변 **평지·숲**에 반투명 고스트로 위치를 골라 설치(설치 컨펌·
  화면 딤). 건설 중엔 **공사장 에셋**(construction.glb)·흙먼지·`남은/총 일수` 라벨, 완료 시 실제 모델로 교체.
  **공사 인력=인구에서 1000 차감**(완료 시 복귀·적에게 파괴되면 전멸). **적군만** 인접 시 공사를 공격→취소.
  위치는 `GameState.FacilityPlacements`(저장 보존). 규칙은 [design-administration.md](doc/design-administration.md) "시설 건설".
- 시장 매입 UI: 수량 **슬라이더/숫자(1단위)**·자원 사진 슬롯. 이동: 다중 부대 동일 목표 수렴(우왕좌왕 해결).
- **좌하단 보고 패널**(삼국지11 오마주): 고정 위치·스크롤·[전체]는 전체 화면. 진행 재생 중 아군 이동 경로 표시.
- **내정 스킬 12종 전부 배선**. **시각 효과 14종 전부 구현**(design-effect — SoulRise 전멸 소멸·Lightning;
  낙뢰 발동 배선은 ❓ 보류). 테스트 **536개(Core)** 통과, 밸런스 **42/42 수렴**.
- 표현: `run-maptest`(캠페인 3D)·`run-admin`(내정) 씬. 최근 배치 수동 QA는 [doc/test/qa-checklist.md](doc/test/qa-checklist.md).

## 5. 다음 작업

- **[doc/plan-roadmap.md](doc/plan-roadmap.md) 최상단 미완료 단계**부터 잡는다.
- 크게 남은 것: **탐색·보물**(설계 [design-general-lifecycle.md](doc/design-general-lifecycle.md) §8, 미구현) ·
  **외교 포로교환**(§7) · **실지역 맵·시나리오(장수 152명 배속)** · **AI 개선** · 부대 소멸/전투 연출.
- 원칙: 끼어드는 아이디어는 해당 `design-*` 문서에 `❓`로 기록만 하고 로드맵 순서를 따른다.
  게임 밸런스·규칙(전투 공식·성장·AI 우선순위)은 **설계자(사용자) 확정**이 필요 — 임의로 정하지 않는다.

## 6. AI로 이어갈 때

- 새 세션은 **CLAUDE.md + doc/ai-working-notes.md를 먼저 읽는다**(작업 방식·검증 함정이 거기 있다).
- 작업 방식 요약: 스크린샷 안 찍음 · 게임 창 자동 실행 안 함 · 코드 주석 최소화 · claude.ai 아티팩트 금지 ·
  "다음 작업"은 항상 plan-roadmap에서.
- 커밋: Conventional Commits, 본문 한국어, 트레일러 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- 완료 보고 전 반드시 `dotnet build` + `dotnet test` (+ Game을 건드렸으면 Game 빌드/헤드리스)까지 실행한다.
