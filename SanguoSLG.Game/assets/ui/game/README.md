# 메인 게임 UI 이미지 교체 규격

이미지가 없으면 `GameUiSkin`의 검붉은색·금색 기본 스타일로 안전하게 표시된다. 아래 PNG를 추가하면 코드 변경 없이 이미지 기반 UI로 전환된다.

| 경로 | 권장 크기 | 용도 |
| --- | ---: | --- |
| `panels/ruler_hud.png` | 512×128 | 좌상단 군주 HUD, 28px 9-slice |
| `frames/ruler_portrait.png` | 128×128 | 원형 군주 초상 금속 프레임, 24px 9-slice |
| `buttons/hud_round_normal.png` | 96×96 | 시스템·지도 버튼 기본 상태 |
| `buttons/hud_round_hover.png` | 96×96 | 시스템·지도 버튼 Hover |
| `buttons/hud_round_pressed.png` | 96×96 | 시스템·지도 버튼 Pressed |
| `buttons/hud_round_disabled.png` | 96×96 | 시스템·지도 버튼 Disabled |
| `icons/system.png` | 48×48 | 시스템 버튼 아이콘(선택) |
| `icons/map.png` | 48×48 | 지도 버튼 아이콘(선택) |

패널·프레임 PNG는 중앙이 투명하지 않은 검붉은 반투명 바탕이어야 하며, 장식은 9-slice 경계 안쪽에 배치한다. 버튼 상태 이미지는 캔버스 크기와 투명 여백을 모두 동일하게 유지한다.
