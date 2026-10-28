namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class GateService
{
    public static CommandResult DismissCommander(GameState state, FactionId faction, CityId gateId)
    {
        var gate = state.Cities.FirstOrDefault(city => city.Id == gateId && city.IsGate);
        if (gate is null || gate.Owner != faction)
            return CommandResult.Fail("자기 세력의 관문만 관리할 수 있습니다.", state);
        if (gate.Governor is null)
            return CommandResult.Fail("임명된 관문장이 없습니다.", state);
        return CommandResult.Success(state with
        {
            Cities = state.Cities.Select(city => city.Id == gateId
                ? city with { Governor = null } : city).ToArray(),
        });
    }
}
