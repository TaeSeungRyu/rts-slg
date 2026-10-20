using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void RunSupplyDaysQa()
    {
        var city = _state.Cities.First(candidate => candidate.Owner == Player);
        _depModalCity = city.Id;
        _supplyDraft.Clear();
        _supplyDraft["swordsman"] = 10_000;
        var provisions = _provPer10kPerDay * 7;
        var field = new FieldUnit(new UnitId(99021), Player, new HexCoord(1000, 1000),
            2, 2, 1, MovementDomain.Land, UnitMode.Standby, null, 99021, 1);
        var unit = new CombatUnit(field, new CombatStats(10_000, 8, 8), new TroopPool(10_000, 0),
            UnitCombatState.Create(60), Provisions: provisions, IsSupply: true, TroopCode: "swordsman");
        _state = _state with { FieldArmies = _state.Armies.Append(unit).ToList() };
        var planned = SupplyProvisionDaysFromAmount(provisions);
        var deployed = EstimateCarriedProvisionDays(unit);
        var passed = planned == deployed && planned >= 9 && planned <= 11;
        GD.Print($"[supply-days-qa] passed={passed} planned={planned} deployed={deployed} provisions={provisions}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
