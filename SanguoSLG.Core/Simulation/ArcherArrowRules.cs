namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public enum ArcherArrowMode { Normal, Fire }

public static class ArcherArrowRules
{
    public static int DamagePercent(ArcherArrowMode mode, TroopClass attackerClass,
        TerrainType targetTerrain, string targetTroopCode, TroopClass targetClass,
        bool raining)
    {
        if (mode != ArcherArrowMode.Fire || attackerClass != TroopClass.Archer) return 100;
        if (raining) return 10;
        var percent = targetTerrain is TerrainType.Forest or TerrainType.Mountain ? 110 : 90;
        if (targetTroopCode == "deunggap") percent += 60;
        if (targetClass == TroopClass.Cavalry) percent -= 10;
        return percent;
    }
}
