namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 지형 공방 보정(design-combat.md "지형 공방 보정"). 분류 단위 flat `+2`류로 스탯에 더해진다(②).
/// 조건이 맞지 않으면 (0, 0).
/// </summary>
public static class TerrainCombatBonus
{
    /// <summary>(분류, 지형) → (공격 flat, 방어 flat).</summary>
    public static (int Atk, int Df) For(TroopClass cls, TerrainType terrain) => (cls, terrain) switch
    {
        (TroopClass.Archer, TerrainType.Forest) => (2, 2),
        (TroopClass.Cavalry, TerrainType.Plains) => (2, 0),
        (TroopClass.Cavalry, TerrainType.Desert) => (2, 0),
        (TroopClass.Cavalry, TerrainType.DesertCactus) => (2, 0),
        (TroopClass.Infantry, TerrainType.Mountain) => (2, 0),
        _ => (0, 0),
    };
}
