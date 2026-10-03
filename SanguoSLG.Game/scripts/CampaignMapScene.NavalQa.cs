namespace SanguoSLG.Game;

using Godot;
using System;
using System.Linq;
using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private async void RunNavalComposeQa()
    {
        try
        {
            var port = _state.Cities.First(c => c.IsPort && c.Owner == Player);
            _state = _state with { PortShipStocks = _state.PortShips.Where(s => s.City != port.Id)
                .Concat(PortShipOptions().Select(s => new PortShipStock(port.Id, s.Code, 1))).ToList() };
            OpenNavalCompose(port.Id);
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var columns = _modalLayer!.FindChildren("DeployComposeColumns", "HBoxContainer", true, false).OfType<HBoxContainer>().Single();
            var resources = columns.FindChildren("ComposeResources", "VBoxContainer", true, false).OfType<VBoxContainer>().Single();
            var officers = columns.FindChildren("ComposeOfficers", "VBoxContainer", true, false).OfType<VBoxContainer>().Single();
            var table = officers.FindChildren("*", "Tree", true, false).OfType<Tree>().Single();
            var first = table.GetRoot().GetFirstChild();
            if (first is null || !first.IsEditable(0) || !first.IsEditable(4)) throw new Exception("선봉/부관 선택 셀 누락");
            if (table.GetColumnTitle(0) != "선봉" || table.GetColumnTitle(4) != "부관") throw new Exception("장수 역할 표기 불일치");
            if (table.Size.Y < officers.Size.Y * 0.9f) throw new Exception($"장수 표 높이 부족 {table.Size.Y}/{officers.Size.Y}");
            var ships = resources.GetNode<Tree>("NavalShipTable");
            var troops = resources.GetNode<Tree>("NavalTroopTable");
            if (ships.GetRoot().GetChildCount() != 5) throw new Exception("5종 선박 QA 데이터 누락");
            foreach (var list in new[] { ships, troops })
            {
                var rows = list.GetRoot().GetChildCount();
                var last = list.GetRoot().GetChild(rows - 1);
                GD.Print($"NAVAL_TABLE {list.Name} size={list.Size} bars={string.Join(';', list.GetChildren(true).OfType<ScrollBar>().Select(b => $"{b.Name}:{b.Visible}:{b.MaxValue}/{b.Page}"))}");
                if (rows <= 5 && (list.GetChildren(true).OfType<VScrollBar>().Any(b => b.Visible && b.MaxValue > b.Page)
                    || list.GetItemAreaRect(last).End.Y > list.Size.Y - 2))
                    throw new Exception($"소수 목록 잘림/스크롤: {list.Name} rows={rows} end={list.GetItemAreaRect(last).End.Y} height={list.Size.Y}");
            }
            var estimate = resources.GetNode<Label>("NavalFoodEstimate");
            if (!estimate.Text.Contains("항해 기준") || !estimate.Text.Contains("육상 기준")) throw new Exception("군량 비교 누락");
            if (NavalFoodEstimate(10000, 100, 100) != "항해 기준 약 50일 (하루 2) · 육상 기준 약 10일 (하루 10)")
                throw new Exception("군량 예상일수 불일치");
            var panel = _modalLayer.GetChildren().OfType<PanelContainer>().Single();
            if (panel.GetGlobalRect().End.Y > GetViewport().GetVisibleRect().End.Y) throw new Exception("모달 화면 하단 넘침");
            if (resources.FindChildren("*", "HSlider", true, false).Count != 3) throw new Exception("병력/군량/금 슬라이더 누락");
            foreach (var pane in new[] { resources, officers })
                if (pane.GetCombinedMinimumSize().Y > columns.Size.Y + 2) throw new Exception($"편성 영역 세로 넘침: {pane.Name} {pane.GetCombinedMinimumSize().Y}/{columns.Size.Y}");
            GD.Print("NAVAL_COMPOSE_QA PASS: 5 ships without scroll, troop rows, officers >=90%, food duration, screen bounds");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError("NAVAL_COMPOSE_QA FAIL: " + e);
            GetTree().Quit(1);
        }
    }
}
