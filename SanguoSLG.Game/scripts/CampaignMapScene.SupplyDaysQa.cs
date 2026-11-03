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
        _state = _state with { Cities = _state.Cities.Select(c => c.Id == city.Id
            ? c with { Provisions = System.Math.Max(c.Provisions, 2100) } : c).ToArray() };
        _supplyDraft["swordsman"] = 20_000;
        _depProvDays = 120;
        var carry = SupplyProvisionsToCarry();
        var sample = unit with { Pool = new TroopPool(20_000, 0), ProvisionsCapacity = 210 };
        var capacityMatches = SupplyCapacityForDraft() == sample.MaxProvisions()
            && SupplyCapacityForDraft() == 2100;
        var targetDaily = 20_000 * _provPer10kPerDay * _balance.SupplyConservationPercent / 1_000_000d;
        var demand = (int)System.Math.Ceiling((SupplyDraftDailyConsumption() + targetDaily) * 60);
        var passed = planned == deployed && planned >= 9 && planned <= 11
            && capacityMatches && carry == 1680 && demand == carry;
        GD.Print($"[supply-days-qa] passed={passed} planned={planned} deployed={deployed} capacity={SupplyCapacityForDraft()} carry={carry} demand={demand}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
