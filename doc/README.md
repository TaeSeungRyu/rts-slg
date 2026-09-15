# 문서 안내 — 현재 기준과 읽는 순서

이 디렉토리는 설계 원본, v2 전환 로드맵, 구현 이력, QA 체크리스트가 함께 들어 있다. 새 작업자는 아래 순서로 읽는다.

## 최우선 기준

1. [ai-working-notes.md](./ai-working-notes.md)
   - 이 저장소에서 작업할 때의 QA, 커밋, Godot 실행, 사용자 선호 규칙.
   - 기능 구현·버그 수정·UI 변경은 요구사항별 QA 후 커밋·푸시한다.
2. [plan-v2-implementation-roadmap.md](./plan-v2-implementation-roadmap.md)
   - 현재 진행 기준인 전투 중심 v2 작업 보드.
   - 단계 상태, 완료 기준, 후속 Phase 배치가 여기서 결정된다.
3. [plan-roadmap.md](./plan-roadmap.md)
   - 전체 프로젝트의 살아있는 상위 로드맵.
   - v2 전환 이후에는 `plan-v2-implementation-roadmap.md`를 우선하고, 이 문서는 큰 흐름과 과거 맥락 확인용으로 본다.

## 핵심 설계 문서

| 분야 | 문서 | 메모 |
|---|---|---|
| 전투 계산 | [design-combat.md](./design-combat.md) | 공방 산출, 피해, 성/집단군/수성 규칙 |
| 이동·입성 | [design-movement.md](./design-movement.md) | 이동 → 입성 → 공격 순서와 경로 규칙 |
| 부대 상태·보급·수송 | [design-unit-state.md](./design-unit-state.md) | 군량, 보급부대, 수송, 괴멸 전리품 |
| 병종/유닛 | [spec-unit.md](./spec-unit.md) | 병종 분류, 시야, 집단군, 해상/항구 연계 |
| 장수 | [spec-general.md](./spec-general.md) | 능력치, 적성, 스킬 슬롯, 성장 |
| 장수 라이프사이클 | [design-general-lifecycle.md](./design-general-lifecycle.md) | 위인 해금, 탐색, 등용, 함락/멸망 획득 |
| 내정 | [design-administration.md](./design-administration.md) | v2 담당자 4슬롯, 생산, 항구 경제 |
| UI | [design-ui.md](./design-ui.md) | 명령 팔레트, 장수/부대/스킬 표시 |
| 스킬 | [design-skill.md](./design-skill.md), [design-skill-actives.md](./design-skill-actives.md), [design-skill-passives.md](./design-skill-passives.md), [design-skill-admin.md](./design-skill-admin.md) | 액티브/패시브/내정 패시브 |
| 계략 전환 | [design-stratagem.md](./design-stratagem.md) | 독립 계략 폐기, 전투 액티브 전환 기준 |
| 외교 | [design-diplomacy-ruler-relations.md](./design-diplomacy-ruler-relations.md) | 동맹/동맹파기와 군주 관계도 |
| 지형·건물·항구 | [design-terrain.md](./design-terrain.md), [design-water.md](./design-water.md) | 지형 문자, 시설, 항구/물 규칙 |
| 이펙트 | [design-effect.md](./design-effect.md) | 3D 효과 카탈로그 |

## 에셋·콘텐츠 문서

| 문서 | 용도 |
|---|---|
| [asset-icon-generation.md](./asset-icon-generation.md) | 명령/병종/스킬 아이콘 제작 가이드와 현황 |
| [asset-general-portraits.md](./asset-general-portraits.md) | 장수 초상 제작·배선·원형 얼굴 구도 메타데이터 |
| [asset-general-faction-groups.md](./asset-general-faction-groups.md) | 장수 세력/지역권 1차 그룹화 |
| [review-game-direction-2026-09.md](./review-game-direction-2026-09.md) | 전투 중심 방향성 검토 기록 |

## 과거 계획 문서

아래 문서는 초기 개발 순서와 구현 이력을 보존한다. 현재 작업 순서는 `plan-v2-implementation-roadmap.md`를 우선한다.

- [plan-01-walking-skeleton.md](./plan-01-walking-skeleton.md)
- [plan-02-map-units.md](./plan-02-map-units.md)
- [plan-03-administration.md](./plan-03-administration.md)
- [plan-04-visual-polish.md](./plan-04-visual-polish.md)
- [plan-05-ui-shell.md](./plan-05-ui-shell.md)
- [plan-06-3d.md](./plan-06-3d.md)
- [plan-combat-focused-refactor.md](./plan-combat-focused-refactor.md)
- [plan-combat-redesign-v2.md](./plan-combat-redesign-v2.md)

## QA 문서

수동·자동 QA 기준은 [test/](./test/) 아래에 둔다.

- [test/qa-checklist.md](./test/qa-checklist.md): 수동 회귀 체크리스트.
- [test/vision-cases.md](./test/vision-cases.md): 정찰 표시와 전장의 안개.
- [test/castle-entry-regression.md](./test/castle-entry-regression.md): 성 입성·화면 재생 회귀.
- [test/movement-cases.md](./test/movement-cases.md), [test/combat-movement-cases.md](./test/combat-movement-cases.md): 이동/교전 시나리오.

## 문서 관리 규칙

- 새 기능은 먼저 해당 `design-*` 또는 `spec-*` 문서에 규칙을 남긴 뒤 로드맵 Phase에 연결한다.
- 구현이 완료되면 `plan-v2-implementation-roadmap.md`의 체크표와 해당 설계 문서의 상태 문구를 갱신한다.
- 과거 구현 이력은 삭제하지 않되, 현재 기준과 충돌하면 “현재 기준” 문구를 문서 상단에 추가한다.
- 문서만 수정한 경우에도 링크 무결성 또는 핵심 키워드 검색으로 QA를 남긴다.
