namespace SanguoSLG.Game;

using Godot;
using System;
using System.Linq;

public sealed partial class CampaignMapScene
{
    private async void RunNavalComposeQa()
    {
        try
        {
            var port = _state.Cities.First(c => c.IsPort && c.Owner == Player);
            OpenNavalCompose(port.Id);
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var columns = _modalLayer!.FindChildren("DeployComposeColumns", "HBoxContainer", true, false).OfType<HBoxContainer>().Single();
            var resources = columns.FindChildren("ComposeResources", "VBoxContainer", true, false).OfType<VBoxContainer>().Single();
            var officers = columns.FindChildren("ComposeOfficers", "VBoxContainer", true, false).OfType<VBoxContainer>().Single();
            var table = officers.FindChildren("*", "Tree", true, false).OfType<Tree>().Single();
            var first = table.GetRoot().GetFirstChild();
            if (first is null || !first.IsEditable(0) || !first.IsEditable(4)) throw new Exception("선봉/부관 선택 셀 누락");
            if (resources.FindChildren("*", "HSlider", true, false).Count != 3) throw new Exception("병력/군량/금 슬라이더 누락");
            foreach (var pane in new[] { resources, officers })
                if (pane.GetCombinedMinimumSize().Y > columns.Size.Y + 2) throw new Exception($"편성 영역 세로 넘침: {pane.Name} {pane.GetCombinedMinimumSize().Y}/{columns.Size.Y}");
            GD.Print("NAVAL_COMPOSE_QA PASS: horizontal layout, resources, editable officers, bounded content");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError("NAVAL_COMPOSE_QA FAIL: " + e);
            GetTree().Quit(1);
        }
    }
}
