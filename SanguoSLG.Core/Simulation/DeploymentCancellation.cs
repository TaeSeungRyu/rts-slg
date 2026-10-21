namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class DeploymentCancellation
{
    public static CommandResult Cancel(GameState state, FactionId faction, UnitId unitId)
    {
        var unit = state.Armies.FirstOrDefault(u => u.Id == unitId && u.Field.Owner == faction);
        if (unit is null || !unit.IsWaitingDeployment || unit.OriginCity is not { } cityId)
            return CommandResult.Fail("출격 대기 중인 아군 부대만 취소할 수 있습니다.", state);
        var city = state.Cities.FirstOrDefault(c => c.Id == cityId && c.Owner == faction);
        if (city is null)
            return CommandResult.Fail("출발지가 더 이상 아군 거점이 아닙니다.", state);
        if (unit.Class == TroopClass.Naval)
            return CommandResult.Fail("출항 대기 취소는 선박과 승선 병력의 원상 복구가 필요합니다.", state);

        var garrisons = state.Garrisons.ToList();
        var incoming = unit.Cargo.Count > 0
            ? unit.Cargo.Select(c => (c.TroopCode, c.Troops, c.TrainingLevel))
            : new[] { (unit.TroopCode, unit.Pool.Active, unit.Training) };
        foreach (var (code, troops, training) in incoming)
        {
            if (troops <= 0 || string.IsNullOrEmpty(code)) continue;
            var trainee = training < 50;
            var index = garrisons.FindIndex(g => g.City == cityId && g.TroopCode == code
                && g.Trainee == trainee);
            if (index >= 0) garrisons[index] = garrisons[index].Merge(troops, training);
            else garrisons.Add(new GarrisonForce(cityId, code, troops, training, trainee));
        }
        var postings = state.Assignments.ToList();
        foreach (var general in new[] { unit.VanguardId, unit.AdjutantId }.OfType<GeneralId>().Distinct())
        {
            postings.RemoveAll(p => p.General == general);
            postings.Add(new GeneralPosting(general, faction, cityId));
        }
        return CommandResult.Success(state with
        {
            Cities = state.Cities.Select(c => c.Id == cityId ? c with
            {
                Gold = c.Gold + unit.CarryingGold,
                Provisions = c.Provisions + Math.Max(0, unit.Provisions),
            } : c).ToList(),
            GarrisonForces = garrisons,
            Postings = postings,
            FieldArmies = state.Armies.Where(u => u.Id != unitId).ToList(),
        });
    }
}
