# 성 입성·화면 재생 회귀 QA (2026-09-13)

## 원인과 수정

- 이동 시뮬레이터가 입성 부대를 스냅샷에서 먼저 제거해 마지막 이동 좌표가 화면에 전달되지 않았다.
- 화면은 입성 부대도 전투 정산 시점에 제거했다. 다른 부대가 계속 움직이면 입성 후에도 대기하는 모습이 생겼다.
- 입성 틱의 `EnteredUnits`에 마지막 위치를 보존하고, 실제 화면에서 사용하는 `MovementPlayback`이 마지막 이동 완료 직후 제거 시각을 계산한다.
- 캠페인의 중복 거리 판정은 제거했다. 경유지·추격·행동불가를 무시한 조기 입성을 막고 이동 시뮬레이터의 입성 사건만 정산한다.

## 자동 검증

| 범위 | 결과 |
|---|---|
| 거리 4, 이동력 2 | 1일차 2칸, 2일차 1칸 이동을 모두 기록하고 마지막 이동 완료 직후 입성 |
| 다른 부대가 7일 내내 이동 | 입성 부대의 제거 시점이 7일차까지 밀리지 않음 |
| 진행 조각 분리 | 날짜 오프셋을 반영하며 이동 누락·중복 제거 없음 |
| 적 성·목표 없음·경유지·행동불가 | 부적절한 입성 없음 |
| 전투·보급·수송 × 거리 2~10 × 이동력 1~3 | Godot에서 실제 CampaignEngine → CampaignMapScene.BuildAnimation 경로 81건 통과 |
| 모든 이동 좌표 | 인접 칸으로만 이동, 마지막 이동 누락 없음 |
| 입성 제거·해골 | 입성은 한 번만 제거하고 해골 효과 없음 |
| 전체 xUnit | 저장소 HEAD 데이터로 격리 실행 시 729/729 통과 |
| 현재 로컬 데이터 | 728/729 통과. 기존 사용자 편집 데이터의 을지문덕 수성 S 초과 검사 1건 실패. 데이터는 보존 |
| Game 빌드·Godot build-solutions | 통과 |
| maptest 헤드리스 로딩 | 통과 |

검증은 이동 좌표와 실제 화면 재생 함수의 예약 시각까지 자동으로 확인했다. 렌더링된 게임 화면을 육안으로 확인했다는 의미는 아니다.

## 재실행

```powershell
dotnet test SanguoSLG.Core.Tests/SanguoSLG.Core.Tests.csproj --filter CastleEntryPlaybackTests
dotnet build SanguoSLG.Game/SanguoSLG.Game.csproj
& "D:\LOCAL-WORK-STATION\Godot_v4.7.2-stable_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path SanguoSLG.Game res://scenes/CastleEntryQa.tscn
```

마지막 명령은 `CASTLE_ENTRY_QA PASS: 81 campaign/animation cases`와 종료 코드 0을 함께 확인한다.
