namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 이동 결과. 이동 후 부대 상태와, 지나간 경로(start·goal 포함)를 담는다.
/// 경로는 Game이 이동 애니메이션에 사용한다.
/// </summary>
public sealed record MoveResult(Unit Unit, IReadOnlyList<HexCoord> Path)
{
    /// <summary>실제로 이동(또는 제자리 경로)이 가능했는가. 도달 불가면 false.</summary>
    public bool Moved => Path.Count > 0;
}
