# 개발 계획 06 — 3D 전환

> 사용자 결정(2026-08-04): 카툰 2D의 시각적 한계를 넘기 위해 **Game 표현층을 Godot 3D로 전환**.
> CLAUDE.md "실시간 3D 아님" 원칙을 뒤집는 결정(문서 갱신 완료). **Core는 불변** — 헥사 좌표·A*·시뮬레이션은 2D/3D 무관.

## 확정된 설계 결정

- **3D 렌더링**: Kenney Hexagon Kit(CC0 GLB) 3D 헥사 타일 + Camera3D 쿼터뷰 + DirectionalLight3D 그림자 + 3D 환경(하늘 앰비언트·톤맵·글로우·SSAO)
- **Core 재사용**: `HexMap`/`HexPathfinder`/`MovementService`/`GameState`/`TurnEngine`/시나리오 로딩 그대로. axial↔월드(x-z) 변환은 `MapView3D`에서만
- **HUD 재사용**: `Hud`(CanvasLayer)는 3D 위에 그대로 동작
- 지형 데이터(map.json)·`TerrainType` 그대로 사용, 3D 타일로 렌더

## 단계 (각 단계 스크린샷 확인 🔍)

- [x] Step 1 — 3D 맵 렌더 골격 🔍: 지형 GLB를 헥사 좌표에 배치, 카메라·조명·환경, HUD 오버레이. (타일 크기·방향 AABB 자동 측정)
- [ ] Step 2 — 카메라 프레이밍·컨트롤 🔍: 맵을 화면에 알맞게, 팬/줌/회전
- [ ] Step 3 — 도시·유닛 3D 🔍: 도시 마커(+한글 라벨 Label3D), 유닛 3D 토큰
- [ ] Step 4 — 3D 클릭 이동 🔍: 카메라 레이캐스트 → 지면 → 헥사, A* 경로 이동 애니메이션
- [ ] Step 5 — 조명·그림자·환경 폴리시 🔍: 그림자 품질, 톤·앰비언트, 물/강 타일
- [ ] Step 6 — 마무리 + 회고

## 남은 참고

- 물/강/다리 GLB, 건물·유닛 모델도 킷에 있음(필요 시 도입)
- 구 2D 씬(Main.tscn)은 당분간 보존(참고용), 3D 안정화 후 정리
