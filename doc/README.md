# 문서 안내 — 현재 기준과 읽는 순서

이 디렉토리는 설계 원본, v2 전환 로드맵, 구현 이력, QA 체크리스트가 함께 들어 있다. 새 작업자는 아래 순서로 읽는다.

> **현행 점검: 2026-10-31.** 현재 작업 기준은 [v2 구현 로드맵](./plan-v2-implementation-roadmap.md)이다. Phase 18D 연속 이동은 실제 플레이 회귀 QA 중이다. Phase 19·19A·20의 신수·보물·소환과 고대유물 10종은 1차 구현 완료다. Phase 21 AI는 장기 밸런스 QA, Phase 21B 지도 에디터·21C 시나리오 에디터는 실제 창 조작 검수, Phase 21D 관문은 마지막 통합 단계가 남았다. Phase 9B 문·무과시험과 Phase 22 통합 QA는 미착수다. `run-maptest.bat`의 신수 발견률 50%는 테스트 설정이며 일반 캠페인은 1%다.
> 사용자가 요청한 UI 우선 개선은 기능 Phase와 분리해 [UI 개선 작업 목록](./plan-ui-improvement.md)으로 관리한다.

## 충돌 시 우선순위

1. 사용자가 마지막으로 확정한 요구사항과 [v2 구현 로드맵](./plan-v2-implementation-roadmap.md)의 해당 Phase
2. 각 분야의 `design-*` 및 `spec-*` 문서 상단에 적힌 현행 기준
3. [상위 로드맵](./plan-roadmap.md)의 요약
4. `plan-01`~`plan-06`, 전투 중심 전환 초기안, 설계 문서의 명시된 과거 기록

과거 규칙은 결정 배경과 회귀 이력을 위해 삭제하지 않는다. 현행 기준과 충돌하면 구현 근거로 사용하지 않는다.

## 최우선 기준

