# 동맹 간 공격 금지 점검 (2026-10-15)

## 캠페인 적용 결과

동맹 관계는 `GameState.AreHostile`로 판정한다. 동맹 대상의 새 공격 명령은 거부하고, 동맹 체결 시 이미 예약·진행 중이던 공격 명령은 대기로 바꾼다. 동맹 파기 후에는 자동 복원하지 않는다.

| 대상 | 적용 경로 | 검증 |
|---|---|---|
| 공격 명령과 UI | `FieldUnitCommandService`, `CampaignMapScene` | 부대·야전 건축물·성 대상 명령 테스트, Game 빌드 |
| 연속 이동 추격·충돌 | `RenewalAdvanceSimulator`, `RenewalCampaignAdvanceRunner` | 동맹 충돌 테스트 |
| 기존 이동·야전 공격·반격·액티브 | `MovementSimulator`, `CombatPhase`, `AdvanceOrchestrator` | 동맹 교전·병력 무손실 테스트 |
| 야전 건축물·정찰대 | `FieldBuildingCombat`, `FieldScoutPostService` | 보루 공격·정찰대 통과 테스트 |
| 성·항구 공격·점령 | `CampaignSiege`, `CityCapture`, `CityPlunder` | 성·항구 공성·함락 방지 테스트 |
| 생산·공사장 | `WorldEngine`, `CampaignEngine` | 동맹 부대의 생산 방해 방지 테스트 |
| AI·계략·소환 | `FactionAI`, `FieldStratagemService`, `SummonService`, `CommandService` | 외교·도시 계략·야전 계략 테스트 |

독립 이동 검수장(`RenewalCombatPhaseService`, `RenewalIntegrationService`, `RenewalMovementAiPlanner`)은 `GameState`의 동맹 목록을 사용하지 않는 테스트 전용 경로다. 이 경로의 임의 소유 세력은 기존처럼 서로 다른 세력이면 적으로 간주한다. 실제 캠페인 기본 경로는 `RenewalCampaignAdvanceRunner`와 `AdvanceOrchestrator`를 사용하므로 위 판정이 적용된다.

시각 검증은 자동 수행하지 않았다. `run-maptest.bat`에서 동맹 전·후의 부대 공격 버튼, 성·항구 공성, 보루 공격, 동맹 체결 직후 부대 대기 전환을 확인한다.
