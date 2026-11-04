namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class OperationalPassiveRules
{
    public const string InfantryMobility = "infantry_mobility";
    public const string ArcherMobility = "archer_mobility";
    public const string CavalryMobility = "cavalry_mobility";
    public const string NavalMobility = "naval_mobility";
    public const string JuniorCommand = "junior_command";
    public const string SeniorCommand = "senior_command";

    public static int MovementPercent(TroopClass troopClass, params General?[] officers)
    {
        var code = troopClass switch
        {
            TroopClass.Infantry => InfantryMobility,
            TroopClass.Archer => ArcherMobility,
            TroopClass.Cavalry => CavalryMobility,
            TroopClass.Naval => NavalMobility,
            _ => "",
        };
        if (code.Length == 0) return 100;
        return 100 + officers.Where(officer => officer is not null)
            .SelectMany(officer => officer!.BattlePassives ?? [])
            .Where(skill => skill.Code == code)
            .Sum(skill => 5 * Math.Clamp(skill.Tier, 1, 3));
    }

    public static int CommandBonus(params General?[] officers)
        => officers.Where(officer => officer is not null)
            .SelectMany(officer => officer!.BattlePassives ?? [])
            .Sum(skill => skill.Code switch
            {
                JuniorCommand => 500 * Math.Clamp(skill.Tier, 1, 3),
                SeniorCommand => 1000 * Math.Clamp(skill.Tier, 1, 3),
                _ => 0,
            });
}
