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
        var portraitDecoration = _hudFacePanel.GetNodeOrNull<TextureRect>("RulerHudPortraitDecoration");
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
            && buttons.Length == 2
            && buttons.All(button => button.GetThemeStylebox("normal") is StyleBox
                && button.GetThemeStylebox("hover") is StyleBox
                && button.GetThemeStylebox("pressed") is StyleBox
                && button.GetThemeStylebox("disabled") is StyleBox)
            && fits;
        GD.Print($"[game-hud-qa] passed={passed} viewport={viewport} panel={_hudPanel.Size} "
            + $"date='{_hudDate.Text}' ruler='{_hudRuler.Text}' cities='{_hudCityCount.Text}' "
            + $"portrait={_hudFace.Texture is not null} portraitFrame={portraitDecoration?.Texture is not null} "
            + $"buttons={buttons.Length}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