새 에이전트는 루트 [AGENTS.md](../AGENTS.md)에서 시작한다. Phase 18D의 구현 인계는 [상세 계획 §15](./plan-renewal-movement.md#15-단계별-구현-인계-지침)를 따른다. 모델이 바뀌어도 확정 규칙·미정 결정·QA와 단계별 커밋 기준은 같다.

1. [ai-working-notes.md](./ai-working-notes.md)
   - 이 저장소에서 작업할 때의 QA, 커밋, Godot 실행, 사용자 선호 규칙.
   - 기능 구현·버그 수정·UI 변경은 요구사항별 QA 후 커밋·푸시한다.
2. [plan-v2-implementation-roadmap.md](./plan-v2-implementation-roadmap.md)
   - 현재 진행 기준인 전투 중심 v2 작업 보드.
   - 단계 상태, 완료 기준, 후속 Phase 배치가 여기서 결정된다.
3. [plan-roadmap.md](./plan-roadmap.md)
   - 전체 프로젝트의 상위 요약과 이전 구현 이력.
   - 현재 단계와 완료 판정은 `plan-v2-implementation-roadmap.md`를 우선한다.

## 핵심 설계 문서

| 분야 | 문서 | 메모 |
|---|---|---|
| 전투 계산 | [design-combat.md](./design-combat.md) | 공방 산출, 피해, 성/집단군/수성 규칙 |
| 이동·입성 | [plan-renewal-movement.md](./plan-renewal-movement.md), [design-movement.md](./design-movement.md) | 연속 이동의 현행 확정 규칙과 이전 타일 기반 설계 이력 |
| 이동·교전 개편 | [plan-renewal-movement.md](./plan-renewal-movement.md) | Phase 18D, 아군 중첩·연속 이동·이동턴/공격턴·공격턴 시작 위치 판정·검수 이력 |
| 부대 상태·보급·수송 | [design-unit-state.md](./design-unit-state.md) | 군량, 보급부대, 수송, 괴멸 전리품 |
| 병종/유닛 | [spec-unit.md](./spec-unit.md) | 병종 분류, 시야, 집단군, 해상/항구 연계 |
| 장수 | [spec-general.md](./spec-general.md) | 능력치, 적성, 스킬 슬롯, 성장 |
| 장수 라이프사이클 | [design-general-lifecycle.md](./design-general-lifecycle.md) | 위인 해금, 탐색, 등용, 함락/멸망 획득 |
| 내정 | [design-administration.md](./design-administration.md) | v2 담당자 4슬롯, 생산, 항구 경제 |
| 저장·장수 데이터 | [design-save-data.md](./design-save-data.md) | 20개 저장 슬롯, 오리진/프리셋/캠페인 상태 분리 |
| UI | [design-ui.md](./design-ui.md) | 명령 팔레트, 장수/부대/스킬 표시 |
| UI 개선 실행 | [plan-ui-improvement.md](./plan-ui-improvement.md) | 확인창·가로형 모달·스크롤·공통 컴포넌트·UI QA 우선순위 |
| 스킬 | [design-skill.md](./design-skill.md), [design-skill-actives.md](./design-skill-actives.md), [design-skill-passives.md](./design-skill-passives.md), [design-skill-admin.md](./design-skill-admin.md) | 액티브/패시브/내정 패시브 |
| 계략 전환 | [design-stratagem.md](./design-stratagem.md) | 독립 계략 폐기, 전투 액티브 전환 기준 |
| 외교 | [design-diplomacy-ruler-relations.md](./design-diplomacy-ruler-relations.md) | 동맹/동맹파기와 군주 관계도 |
| 지형·건물·항구 | [design-terrain.md](./design-terrain.md), [design-water.md](./design-water.md) | 지형 문자, 시설, 항구/물 규칙 |
| 지역 기후 | [design-climate.md](./design-climate.md) | Phase 21A 1차 구현·자동 QA 완료: 6×6 비·눈, 일별 갱신, 이동·공격 보정과 적설 표현. 실기기 연출 검수 대기 |
| 이펙트 | [design-effect.md](./design-effect.md) | 3D 효과 카탈로그 |
| 신수 사냥·보물 | [design-event-hunts.md](./design-event-hunts.md) | Phase 19~20 대호·용·봉황, 장착·소환 규칙 |
| 고대유물 단서 | [plan-ancient-relics.md](./plan-ancient-relics.md) | 10종 단서 2개 교환·유물 장착/효과의 현행 규칙 |
| 야전 건축 | [design-field-construction.md](./design-field-construction.md) | Phase 18C 목책·정찰대·감시탑·보루·진법 |
| AI 야전 건축 | [plan-phase21-ai-field-construction.md](./plan-phase21-ai-field-construction.md) | Phase 21 부분 구현·60~90일 주기 후보와 남은 장기 QA |
| 지도·시나리오 편집 | [plan-scenario-map-editor.md](./plan-scenario-map-editor.md), [plan-scenario-editor.md](./plan-scenario-editor.md), [plan-historical-scenarios.md](./plan-historical-scenarios.md) | 기본 지도·버전·역사 시나리오 6종의 현행 연결과 실제 창 검수 |
| 관문 | [plan-gate.md](./plan-gate.md) | 소/중형 관문 1차 구현과 남은 초기 배치·AI 통합 QA |
| 추가 장수 제작 | [plan-scenario-sub-generals.md](./plan-scenario-sub-generals.md) | 시나리오별 성 단위 후보와 중복 제거 작업표. 체크 표시는 사용자 편집 상태 |

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

2026-10-31 문서 점검: 저장소의 Markdown 78개를 목록화하고 상대 파일 링크를 검사했다(누락 0개). 현행 구현과 충돌한 지도·시나리오·AI 상태 안내를 위 진입 문서 및 관련 계획에 맞추었다. 오래된 `plan-01`~`plan-06`, QA 결과, 날짜가 붙은 구현 기록은 당시 상태를 보여주는 자료로 유지한다. 사용자 편집 중인 [추가 장수 체크표](./plan-scenario-sub-generals.md)의 완료 표시는 이번 정리에서 수정하지 않았다.

## 전체 문서 인벤토리

| 분류 | 문서 | 현재 용도 |
|---|---|---|
| 저장소 규칙 | [../CLAUDE.md](../CLAUDE.md), [ai-working-notes.md](./ai-working-notes.md) | 아키텍처·작업 방식·QA·머신 경로 |
| 진입점 | [../README.md](../README.md), [../ONBOARDING.md](../ONBOARDING.md), 이 문서 | 프로젝트 소개와 읽는 순서 |
| 실행 로드맵 | [plan-v2-implementation-roadmap.md](./plan-v2-implementation-roadmap.md), [plan-ui-improvement.md](./plan-ui-improvement.md) | 현재 기능 Phase와 UI 우선 작업 보드 |
| 상위/방향 기록 | [plan-roadmap.md](./plan-roadmap.md), [plan-combat-redesign-v2.md](./plan-combat-redesign-v2.md), [review-game-direction-2026-09.md](./review-game-direction-2026-09.md) | 전체 맥락·방향성·의사결정 기록 |
| 현행 설계 | [design-administration.md](./design-administration.md), [design-combat.md](./design-combat.md), [design-diplomacy-ruler-relations.md](./design-diplomacy-ruler-relations.md), [design-event-hunts.md](./design-event-hunts.md), [design-field-construction.md](./design-field-construction.md), [design-general-lifecycle.md](./design-general-lifecycle.md), [design-movement.md](./design-movement.md), [design-save-data.md](./design-save-data.md), [design-stratagem.md](./design-stratagem.md), [design-terrain.md](./design-terrain.md), [design-ui.md](./design-ui.md), [design-unit-state.md](./design-unit-state.md), [design-water.md](./design-water.md) | 분야별 확정 규칙. 이동은 Phase 18D 상세 계획 우선 |
| 스킬/효과 | [design-skill.md](./design-skill.md), [design-skill-actives.md](./design-skill-actives.md), [design-skill-passives.md](./design-skill-passives.md), [design-skill-admin.md](./design-skill-admin.md), [design-effect.md](./design-effect.md) | 액티브·패시브·내정 패시브와 3D 효과 |
| 확정 사양 | [spec-city.md](./spec-city.md), [spec-general.md](./spec-general.md), [spec-unit.md](./spec-unit.md) | 저장 구조·병종·도시·장수 사양 |
| 에셋/콘텐츠 | [asset-general-portraits.md](./asset-general-portraits.md), [asset-general-faction-groups.md](./asset-general-faction-groups.md), [asset-icon-generation.md](./asset-icon-generation.md), [previews/README.md](./previews/README.md) | 초상·그룹·아이콘·검수 이미지 규칙 |
| QA | [test/qa-checklist.md](./test/qa-checklist.md), [test/castle-entry-regression.md](./test/castle-entry-regression.md), [test/combat-movement-cases.md](./test/combat-movement-cases.md), [test/movement-cases.md](./test/movement-cases.md), [test/port-regression-qa.md](./test/port-regression-qa.md), [test/vision-cases.md](./test/vision-cases.md), [qa-renewal-campaign-12f.md](./qa-renewal-campaign-12f.md), [qa-field-building-attack-motion.md](./qa-field-building-attack-motion.md) | 수동·자동 회귀 시나리오와 검수 이력 |
| 역사 계획 | [plan-01-walking-skeleton.md](./plan-01-walking-skeleton.md), [plan-02-map-units.md](./plan-02-map-units.md), [plan-03-administration.md](./plan-03-administration.md), [plan-04-visual-polish.md](./plan-04-visual-polish.md), [plan-05-ui-shell.md](./plan-05-ui-shell.md), [plan-06-3d.md](./plan-06-3d.md), [plan-combat-focused-refactor.md](./plan-combat-focused-refactor.md) | 완료된 초기 단계와 폐기·전환 전 계획. 신규 구현 기준으로 사용하지 않음 |
