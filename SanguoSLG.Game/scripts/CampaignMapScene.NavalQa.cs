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
            var allLandTroops = _troops.Where(t => t.Class != SanguoSLG.Core.Domain.TroopClass.Naval)
                .Select(t => t.Code).ToHashSet();
            var mediumPortTroops = _state.Garrisons.Where(g => g.City == new SanguoSLG.Core.Domain.CityId(9))
                .Select(g => g.TroopCode).ToHashSet();
            if (!allLandTroops.IsSubsetOf(mediumPortTroops))
                throw new Exception("위중항에 빠진 병종: " + string.Join(", ", allLandTroops.Except(mediumPortTroops)));
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
            if (first is null || !first.IsEditable(0) || !first.IsEditable(1)) throw new Exception("선봉/부관 선택 셀 누락");
            if (table.GetColumnTitle(0) != "선봉" || table.GetColumnTitle(1) != "부관") throw new Exception("장수 역할 표기 불일치");
            if (table.Columns != 8 || table.GetColumnTitle(2) != "이름" || table.GetColumnTitle(6) != "적성")
                throw new Exception("전투편성형 장수 표 구조 불일치");
            if (table.Size.Y < officers.Size.Y * 0.9f) throw new Exception($"장수 표 높이 부족 {table.Size.Y}/{officers.Size.Y}");
            var ships = resources.GetNode<Tree>("NavalShipTable");
            var troops = resources.GetNode<Tree>("NavalTroopTable");
            var initialShipSize = ships.Size;
            var initialTroopSize = troops.Size;
            var initialOfficerSize = table.Size;
            var initialShipPosition = ships.GetGlobalRect().Position;
            var initialTroopPosition = troops.GetGlobalRect().Position;
            var initialOfficerPosition = table.GetGlobalRect().Position;
            foreach (var target in new[] { ships, troops, table, ships, troops, table })
            {
                var row = target.GetRoot().GetFirstChild();
                var rowCenter = row is null ? target.GetGlobalRect().GetCenter()
                    : target.GetGlobalPosition() + target.GetItemAreaRect(row).GetCenter();
                GetViewport().PushInput(new InputEventMouseMotion { Position = rowCenter });
                for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!ships.Size.IsEqualApprox(initialShipSize) || !troops.Size.IsEqualApprox(initialTroopSize)
                    || !table.Size.IsEqualApprox(initialOfficerSize)
                    || !ships.GetGlobalRect().Position.IsEqualApprox(initialShipPosition)
                    || !troops.GetGlobalRect().Position.IsEqualApprox(initialTroopPosition)
                    || !table.GetGlobalRect().Position.IsEqualApprox(initialOfficerPosition))
                    throw new Exception($"마우스 오버 후 표 위치·높이 변경: 선박 {ships.GetGlobalRect()}, 병력 {troops.GetGlobalRect()}, 장수 {table.GetGlobalRect()}");
            }
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!ships.Size.IsEqualApprox(initialShipSize) || !troops.Size.IsEqualApprox(initialTroopSize))
                throw new Exception($"좌측 표 레이아웃 변동 ship={initialShipSize}->{ships.Size}, troop={initialTroopSize}->{troops.Size}");
            var headings = resources.GetChildren().OfType<Label>().Select(label => label.Text).ToHashSet();
            if (!headings.Contains("1. 선박") || !headings.Contains("2. 승선 병력") || !headings.Contains("3. 병력 물자"))
                throw new Exception("출항 편성 단계 제목 누락");
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
            var save = _modalLayer.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "출항 예약");
            if (!panel.GetGlobalRect().Encloses(save.GetGlobalRect())) throw new Exception("출항 예약 버튼 모달 밖으로 넘침");
            if (save.GetGlobalRect().End.Y > GetViewport().GetVisibleRect().End.Y - 8) throw new Exception("출항 예약 버튼 화면 하단 넘침");
            OpenNavalCompose(new SanguoSLG.Core.Domain.CityId(9));
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var mediumTables = _modalLayer!.FindChildren("*", "Tree", true, false).OfType<Tree>().ToList();
            var mediumTroops = mediumTables.Single(t => t.Name == "NavalTroopTable");
            var mediumOfficers = mediumTables.Single(t => t.Name == "NavalOfficerTable");
            if (mediumTroops.GetRoot().GetChildCount() != allLandTroops.Count)
                throw new Exception("위중항 병종 표에 전체 지상 병종이 나오지 않음");
            var troopHeight = mediumTroops.Size.Y;
            var officerHeight = mediumOfficers.Size.Y;
            foreach (var target in new[] { mediumTroops, mediumOfficers, mediumTroops, mediumOfficers })
            {
                GetViewport().PushInput(new InputEventMouseMotion { Position = target.GetGlobalRect().GetCenter() });
                for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!Mathf.IsEqualApprox(mediumTroops.Size.Y, troopHeight)
                    || !Mathf.IsEqualApprox(mediumOfficers.Size.Y, officerHeight))
                    throw new Exception("위중항 표가 마우스 오버 후 높이를 변경함");
            }
            GD.Print("NAVAL_COMPOSE_QA PASS: stable tables, numbered sections, combat-style officer checks, 5 ships without scroll, food duration, screen bounds");
            GetParent().CallDeferred("RequestGracefulShutdown");
        }
        catch (Exception e)
        {
            GD.PushError("NAVAL_COMPOSE_QA FAIL: " + e);
            GetTree().Quit(1);
        }
    }
}
