namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 건설한 시설이 놓인 성 주변 타일(2026-08-27). 도시는 시설을 개수로만 갖고(City 불변식 —
/// 컬렉션을 넣으면 record 값 동등성이 깨진다), 타일 위치는 여기 GameState 목록으로 따로 둔다.
/// 사용자가 건설 시 지정한 칸을 기록해 표현 계층이 그 자리에 모델을 얹는다.
///
/// append-only: 건설 완료 시 하나 추가되고, 약탈·수리로는 제거하지 않는다. 표현 계층이 성별·시설별로
/// 온전 개수(City.Paddies 등)/잔해 개수(City.RuinedPaddies 등)에 맞춰 앞에서부터 온전/잔해로 나눠
/// 그린다 — 그래서 약탈·수리를 이 목록에 배선하지 않아도 화면이 어긋나지 않는다.
/// </summary>
public sealed record FacilityPlacement(CityId City, HexCoord Plot, string Code, int HitPoints = FacilityHealth.Level1);
