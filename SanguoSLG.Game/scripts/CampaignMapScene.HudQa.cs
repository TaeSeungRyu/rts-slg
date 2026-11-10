using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private async void RunGameHudQa()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var faction = _state.Factions.First(f => f.Id == Player);
        var ruler = _state.Generals.FirstOrDefault(general => general.Id == faction.Ruler);
        var expectedDate = $"{_state.Year}년 {_state.Month}월 {_state.DayOfMonth}일";
        var expectedCities = $"점령 도시  {_state.CityCount(Player)}";
        var viewport = GetViewport().GetVisibleRect().Size;
        var buttons = _hudPanel.FindChildren("RulerHud*Button", "Button", true, false).OfType<Button>().ToArray();
        var centeredIcons = buttons.Select(button => button.GetNodeOrNull<TextureRect>("GameUiCenteredIcon"))
            .ToArray();
        var portraitDecoration = _hudFacePanel.GetNodeOrNull<TextureRect>("RulerHudPortraitDecoration");
        var topSafe = Mathf.IsEqualApprox(_hudPanel.Position.Y, 2f)
            && _hudPanel.GetGlobalRect().Position.Y >= 0f
            && (portraitDecoration is null || portraitDecoration.GetGlobalRect().Position.Y >= 0f);
        var rulerRect = _hudRuler.GetGlobalRect();
        var cityRect = _hudCityCount.GetGlobalRect();
        var dateRect = _hudDate.GetGlobalRect();
        var lowerText = _hudPanel.FindChild("RulerHudLowerText", true, false) as Control;
        var informationOrder = rulerRect.Position.Y < cityRect.Position.Y
            && cityRect.Position.Y < dateRect.Position.Y
            && dateRect.End.Y <= _hudPanel.GetGlobalRect().End.Y - 8f;
        var lowerInfoCentered = lowerText is not null && buttons.All(button =>
            Mathf.Abs((lowerText.GetGlobalRect().Position.Y + lowerText.Size.Y / 2f)
                - (button.GetGlobalRect().Position.Y + button.Size.Y / 2f)) <= 1f);
        var fits = _hudPanel.Position.X >= 0 && _hudPanel.Position.Y >= 0
            && _hudPanel.Position.X + _hudPanel.Size.X <= viewport.X
            && _hudPanel.Position.Y + _hudPanel.Size.Y <= viewport.Y;
        var passed = _hudPanel.GetThemeStylebox("panel") is StyleBoxTexture
            && _hudFacePanel.GetThemeStylebox("panel") is StyleBox
            && portraitDecoration?.Texture is not null
            && portraitDecoration.MouseFilter == Control.MouseFilterEnum.Ignore
            && _hudDate.Text == expectedDate
            && _hudRuler.Text == (ruler?.Name ?? "미지정")
            && _hudCityCount.Text == expectedCities
            && informationOrder
            && lowerInfoCentered
            && topSafe
            && buttons.Length == 2
            && centeredIcons.All(icon => icon?.Texture is not null
                && icon.MouseFilter == Control.MouseFilterEnum.Ignore
                && icon.Position + icon.Size / 2f == icon.GetParent<Control>().Size / 2f)
            && buttons.All(button => button.Icon is null
                && button.GetThemeStylebox("normal") is StyleBoxTexture
                && button.GetThemeStylebox("hover") is StyleBoxTexture
                && button.GetThemeStylebox("pressed") is StyleBoxTexture
                && button.GetThemeStylebox("disabled") is StyleBoxTexture)
            && fits;
        GD.Print($"[game-hud-qa] passed={passed} viewport={viewport} panel={_hudPanel.Size} "
            + $"date='{_hudDate.Text}' ruler='{_hudRuler.Text}' cities='{_hudCityCount.Text}' "
            + $"portrait={_hudFace.Texture is not null} portraitFrame={portraitDecoration?.Texture is not null} "
            + $"informationOrder={informationOrder} lowerInfoCentered={lowerInfoCentered} topSafe={topSafe} centeredIcons="
            + $"{centeredIcons.Count(icon => icon?.Texture is not null)} buttons={buttons.Length}");
        var shotArgument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(argument => argument.StartsWith("--maptesthudshot=", StringComparison.Ordinal));
        if (shotArgument is not null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var tag = shotArgument["--maptesthudshot=".Length..];
            var projectDirectory = new System.IO.DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
            var outputPath = System.IO.Path.Combine(projectDirectory.Parent?.FullName ?? projectDirectory.FullName,
                $"shot_hud_{tag}.png");
            var saveResult = GetViewport().GetTexture().GetImage().SavePng(outputPath);
            passed &= saveResult == Error.Ok;
            GD.Print($"[game-hud-shot] result={saveResult} path={outputPath}");
        }
        GetTree().Quit(passed ? 0 : 1);
    }
}
