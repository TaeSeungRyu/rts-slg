using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using System.IO;
using System.Linq;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void SeedMapTestOfficers()
    {
        var fixture = new PostingLoader().LoadFromJson(File.ReadAllText(Path.Combine(_dataDirectory, "maptest-postings.json")));
        if (fixture.Select(posting => posting.General).Distinct().Count() != fixture.Count
            || fixture.Any(posting => !_state.Generals.Any(general => general.Id == posting.General)
                || !_state.Cities.Any(city => city.Id == posting.Location && city.Owner == posting.Faction)))
            throw new InvalidDataException("테스트 장수 배치가 중복되거나 장수/도시를 찾을 수 없습니다.");
        var existing = _state.Assignments.Select(posting => posting.General).ToHashSet();
        var additions = fixture.Where(posting => !existing.Contains(posting.General)).ToList();
        var added = additions.Select(posting => posting.General).ToHashSet();
        _state = _state with
        {
            Postings = _state.Assignments.Concat(additions).ToList(),
            HeroUnlockStates = _state.HeroStates.Select(hero => added.Contains(hero.General)
                ? hero with { Status = HeroUnlockStatus.Recruited, EligibleFaction = Player, UpdatedDay = _state.Day }
                : hero).ToList(),
        };
    }

    private void RunMapTestOfficerFixtureQa()
    {
        var originalsPreserved = _initial.Assignments.All(original => _state.Assignments.Contains(original));
        var passed = _state.Assignments.Select(posting => posting.General).Distinct().Count() == _state.Assignments.Count
            && originalsPreserved;
        foreach (var city in _state.Cities.Where(city => city.Owner == Player && !city.IsPort))
        {
            var officers = _state.GeneralsAt(city.Id).ToList();
            var free = officers.Count(id => !_state.IsGeneralBusy(id));
            var local = officers.Count >= 12 && free >= 12;
            passed &= local;
            GD.Print($"[maptestofficerfixtureqa] city={city.Name} total={officers.Count} free={free} passed={local}");
        }
        var fixture = new PostingLoader().LoadFromJson(File.ReadAllText(Path.Combine(_dataDirectory, "maptest-postings.json")));
        passed &= _state.HeroStates.Where(hero => fixture.Any(posting => posting.General == hero.General))
            .All(hero => hero.Status == HeroUnlockStatus.Recruited);
        var before = _state.Assignments.Count;
        SeedMapTestOfficers();
        passed &= before == _state.Assignments.Count;
        GD.Print($"[maptestofficerfixtureqa] passed={passed} postings={_state.Assignments.Count} originalsPreserved={originalsPreserved} idempotent={before == _state.Assignments.Count}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
