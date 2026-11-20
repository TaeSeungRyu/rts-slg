using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private Control BuildNavalComposeHeroCard(string role, GeneralId? id)
    {
        var card = new Control
        {
            CustomMinimumSize = new Vector2(285, 108),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true,
        };
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>(DeployCommandAssetRoot + "hero_card.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(background);
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        card.AddChild(margin);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);
        if (id is not { } generalId)
        {
            var empty = MakeLabel($"{role}\n선택되지 않음", 14, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(empty);
            return card;
        }
        var general = _state.Generals.First(item => item.Id == generalId);
        var portrait = FixedIcon(OfficerPortrait(generalId), 74);
        portrait.CustomMinimumSize = new Vector2(74, 74);
        portrait.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        portrait.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(portrait);
        var text = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        text.AddChild(MakeLabel($"{role}   {general.Name}", 17, GoldBright));
        text.AddChild(MakeLabel($"해상 적성 {GradeText(general.AptitudeFor(TroopClass.Naval))}", 13, Parchment));
        text.AddChild(MakeLabel($"무 {general.Might} · 지 {general.Intellect} · 정 {general.Politics}", 13, Parchment));
        row.AddChild(text);
        return card;
    }
}
