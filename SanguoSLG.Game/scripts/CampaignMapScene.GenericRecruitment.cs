namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private CanvasLayer? _genericRecruitLayer;
    private readonly Dictionary<string, ImageTexture> _genericRecruitArtwork = new();

    private static readonly string[][] MartialRecruitDialogues =
    [
        [
            "{city}의 병사 가운데 기개가 뛰어난 이가 있다고 들었소. 전장에 설 자질이 있는지 직접 살펴봅시다.",
            "소신이 여러 후보를 지켜보았습니다. 특히 {name}은 훈련을 빠뜨리지 않고 동료들까지 이끌었습니다.",
            "기세만으로는 오래 버틸 수 없소. 백성을 지키고 명령을 따를 마음까지 갖추었는지 물어봅시다.",
            "{name}은 위험한 자리에서도 대열을 떠나지 않았습니다. 병사들이 먼저 곁에 서기를 원합니다.",
            "제게 맡겨 주신 병사들의 이름을 기억하겠습니다. {city}의 성문 앞에서 한 약속을 전장에서도 지키겠습니다.",
        ],
        [
            "{city}의 수비를 맡길 새 무관을 찾고 있소. 싸움의 재주와 더불어 사람을 아끼는 태도를 보겠소.",
            "{name}은 어제의 모의 교전에서 무리하게 앞서지 않고 흩어진 병사들을 다시 모아 냈습니다.",
            "이기는 법을 아는 사람도 귀하지만, 패한 뒤에 부대를 다시 일으킬 사람은 더욱 귀하오.",
            "소신도 같은 생각입니다. {name}은 부상자를 먼저 돌보고 끝까지 자리를 지킨 것으로 알려졌습니다.",
            "부족한 솜씨는 더 갈고닦겠습니다. 맡겨 주신 부대가 서로를 믿고 싸우도록 제가 먼저 움직이겠습니다.",
        ],
        [
            "장차 먼 길을 나설 부대가 필요하오. {city}에서 병사들의 신뢰를 얻은 사람이 누구인지 말해 주시오.",
            "대열을 직접 살펴본 결과 {name}이 가장 꾸준했습니다. 빠른 판단보다 약속을 지키는 힘이 돋보입니다.",
            "그렇다면 한 사람의 용맹보다 모두의 귀환을 우선하는지 확인해 봅시다. 그 마음이 장수의 자격이오.",
            "{name}은 퇴각 훈련에서도 마지막까지 후위를 맡았습니다. 병사들 사이에 그 이야기가 남아 있습니다.",
            "전공만 좇지 않겠습니다. 이곳에서 함께한 병사들이 무사히 돌아올 수 있도록 제 몫을 다하겠습니다.",
        ],
    ];

    private static readonly string[][] CivilRecruitDialogues =
    [
        [
            "{city}의 장부와 창고를 맡길 사람이 필요하오. 숫자를 아는 것만큼 백성의 형편을 살필 줄 알아야 하오.",
            "{name}은 부족한 군량을 숨기지 않고 먼저 알렸습니다. 그 덕에 마을마다 필요한 양을 다시 나눴습니다.",
            "좋은 소식만 전하는 사람보다 어려운 사실을 정확히 전하는 사람이 지금 우리에게 필요하오.",
            "소신이 다시 확인했습니다. {name}은 기록을 바로잡으면서도 어느 집도 몫에서 빠뜨리지 않았습니다.",
            "허락해 주신다면 장부에 적힌 수치 뒤의 사람들까지 살피겠습니다. 작은 약속부터 지켜 나가겠습니다.",
        ],
        [
            "전쟁이 길어질수록 {city}의 살림을 돌보는 손이 중요해지오. 백성과 병사를 함께 살필 인재를 찾읍시다.",
            "여러 사람의 의견을 들었는데 {name}이 일을 서두르지 않고 먼저 현장의 사정을 묻는다고 합니다.",
            "그 태도가 마음에 드오. 지금 필요한 것은 한 번의 큰 성과보다 내일도 이어질 단단한 질서요.",
            "{name}은 창고와 논의 기록을 대조하여 빠진 물자를 찾아냈습니다. 함께 일한 이들의 평가도 좋습니다.",
            "맡겨 주시는 일에 앞서 현장을 찾아가겠습니다. 백성과 병사가 무엇을 필요로 하는지 먼저 듣겠습니다.",
        ],
        [
            "{city}의 사정을 살피다가 새 인재의 이름을 들었소. 말솜씨보다 실제로 일을 마무리하는 힘을 보겠소.",
            "{name}은 지난달 어긋난 배급 순서를 다시 정했습니다. 원망을 듣고도 기록을 끝까지 확인했습니다.",
            "사람들의 말을 듣고 잘못을 고칠 줄 안다면 맡길 만하오. 함께 일할 이들의 생각은 어떠하오.",
            "처음에는 반대하던 이들도 이제 {name}에게 일을 묻습니다. 결과와 태도로 신뢰를 얻은 셈입니다.",
            "큰 자리를 얻었다고 마음을 앞세우지 않겠습니다. 먼저 듣고 정확히 기록하여 {city}의 일을 돕겠습니다.",
        ],
    ];

    private void ConfirmGenericRecruitment(CityId cityId, bool martial)
    {
        var city = _state.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city is null || city.Owner != Player) return;
        var title = martial ? "무관모집" : "문관모집";
        var actor = city.Governor ?? _state.Assignments.FirstOrDefault(p => p.Faction == city.Owner && p.Location == city.Id)?.General
            ?? _state.Factions.First(f => f.Id == city.Owner).Ruler;
        var month = (_state.Day - 1) / GameState.DaysPerMonth;
        if (_state.GenericRecruitmentHistory.Any(e => e.City == city.Id && e.MonthIndex == month))
        {
            ShowNotice(title, "이 도시에서는 이번 달에 이미 장수를 모집했습니다.");
            return;
        }
        ShowOfficerConfirm(title, $"{city.Name}에 장수가 필요한 것 같습니다.\n금 1,000을 들여 {title}을 진행하겠습니까?",
            actor, () => ExecuteGenericRecruitment(cityId, martial, actor));
    }

    private void ExecuteGenericRecruitment(CityId cityId, bool martial, GeneralId actor)
    {
        var city = _state.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city is null || city.Owner != Player) return;
        var title = martial ? "무관모집" : "문관모집";
        var kind = martial ? CommandKind.RecruitMartial : CommandKind.RecruitCivil;
        var result = _commander.Issue(_state, new CommandRequest(city.Id, kind, actor));
        if (!result.Ok)
        {
            ShowNotice($"{title} 실패", result.Error ?? "모집할 수 없습니다.");
            return;
        }
        _state = result.State;
        var entry = _state.GenericRecruitmentHistory.Last();
        var general = _state.Generals.First(g => g.Id == entry.General);
        Report($"[인재] {city.Name}에서 {general.Name}을(를) {title}으로 발탁했습니다.", GoldBright,
            general.Id);
        Redraw($"{title}: {general.Name}");
        ShowGenericRecruitmentPresentation(entry);
    }

    private void TickGenericRecruitmentPresentation()
    {
        if (_genericRecruitLayer is not null || _advancing || _modalLayer is not null || _confirmLayer is not null
            || _storyOpen) return;
        var pending = _state.GenericRecruitmentHistory.FirstOrDefault(e => !e.Presented
            && _state.PostingOf(e.General)?.Faction == Player);
        if (pending is not null) ShowGenericRecruitmentPresentation(pending);
    }

    private void ShowGenericRecruitmentPresentation(GenericRecruitment entry)
    {
        if (_genericRecruitLayer is not null) return;
        var general = _state.Generals.FirstOrDefault(g => g.Id == entry.General);
        if (general is null) return;
        var city = _state.Cities.FirstOrDefault(c => c.Id == entry.City);
        var rulerName = _state.Generals.FirstOrDefault(g => g.Id == entry.Ruler)?.Name ?? "군주";
        var companionName = _state.Generals.FirstOrDefault(g => g.Id == entry.Companion)?.Name ?? rulerName;
        var scripts = entry.Kind == "martial" ? MartialRecruitDialogues : CivilRecruitDialogues;
        var dialogue = scripts[Math.Clamp(entry.DialogueVariant, 0, scripts.Length - 1)];
        var page = Math.Clamp(entry.DialoguePage, 0, dialogue.Length);
        var layer = new CanvasLayer { Layer = 55 };
        AddChild(layer);
        _genericRecruitLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.63f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
        center.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(650, 0) };
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        var kindName = entry.Kind == "martial" ? "무관" : "문관";
        var title = MakeLabel(page < dialogue.Length ? $"{kindName} 발탁 · {city?.Name ?? "도시"}" : $"{kindName}임명", 19, GoldBright);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);
        box.AddChild(GoldRule());
        if (page < dialogue.Length)
        {
            var speakerId = page switch { 0 or 2 => entry.Ruler, 1 or 3 => entry.Companion, _ => entry.General };
            var speakerName = _state.Generals.FirstOrDefault(g => g.Id == speakerId)?.Name ?? "장수";
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 18);
            box.AddChild(row);
            var portraitBox = new VBoxContainer { CustomMinimumSize = new Vector2(170, 0) };
            row.AddChild(portraitBox);
            portraitBox.AddChild(new TextureRect
            {
                Texture = OfficerPortrait(speakerId), CustomMinimumSize = new Vector2(170, 205),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            });
            var nameLabel = MakeLabel(speakerName, 14, GoldBright);
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            portraitBox.AddChild(nameLabel);
            var line = dialogue[page].Replace("{city}", city?.Name ?? "이곳")
                .Replace("{name}", general.Name).Replace("{ruler}", rulerName)
                .Replace("{companion}", companionName);
            var speech = MakeLabel(line, 16, Parchment);
            speech.CustomMinimumSize = new Vector2(430, 170);
            speech.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(speech);
        }
        else
        {
            var artworkPath = $"res://assets/ui/generic-recruit/{entry.Kind}-appointed.png";
            if (!_genericRecruitArtwork.TryGetValue(artworkPath, out var artwork))
            {
                using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(artworkPath));
                if (image is not null && !image.IsEmpty())
                {
                    image.GenerateMipmaps();
                    artwork = ImageTexture.CreateFromImage(image);
                    _genericRecruitArtwork[artworkPath] = artwork;
                }
            }
            if (artwork is not null)
                box.AddChild(new TextureRect
                {
                    Texture = artwork, CustomMinimumSize = new Vector2(0, 170),
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                });
            box.AddChild(new TextureRect
            {
                Texture = OfficerPortrait(general.Id), CustomMinimumSize = new Vector2(0, 240),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            });
            var details = MakeLabel($"{general.Name} · {kindName}\n무력 {general.Might}   지력 {general.Intellect}   정치 {general.Politics}", 18, GoldBright);
            details.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(details);
            var flourish = MakeLabel("✦  새로운 인재가 합류했습니다  ✦", 16, GoldBright);
            flourish.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(flourish);
            StartAppointmentAnimation(layer, panel, flourish, () =>
            {
                _state = GenericRecruitmentService.MarkPresented(_state, entry.General);
                ShowRecruitmentSummary(entry.General);
            });
            return;
        }
        var footer = new HBoxContainer();
        box.AddChild(footer);
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var next = MakeButton(page < dialogue.Length ? "다음 ▶" : "확인", accent: true);
        next.CustomMinimumSize = new Vector2(100, 34);
        footer.AddChild(next);
        next.Pressed += () =>
        {
            _genericRecruitLayer = null;
            layer.QueueFree();
            if (page >= dialogue.Length)
            {
                _state = GenericRecruitmentService.MarkPresented(_state, entry.General);
                return;
            }
            _state = GenericRecruitmentService.SetDialoguePage(_state, entry.General, page + 1);
            ShowGenericRecruitmentPresentation(_state.GenericRecruitmentHistory.First(e => e.General == entry.General));
        };
    }

    private void StartAppointmentAnimation(CanvasLayer layer, Control panel, CanvasItem flourish, Action completed)
    {
        panel.Modulate = new Color(1, 1, 1, 0);
        flourish.Modulate = new Color(1, 1, 1, 0.35f);
        var tween = CreateTween();
        tween.TweenProperty(panel, "modulate:a", 1f, 0.35f);
        tween.TweenProperty(flourish, "modulate:a", 1f, 0.45f);
        tween.TweenProperty(flourish, "modulate:a", 0.45f, 0.45f);
        tween.TweenInterval(1.15f);
        tween.TweenProperty(panel, "modulate:a", 0f, 0.4f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (_genericRecruitLayer == layer) _genericRecruitLayer = null;
            if (IsInstanceValid(layer)) layer.QueueFree();
            completed();
        }));
    }

    private void ShowHeroRecruitmentAppointment(GeneralId generalId)
    {
        if (_genericRecruitLayer is not null) return;
        var general = _state.Generals.FirstOrDefault(g => g.Id == generalId);
        if (general is null) return;
        var layer = new CanvasLayer { Layer = 55 };
        AddChild(layer);
        _genericRecruitLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.63f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
        center.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        var title = MakeLabel("위인영입", 22, GoldBright);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);
        box.AddChild(GoldRule());
        box.AddChild(new TextureRect
        {
            Texture = OfficerPortrait(generalId), CustomMinimumSize = new Vector2(0, 270),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        });
        var name = MakeLabel(general.Name, 25, GoldBright);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(name);
        var flourish = MakeLabel("✦  새로운 위인이 합류했습니다  ✦", 16, GoldBright);
        flourish.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(flourish);
        StartAppointmentAnimation(layer, panel, flourish, () => ShowRecruitmentSummary(generalId));
    }

    private void ShowRecruitmentSummary(GeneralId generalId)
    {
        var general = _state.Generals.FirstOrDefault(g => g.Id == generalId);
        if (general is null) return;
        CloseModal();
        var layer = new CanvasLayer { Layer = 35 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.58f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(490, 0), MouseFilter = Control.MouseFilterEnum.Stop };
        panel.SetMeta("modal_root", true);
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
        center.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        var title = MakeLabel("새로 합류한 장수", 19, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(36, 32);
        close.Pressed += CloseModal;
        header.AddChild(close);
        box.AddChild(GoldRule());
        box.AddChild(new TextureRect
        {
            Texture = OfficerPortrait(generalId), CustomMinimumSize = new Vector2(0, 230),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        });
        var name = MakeLabel(general.Name, 23, GoldBright);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(name);
        var stats = MakeLabel($"무력 {general.Might}     지력 {general.Intellect}     정치 {general.Politics}", 16, Parchment);
        stats.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(stats);
    }

    private async void RunGenericRecruitmentQa()
    {
        var original = _state;
        var city = _state.Cities.FirstOrDefault(c => c.Owner == Player && !c.IsGate);
        var actor = city is null ? null : _state.Assignments.FirstOrDefault(p => p.Faction == Player
            && p.Location == city.Id)?.General;
        if (city is null || actor is null)
        {
            GD.Print("[genericrecruitqa] passed=False reason=no-player-city-or-officer");
            GetTree().Quit(1);
            return;
        }
        var group = Array.FindIndex(CmdGroups, g => g.Group == "인재");
        SelectCity(city.Id);
        ToggleGroup(group);
        var menuLabels = _cmdSubList.FindChildren("*", "Button", true, false).OfType<Button>()
            .Select(button => button.Text).ToArray();
        var menuOk = menuLabels.Contains("무관모집") && menuLabels.Contains("문관모집");
        CloseGroupMenu();
        var allDialogue = MartialRecruitDialogues.Concat(CivilRecruitDialogues).ToArray();
        var scriptsOk = allDialogue.Length == 6 && allDialogue.All(script => script.Length == 5
            && script.All(line => line.Replace("{city}", "").Replace("{name}", "").Length >= 30));
        var imagesOk = new[] { "martial", "civil" }.All(kind =>
            Godot.FileAccess.FileExists($"res://assets/ui/generic-recruit/{kind}-appointed.png")
            && Enumerable.Range(2, 4).All(age => Godot.FileAccess.FileExists(
                $"res://assets/portraits/generic/{age}0s-{kind}.png")));
        _state = _state with { Cities = _state.Cities.Select(c => c.Id == city.Id
            ? c with { Gold = Math.Max(c.Gold, 1000) } : c).ToArray() };
        ConfirmGenericRecruitment(city.Id, true);
        var confirmOk = _confirmLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Any(label => label.Text.Contains("장수가 필요한 것 같습니다", StringComparison.Ordinal)) == true;
        _confirmLayer?.QueueFree();
        _confirmLayer = null;
        var result = _commander.Issue(_state,
            new CommandRequest(city.Id, CommandKind.RecruitMartial, actor.Value));
        var dialogueOk = result.Ok;
        if (result.Ok)
        {
            _state = result.State;
            var entry = _state.GenericRecruitmentHistory.Last();
            ShowGenericRecruitmentPresentation(entry);
            for (var page = 0; page < 5; page++)
            {
                dialogueOk &= _genericRecruitLayer is not null;
                var button = _genericRecruitLayer?.FindChildren("*", "Button", true, false)
                    .OfType<Button>().FirstOrDefault(b => b.Text == "다음 ▶");
                dialogueOk &= button is not null;
                button?.EmitSignal(Button.SignalName.Pressed);
            }
            var appointment = _genericRecruitLayer;
            dialogueOk &= appointment?.FindChildren("*", "TextureRect", true, false)
                .OfType<TextureRect>().Count(image => image.Texture is not null) >= 2;
            await ToSignal(GetTree().CreateTimer(3.4), SceneTreeTimer.SignalName.Timeout);
            dialogueOk &= _state.GenericRecruitmentHistory.Last().Presented;
            dialogueOk &= RecruitmentSummaryVisible(entry.General);
            CloseModal();
        }
        if (_genericRecruitLayer is not null) { _genericRecruitLayer.QueueFree(); _genericRecruitLayer = null; }
        ShowHeroRecruitmentAppointment(actor.Value);
        var heroOk = _genericRecruitLayer?.FindChildren("*", "TextureRect", true, false)
            .OfType<TextureRect>().Any(image => image.Texture is not null) == true;
        await ToSignal(GetTree().CreateTimer(3.4), SceneTreeTimer.SignalName.Timeout);
        heroOk &= _genericRecruitLayer is null;
        heroOk &= RecruitmentSummaryVisible(actor.Value);
        CloseModal();
        _state = original;
        var passed = menuOk && confirmOk && scriptsOk && imagesOk && dialogueOk && heroOk;
        GD.Print($"[genericrecruitqa] passed={passed} menu={menuOk} confirm={confirmOk} scripts={scriptsOk} images={imagesOk} dialogue={dialogueOk} hero={heroOk}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private bool RecruitmentSummaryVisible(GeneralId generalId)
        => _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Any(label => label.Text == _state.Generals.First(g => g.Id == generalId).Name) == true
            && _modalLayer.FindChildren("*", "Button", true, false).OfType<Button>()
                .Any(button => button.Text == "✕");
}
