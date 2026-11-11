using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private GridContainer BuildCityOfficerGrid(City city)
    {
        var grid = new GridContainer { Name = "CityOfficerGrid", Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        foreach (var (role, officerId) in new[] { ("태수", city.Governor), ("군사", city.Strategist) })
        {
            var frame = new PanelContainer { Name = "CityOfficerCard",
                CustomMinimumSize = new Vector2(0, 60),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            // 배경 PNG가 태수·군사 칸의 테두리를 이미 제공하므로 중복 프레임은 그리지 않는다.
            frame.AddThemeStyleboxOverride("panel", new StyleBoxEmpty
            {
                ContentMarginLeft = 8,
                ContentMarginTop = 5,
                ContentMarginRight = 8,
                ContentMarginBottom = 5,
            });
            frame.SetMeta("city_officer_card", true);
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
            row.AddThemeConstantOverride("separation", 8);
            frame.AddChild(row);
            var portraitCell = new CenterContainer { CustomMinimumSize = new Vector2(48, 48),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            var picture = new TextureRect { Name = "CityOfficerPortrait",
                Texture = officerId is { } id ? CircularPortraitFor(id) : null,
                CustomMinimumSize = new Vector2(48, 48),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize };
            portraitCell.AddChild(picture);
            row.AddChild(portraitCell);
            var stack = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            stack.AddThemeConstantOverride("separation", 3);
            row.AddChild(stack);
            var name = officerId is { } nameId
                ? _state.Generals.FirstOrDefault(general => general.Id == nameId)?.Name ?? "없음"
                : "없음";
            foreach (var (caption, size, color) in new[]
                { (role, 11, GoldBright), (name, 14, Parchment) })
            {
                var label = MakeLabel(caption, size, color);
                label.CustomMinimumSize = new Vector2(0, caption == role ? 18 : 22);
                label.HorizontalAlignment = HorizontalAlignment.Left;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                stack.AddChild(label);
            }
            grid.AddChild(frame);
        }
        return grid;
    }

    private void RunCityOfficerGridQa()
    {
        var city = _state.Cities.First(candidate => candidate.Owner == Player);
        var generals = _state.Generals.Take(2).ToArray();
        if (generals.Length < 2) { GetTree().Quit(1); return; }
        _state = _state with { Cities = _state.Cities.Select(candidate => candidate.Id == city.Id
            ? candidate with { Governor = generals[0].Id, Strategist = generals[1].Id }
            : candidate).ToList() };
        SelectCity(city.Id);
        var grid = _infoRows.FindChild("CityOfficerGrid", true, false) as GridContainer;
        var divider = _infoRows.FindChild("CityResourceDivider", true, false) as MarginContainer;
        var scroll = _infoCard.FindChild("CityInfoScroll", true, false) as ScrollContainer;
        var actions = _infoCard.FindChild("CityInfoActions", true, false) as VBoxContainer;
        var labels = grid?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).ToList() ?? [];
        var passed = grid?.Columns == 2 && grid.GetChildCount() == 2
            && grid.GetChildren().OfType<PanelContainer>().Count(panel =>
                panel.GetMeta("city_officer_card").AsBool()) == 2
            && labels.Count == 4 && labels[0] == "태수" && labels[2] == "군사"
            && grid.FindChildren("CityOfficerPortrait", "TextureRect", true, false).OfType<TextureRect>()
                .Count(portrait => portrait.Texture is not null) == 2
            && grid.FindChildren("*", "HBoxContainer", true, false).Count >= 2
            && divider?.GetThemeConstant("margin_top") is >= 4 and <= 6
            && scroll?.VerticalScrollMode == ScrollContainer.ScrollMode.Auto
            && actions?.FindChildren("*", "Button", true, false).Count > 0;
        GD.Print($"[city-officer-grid-qa] passed={passed} labels={string.Join(',', labels)} cards={grid?.GetChildCount()} divider={divider?.GetThemeConstant("margin_top")} portraits={grid?.FindChildren("CityOfficerPortrait", "TextureRect", true, false).Count}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private async void RunRightUiLayoutQa()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--maptestrightuiqa=", System.StringComparison.Ordinal));
        var resolution = argument?.EndsWith("1920x1080", System.StringComparison.Ordinal) == true
            ? new Vector2I(1920, 1080) : new Vector2I(1600, 900);
        GetWindow().Size = resolution;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player && !candidate.IsPort);
        SelectCity(city.Id);
        ToggleGroup(0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var viewport = GetViewport().GetVisibleRect().Size;
        var info = _infoCard.GetGlobalRect();
        var command = _cmdMenu.GetGlobalRect();
        var submenu = _cmdSubMenu.GetGlobalRect();
        var gap = info.Position.X - command.End.X;
        var header = _infoRows.FindChild("CityInfoHeader", true, false) as Control;
        var title = _infoRows.FindChild("CityInfoTitle", true, false) as Label;
        var subtitle = _infoRows.FindChild("CityInfoSubtitle", true, false) as Label;
        var contentFitsWithoutScroll = !_infoScroll.GetVScrollBar().Visible
            && _infoScroll.ScrollVertical == 0
            && header is not null && header.GetGlobalRect().Position.Y >= _infoScroll.GetGlobalRect().Position.Y;
        var titleCenter = title?.GetGlobalRect().GetCenter().Y ?? -1f;
        var subtitleCenter = subtitle?.GetGlobalRect().GetCenter().Y ?? -1f;
        var titleBandsCentered = Mathf.Abs(titleCenter - (info.Position.Y + 27f)) <= 1f
            && Mathf.Abs(subtitleCenter - (info.Position.Y + 55.5f)) <= 2f;
        var passed = _infoCard.Visible && _cmdMenu.Visible && _cmdSubMenu.Visible
            && Mathf.Abs(info.End.X - (viewport.X - 12f)) <= 3f
            && Mathf.Abs(gap - 10f) <= 1f
            && command.End.X < info.Position.X
            && submenu.End.X < command.Position.X
            && contentFitsWithoutScroll
            && titleBandsCentered
            && info.Position.Y >= 8f && info.End.Y <= viewport.Y - 8f
            && command.Position.Y >= 8f && command.End.Y <= viewport.Y - 8f;
        var outputPath = System.IO.Path.Combine(
            new System.IO.DirectoryInfo(ProjectSettings.GlobalizePath("res://")).Parent?.FullName
                ?? ProjectSettings.GlobalizePath("res://"),
            $"shot_right_ui_{resolution.X}x{resolution.Y}.png");
        var saveResult = GetViewport().GetTexture().GetImage().SavePng(outputPath);
        passed &= saveResult == Error.Ok;

        var firstHeight = info.Size.Y;
        var otherCity = _state.Cities.First(candidate => candidate.Owner == Player
            && !candidate.IsPort && candidate.Id != city.Id);
        SelectCity(otherCity.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SelectCity(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var reselectionHeight = _infoCard.Size.Y;
        HidePanels();
        SelectCity(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var reopenedHeight = _infoCard.Size.Y;
        var stableReselection = Mathf.Abs(firstHeight - reselectionHeight) <= 1f
            && Mathf.Abs(firstHeight - reopenedHeight) <= 1f
            && _infoScroll.ScrollVertical == 0;
        passed &= stableReselection;

        var summaryLabels = _infoRows.FindChildren("*", "Label", true, false)
            .OfType<Label>().Select(label => label.Text).ToHashSet();
        var requiredSummary = new[]
            { "태수", "군사", "금", "군량", "주 금", "주 군량", "치안", "성벽", "시설", "주둔 병력" };
        var removedDetails = new[]
            { "광물", "부상병", "치안담당", "내정담당", "병력담당", "훈련담당", "출전대기" };
        var compactSummary = requiredSummary.All(summaryLabels.Contains)
            && removedDetails.All(label => !summaryLabels.Contains(label));
        var detailButton = _infoActions.GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Text.Contains("상세 · 진행 목록", System.StringComparison.Ordinal));
        detailButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var detailOpened = _modalLayer is not null;
        if (detailOpened) CloseModal();
        SelectCity(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        passed &= compactSummary && detailOpened;

        var port = _state.Cities.FirstOrDefault(candidate => candidate.Owner == Player && candidate.IsPort);
        var portOk = true;
        if (port is not null)
        {
            SelectCity(port.Id);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            portOk = _infoCard.Visible && _infoRows.FindChildren("*", "Label", true, false)
                .OfType<Label>().Any(label => label.Text.StartsWith("항구 · 시야", System.StringComparison.Ordinal));
            passed &= portOk;
        }
        GD.Print($"[right-ui-qa] passed={passed} resolution={resolution} info={info} command={command} submenu={submenu} gap={gap} contentMin={_infoRows.GetCombinedMinimumSize()} scrollMin={_infoScroll.CustomMinimumSize} contentFits={contentFitsWithoutScroll} titleCenters={titleCenter}/{subtitleCenter} titleBandsCentered={titleBandsCentered} stableReselection={stableReselection} heights={firstHeight}/{reselectionHeight}/{reopenedHeight} compactSummary={compactSummary} detailOpened={detailOpened} port={portOk} shot={outputPath}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private async void RunEnemyCityPanelQa()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--maptestenemycitypanelqa=", System.StringComparison.Ordinal));
        var resolution = argument?.EndsWith("1920x1080", System.StringComparison.Ordinal) == true
            ? new Vector2I(1920, 1080) : new Vector2I(1600, 900);
        GetWindow().Size = resolution;

        async System.Threading.Tasks.Task WaitForLayout()
        {
            for (var frame = 0; frame < 4; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        string SaveShot(string state)
        {
            if (DisplayServer.GetName() == "headless") return "headless";
            var root = new System.IO.DirectoryInfo(ProjectSettings.GlobalizePath("res://")).Parent?.FullName
                ?? ProjectSettings.GlobalizePath("res://");
            var path = System.IO.Path.Combine(root,
                $"shot_enemy_city_{state}_{resolution.X}x{resolution.Y}.png");
            var result = GetViewport().GetTexture().GetImage().SavePng(path);
            return result == Error.Ok ? path : result.ToString();
        }

        await WaitForLayout();
        var own = _state.Cities.First(city => city.Owner == Player);
        var enemy = _state.Cities.First(city => city.Owner != Player);
        var originalIntel = _state.Intel.ToList();

        SelectCity(own.Id);
        await WaitForLayout();
        var ownRect = _infoCard.GetGlobalRect();
        var ownShot = SaveShot("own");

        _state = _state with
        {
            ScoutedCities = originalIntel.Where(intel => intel.City != enemy.Id).ToList(),
        };
        SelectCity(enemy.Id);
        await WaitForLayout();
        var unknownRect = _infoCard.GetGlobalRect();
        var unknownShot = SaveShot("unknown");
        var unknownText = _infoRows.FindChild("CityUnknownIntelText", true, false) as Label;
        var unknownActions = _infoActions.FindChild("CityEnemyActions", true, false) as HBoxContainer;
        var unknownFits = unknownText is not null
            && unknownRect.Encloses(unknownText.GetGlobalRect())
            && unknownActions is not null
            && unknownRect.Encloses(unknownActions.GetGlobalRect())
            && unknownActions.GetChildren().OfType<Button>().Count() == 2
            && _infoScroll.ScrollVertical == 0;

        const int firstScoutDays = 24;
        _state = _state with
        {
            ScoutedCities = originalIntel.Where(intel => intel.City != enemy.Id)
                .Append(new CityIntel(Player, enemy.Id, _state.Day + firstScoutDays)).ToList(),
        };
        SelectCity(enemy.Id);
        await WaitForLayout();
        var scoutedRect = _infoCard.GetGlobalRect();
        var scoutedShot = SaveShot("scouted");
        var scoutedSubtitle = _infoRows.FindChild("CityInfoSubtitle", true, false) as Label;
        var scoutedActions = _infoActions.FindChild("CityEnemyActions", true, false) as HBoxContainer;
        var scoutedFits = scoutedSubtitle?.Text.Contains($"정찰 {firstScoutDays}일",
                System.StringComparison.Ordinal) == true
            && _infoRows.FindChild("CityOfficerGrid", true, false) is GridContainer
            && _infoRows.FindChild("CityPrimaryStats", true, false) is PanelContainer
            && _infoRows.FindChild("CitySecondaryStats", true, false) is PanelContainer
            && scoutedActions is not null
            && scoutedRect.Encloses(scoutedActions.GetGlobalRect())
            && scoutedActions.GetChildren().OfType<Button>().Count() == 2
            && _infoScroll.ScrollVertical == 0;

        const int updatedScoutDays = 9;
        _state = _state with
        {
            ScoutedCities = originalIntel.Where(intel => intel.City != enemy.Id)
                .Append(new CityIntel(Player, enemy.Id, _state.Day + updatedScoutDays)).ToList(),
        };
        SelectCity(own.Id);
        await WaitForLayout();
        SelectCity(enemy.Id);
        await WaitForLayout();
        var reselectionRect = _infoCard.GetGlobalRect();
        var updatedSubtitle = _infoRows.FindChild("CityInfoSubtitle", true, false) as Label;

        var stableSize = new[] { unknownRect.Size, scoutedRect.Size, reselectionRect.Size }
            .All(size => Mathf.Abs(size.X - ownRect.Size.X) <= 1f
                && Mathf.Abs(size.Y - ownRect.Size.Y) <= 1f);
        var boundsOk = new[] { ownRect, unknownRect, scoutedRect, reselectionRect }
            .All(rect => rect.Position.X >= 0f && rect.Position.Y >= 0f
                && rect.End.X <= resolution.X && rect.End.Y <= resolution.Y);
        var refreshed = updatedSubtitle?.Text.Contains($"정찰 {updatedScoutDays}일",
                System.StringComparison.Ordinal) == true
            && _infoScroll.ScrollVertical == 0;
        var passed = unknownFits && scoutedFits && stableSize && boundsOk && refreshed;
        GD.Print($"[enemy-city-panel-qa] passed={passed} resolution={resolution} own={ownRect} unknown={unknownRect} scouted={scoutedRect} reselected={reselectionRect} unknownFits={unknownFits} scoutedFits={scoutedFits} stable={stableSize} bounds={boundsOk} refreshed={refreshed} shots={ownShot}|{unknownShot}|{scoutedShot}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
