namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private async void RunArmyCorpsModalQa()
    {
        OpenSystemPalette();
        OpenArmyCorpsList();
        OpenArmyCorpsEditor(null);
        OpenArmyCorpsList();
        var close = _modalLayer?.FindChild("SystemViewClose", true, false) as Button;
        close?.EmitSignal(BaseButton.SignalName.ButtonDown);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        var leftovers = GetChildren().OfType<CanvasLayer>().Count(layer =>
            layer.GetChildren().OfType<PanelContainer>().Any(panel => panel.HasMeta("modal_root")));
        var passed = close is not null && _modalLayer is null && leftovers == 0;
        GD.Print($"[maptestcorpsmodalqa] close={close is not null} leftovers={leftovers} passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void OpenArmyCorpsList()
    {
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var width = Mathf.Min(920f, GetViewport().GetVisibleRect().Size.X * 0.9f);
        var height = Mathf.Min(640f, GetViewport().GetVisibleRect().Size.Y * 0.86f);
        var box = SystemView("군단편성", width, out var scroll, out var panel, out var titleRow);
        box.AddChild(MakeLabel("군단장에게 성·항구를 맡깁니다. 내정중심은 공격 공세를 시작하지 않습니다.", 14, Parchment));
        var create = MakeButton("＋ 새 군단 편성", accent: true);
        create.Pressed += () => OpenArmyCorpsEditor(null);
        box.AddChild(create);
        box.AddChild(GoldRule());
        foreach (var corps in _state.Corps.Where(entry => entry.Faction == Player).OrderBy(entry => entry.Id))
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 58) };
            box.AddChild(row);
            var leader = _state.Generals.FirstOrDefault(general => general.Id == corps.Commander)?.Name ?? "장수 없음";
            var cities = string.Join(", ", corps.Cities.Select(id => _state.Cities.FirstOrDefault(city => city.Id == id)?.Name ?? "상실"));
            var label = MakeLabel($"제{corps.Id}군단  ·  {leader}  ·  {(corps.Policy == ArmyCorpsPolicy.Combat ? "전투중심" : "내정중심")}\n{cities}", 14, Parchment);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            var edit = MakeButton("편집");
            edit.Pressed += () => OpenArmyCorpsEditor(corps.Id);
            row.AddChild(edit);
            var disband = MakeButton("해산");
            disband.Pressed += () =>
            {
                _state = ArmyCorpsService.Disband(_state, corps.Id, Player);
                OpenArmyCorpsList();
            };
            row.AddChild(disband);
            box.AddChild(GoldRule());
        }
        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private void OpenArmyCorpsEditor(int? corpsId)
    {
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var existing = _state.Corps.FirstOrDefault(entry => entry.Id == corpsId && entry.Faction == Player);
        var width = Mathf.Min(1040f, GetViewport().GetVisibleRect().Size.X * 0.94f);
        var height = Mathf.Min(690f, GetViewport().GetVisibleRect().Size.Y * 0.88f);
        var box = SystemView(existing is null ? "새 군단 편성" : $"제{existing.Id}군단 편집", width,
            out var scroll, out var panel, out var titleRow);
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        box.AddChild(body);

        var left = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddChild(left);
        left.AddChild(MakeLabel("담당 성·항구", 17, Gold));
        left.AddChild(MakeLabel("같은 거점은 한 군단에만 배치할 수 있습니다.", 12, Parchment));
        var cityScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, height - 195),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        left.AddChild(cityScroll);
        var cityGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        cityScroll.AddChild(cityGrid);
        var selected = existing?.Cities.ToHashSet() ?? [];
        var leaderOption = MakeOption(320);
        foreach (var city in _state.Cities.Where(city => city.Owner == Player).OrderBy(city => city.Name))
        {
            var occupied = _state.Corps.Any(other => other.Faction == Player
                && other.Id != corpsId && other.Cities.Contains(city.Id));
            var check = new CheckBox { Text = $"{city.Name} {(city.IsPort ? "[항구]" : "[성]" )}",
                ButtonPressed = selected.Contains(city.Id), Disabled = occupied,
                CustomMinimumSize = new Vector2(205, 40) };
            check.AddThemeColorOverride("font_color", Parchment);
            check.Toggled += pressed =>
            {
                if (pressed) selected.Add(city.Id);
                else selected.Remove(city.Id);
                RefreshCommanders();
            };
            cityGrid.AddChild(check);
        }

        var right = new VBoxContainer { CustomMinimumSize = new Vector2(350, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddChild(right);
        right.AddChild(MakeLabel("군단장", 17, Gold));
        right.AddChild(leaderOption);
        right.AddChild(MakeLabel("군단방침", 17, Gold));
        var policyOption = MakeOption(320);
        policyOption.AddItem("내정중심 · 공세 없음", (int)ArmyCorpsPolicy.Domestic);
        policyOption.AddItem("전투중심 · AI 전투 규칙", (int)ArmyCorpsPolicy.Combat);
        policyOption.Select(existing?.Policy == ArmyCorpsPolicy.Combat ? 1 : 0);
        right.AddChild(policyOption);
        right.AddChild(MakeLabel("군단에 속한 장수·병력·자원으로 운영하며, 거점 수에 따른 AI 생산 규칙을 사용합니다.", 13, Parchment));
        var error = MakeLabel("", 13, Gold);
        right.AddChild(error);

        void RefreshCommanders()
        {
            var former = leaderOption.Selected >= 0 ? leaderOption.GetItemId(leaderOption.Selected)
                : existing?.Commander.Value ?? -1;
            leaderOption.Clear();
            foreach (var general in _state.Generals.Where(general =>
                    _state.PostingOf(general.Id) is { } posting && posting.Faction == Player
                    && posting.Location is { } city && selected.Contains(city)
                    && !_state.IsGeneralInField(general.Id)
                    && !_state.Corps.Any(other => other.Id != corpsId && other.Commander == general.Id))
                .OrderBy(general => general.Name))
                leaderOption.AddItem(general.Name, general.Id.Value);
            for (var index = 0; index < leaderOption.ItemCount; index++)
                if (leaderOption.GetItemId(index) == former) { leaderOption.Select(index); break; }
            if (leaderOption.Selected < 0 && leaderOption.ItemCount > 0) leaderOption.Select(0);
        }
        RefreshCommanders();
        var actions = new HBoxContainer();
        box.AddChild(actions);
        var cancel = MakeButton("취소");
        cancel.Pressed += OpenArmyCorpsList;
        actions.AddChild(cancel);
        var save = MakeButton("군단 저장", accent: true);
        save.Pressed += () =>
        {
            if (leaderOption.Selected < 0)
            {
                error.Text = "맡길 거점과 주둔 군단장을 선택하세요.";
                return;
            }
            try
            {
                _state = ArmyCorpsService.Save(_state, corpsId, Player,
                    new GeneralId(leaderOption.GetItemId(leaderOption.Selected)), selected,
                    (ArmyCorpsPolicy)policyOption.GetItemId(policyOption.Selected));
                OpenArmyCorpsList();
            }
            catch (System.InvalidOperationException exception) { error.Text = exception.Message; }
        };
        actions.AddChild(save);
        scroll.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
    }
}
