namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Domain;

public sealed partial class CampaignMapScene
{
    private void RunGeneralRosterFilterQa()
    {
        OpenGeneralRosterFresh();
        var filter = _modalLayer?.FindChild("GeneralRosterFactionFilter", true, false) as OptionButton;
        var tree = _modalLayer?.FindChild("GeneralRosterPageTree", true, false) as Tree;
        bool Matches(System.Func<GeneralId, bool> predicate)
            => tree?.GetRoot()?.GetChildren().OfType<TreeItem>().All(item =>
                predicate(new GeneralId(item.GetMetadata(0).AsInt32()))) == true;
        var options = filter is not null && filter.ItemCount == 3
            && filter.GetItemText(0) == "전체" && filter.GetItemText(1) == "아군"
            && filter.GetItemText(2) == "미발견";
        filter?.Select(1);
        filter?.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
        var friendly = Matches(id => _state.PostingOf(id)?.Faction == Player);
        filter?.Select(2);
        filter?.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
        var locked = Matches(id => _state.HeroStates.Any(state =>
            state.General == id && state.Status == HeroUnlockStatus.Locked));
        var passed = options && friendly && locked && _generalRosterPage == 0;
        GD.Print($"[general-roster-filter-qa] passed={passed} options={options} friendly={friendly} locked={locked}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
