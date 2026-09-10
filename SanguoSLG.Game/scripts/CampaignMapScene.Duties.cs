namespace SanguoSLG.Game;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Domain;

public sealed partial class CampaignMapScene
{
    private bool OfficerUnavailable(GeneralId id) => _state.IsGeneralBusy(id)
        || _pendingDeploys.Any(p => p.Req.Vanguard == id || p.Req.Adjutant == id)
        || _pendingSupplyDeploys.Any(p => p.Req.Vanguard == id);

    private string CurrentDuty(GeneralId id)
    {
        var duties = new List<string>();
        foreach (var c in _state.Cities)
        {
            if (c.Governor == id) { duties.Add($"{c.Name} 태수(겸임)"); }
            if (c.Strategist == id) { duties.Add($"{c.Name} 군사(겸임)"); }
        }
        if (CurrentOfficerAssignment(id) is { } role) { duties.Add($"{role.CityName} {KindName(role.Kind)}"); }
        foreach (var command in _state.Commands.Where(c => c.Locks(id))) { duties.Add(KindName(command.Kind)); }
        if (_state.ProductionOps.Any(p => p.General == id)) { duties.Add("생산(이동·채집·복귀)"); }
        var unit = _state.Armies.FirstOrDefault(u => u.VanguardId == id || u.AdjutantId == id);
        if (unit is not null) { duties.Add(unit.IsSupply ? "보급부대 출전" : "출전"); }
        if (_pendingDeploys.Any(p => p.Req.Vanguard == id || p.Req.Adjutant == id)) { duties.Add("출전 예약"); }
        if (_pendingSupplyDeploys.Any(p => p.Req.Vanguard == id)) { duties.Add("보급부대 예약"); }
        return duties.Count == 0 ? "없음" : string.Join(" / ", duties);
    }

    private string DutyReleaseNotice(params GeneralId[] ids)
    {
        var lines = ids.Distinct().Select(id => (Id: id, Role: CurrentOfficerAssignment(id)))
            .Where(x => x.Role is not null)
            .Select(x => $"{_state.Generals.First(g => g.Id == x.Id).Name}: {x.Role!.Value.CityName} {KindName(x.Role.Value.Kind)} 해제");
        var text = string.Join("\n", lines);
        return text.Length == 0 ? "" : $"\n\n기존 업무에서 빠집니다:\n{text}\n태수·군사 직책은 유지됩니다. 담당 업무는 자동 복귀하지 않습니다.";
    }
}
