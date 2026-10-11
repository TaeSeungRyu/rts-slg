using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void BeginSupplyReplenishmentTargeting(int sourceId)
    {
        var source = _state.Armies.FirstOrDefault(unit => unit.Id.Value == sourceId);
        if (_advancing || source is null || !source.IsSupply || source.Field.Owner != Player)
        {
            ShowNotice("보충 불가", "명령 가능한 아군 보급부대가 아닙니다.");
            return;
        }
        if (_state.ReplenishmentOrders.Any(order => order.Source == source.Id))
        {
            ShowNotice("보충 불가", "이미 다른 부대에 보충을 예약했습니다. 다음 진행 후 다시 명령하세요.");
            return;
        }
        if (!_state.Armies.Any(unit => unit.Id != source.Id && unit.Field.Owner == Player
            && _supplyReplenishment.Limits(_state, source.Id, unit.Id) is var limits
            && limits.Troops + limits.Gold + limits.Provisions > 0))
        {
            ShowNotice("보충 불가", "인접한 보충 가능 아군 부대가 없습니다.");
            return;
        }
        HidePanels();
        _supplyReplenishmentSourceId = sourceId;
        _log.Text = "보충할 인접 아군 부대를 클릭하세요. 우클릭 또는 Esc로 취소합니다.";
    }

    private CombatUnit? PickSupplyReplenishmentTarget(Vector2 screen)
    {
        if (_supplyReplenishmentSourceId < 0) return null;
        var sourceId = new UnitId(_supplyReplenishmentSourceId);
        return DisplayedArmies.Where(unit => unit.Id != sourceId && unit.Field.Owner == Player
                && _armyTokens.TryGetValue(unit.Id.Value, out var token) && IsInstanceValid(token)
                && token.Visible
                && _supplyReplenishment.Limits(_state, sourceId, unit.Id) is var limits
                && limits.Troops + limits.Gold + limits.Provisions > 0)
            .Select(unit => (Unit: unit, Distance: _camera.UnprojectPosition(
                _armyTokens[unit.Id.Value].GlobalPosition + new Vector3(0f, .2f, 0f)).DistanceTo(screen)))
            .Where(entry => entry.Distance <= 32f)
            .OrderBy(entry => entry.Distance).ThenBy(entry => entry.Unit.Id.Value)
            .Select(entry => entry.Unit).FirstOrDefault();
    }

    private void StopSupplyReplenishmentTargeting()
    {
        _supplyReplenishmentSourceId = -1;
        if (_ring is not null) _ring.Visible = false;
    }

    private void OpenSupplyReplenishmentModal(UnitId targetId)
    {
        var sourceId = new UnitId(_supplyReplenishmentSourceId);
        StopSupplyReplenishmentTargeting();
        var source = _state.Armies.FirstOrDefault(unit => unit.Id == sourceId);
        var target = _state.Armies.FirstOrDefault(unit => unit.Id == targetId);
        if (source is null || target is null) return;
        var limits = _supplyReplenishment.Limits(_state, sourceId, targetId);
        var width = Mathf.Min(560f, GetViewport().GetVisibleRect().Size.X - 48f);
        var box = DeployScaffold(width, out var scroll, out var panel);
        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel("◈ 보급부대 보충", 18, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        var close = MakeButton("×");
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel($"대상: {TroopName(target.TroopCode)} · 병력 {target.Pool.Active:N0}명\n다음 진행 시작 시 인접 상태와 보유량을 재확인해 전달합니다.", 13, Parchment));

        int troops = 0, gold = 0, provisions = 0;
        void ResourceRow(string name, int maximum, System.Action<int> onChange)
        {
            box.AddChild(MakeLabel($"{name} · 최대 {maximum:N0}", 13, GoldBright));
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            box.AddChild(row);
            var slider = ApplySliderStyle(new HSlider
            {
                Name = $"SupplyReplenishmentSlider{name}",
                MinValue = 0, MaxValue = maximum, Step = 1,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(260, 26),
            });
            var input = ApplyNumberInputStyle(new SpinBox
            {
                Name = $"SupplyReplenishmentInput{name}",
                MinValue = 0, MaxValue = maximum, Step = 1,
                CustomMinimumSize = new Vector2(130, 30),
            });
            slider.ValueChanged += value => { input.SetValueNoSignal(value); onChange((int)value); };
            input.ValueChanged += value => { slider.SetValueNoSignal(value); onChange((int)value); };
            row.AddChild(slider);
            row.AddChild(input);
        }
        ResourceRow("병력", limits.Troops, value => troops = value);
        ResourceRow("금", limits.Gold, value => gold = value);
        ResourceRow("군량", limits.Provisions, value => provisions = value);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 12);
        box.AddChild(actions);
        var accept = MakeButton("확인", accent: true);
        accept.Pressed += () =>
        {
            if (troops + gold + provisions <= 0)
            {
                ShowNotice("보충 불가", "병력·금·군량 중 하나 이상을 입력하세요.");
                return;
            }
            CloseModal();
            var order = new SupplyReplenishmentOrder(sourceId, targetId, troops, gold, provisions);
            var message = $"병력 {troops:N0}명 · 금 {gold:N0} · 군량 {provisions:N0}을(를) 보충하겠습니다.\n다음 진행 시작 시 대상 부대에 전달됩니다.";
            void Queue()
            {
                var result = _supplyReplenishment.Queue(_state, Player, order);
                if (!result.Ok) { ShowNotice("보충 불가", result.Error ?? "보충을 예약할 수 없습니다."); return; }
                _state = result.State;
                Redraw("[보충] 다음 진행에 전달할 물자를 예약했습니다.");
            }
            if (source.VanguardId is { } officer) ShowOfficerConfirm("보충 명령 확인", message, officer, Queue);
            else ShowConfirm("보충 명령 확인", message, Queue);
        };
        actions.AddChild(accept);
        var cancel = MakeButton("취소");
        cancel.Pressed += CloseModal;
        actions.AddChild(cancel);
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        var height = Mathf.Min(box.GetCombinedMinimumSize().Y + 12f, GetViewport().GetVisibleRect().Size.Y - 30f);
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private void RunSupplyReplenishmentQa()
    {
        try
        {
            var city = _state.Cities.First(candidate => candidate.Owner == Player);
            CombatUnit MakeUnit(int id, int q, bool supply)
            {
                var field = new FieldUnit(new UnitId(id), Player,
                    new SanguoSLG.Core.Spatial.HexCoord(q, city.Position.R), 2, 2, 1,
                    SanguoSLG.Core.Spatial.MovementDomain.Land, UnitMode.Standby, null, id, 1);
                return new CombatUnit(field, new CombatStats(5000, 8, 8), new TroopPool(5000, 0),
                    UnitCombatState.Create(60), MaxTroops: 10000, Provisions: supply ? 200 : 20,
                    IsSupply: supply, TroopCode: "swordsman",
                    VanguardId: _state.Generals.First().Id,
                    SupplyCargo: supply ? [new SupplyComponent("swordsman", 5000, 60)] : null,
                    CargoGold: supply ? 300 : 0);
            }
            var source = MakeUnit(99001, city.Position.Q, true);
            var target = MakeUnit(99002, city.Position.Q + 1, false);
            _state = _state with { FieldArmies = _state.Armies.Concat([source, target]).ToList() };
            Redraw("보충 QA");
            BeginSupplyReplenishmentTargeting(source.Id.Value);
            if (_supplyReplenishmentSourceId != source.Id.Value)
                throw new System.Exception("보충 대상 지정 모드 진입 실패");
            var picked = PickSupplyReplenishmentTarget(_camera.UnprojectPosition(
                _armyTokens[target.Id.Value].GlobalPosition + new Vector3(0f, .2f, 0f)));
            if (picked?.Id != target.Id) throw new System.Exception("인접 부대 맵 선택 실패");
            OpenSupplyReplenishmentModal(target.Id);
            foreach (var (name, value) in new[] { ("병력", 100), ("금", 20), ("군량", 10) })
            {
                var input = _modalLayer?.FindChild($"SupplyReplenishmentInput{name}", true, false) as SpinBox;
                if (input is null || input.MaxValue < value)
                    throw new System.Exception($"{name} 수치 입력 누락");
                input.Value = value;
            }
            var accept = _modalLayer?.FindChildren("*", "Button", true, false)
                .OfType<Button>().FirstOrDefault(button => button.Text == "확인");
            if (accept is null) throw new System.Exception("보충 확인 버튼 누락");
            accept.EmitSignal(Button.SignalName.Pressed);
            var confirm = _confirmLayer?.FindChildren("*", "Button", true, false)
                .OfType<Button>().FirstOrDefault(button => button.Text == "확인");
            if (confirm is null) throw new System.Exception("장수 컨펌 누락");
            confirm.EmitSignal(Button.SignalName.Pressed);
            var order = _state.ReplenishmentOrders.Single();
            if (order != new SupplyReplenishmentOrder(source.Id, target.Id, 100, 20, 10))
                throw new System.Exception("다음 진행 보충 예약 수치 불일치");
            BeginSupplyReplenishmentTargeting(source.Id.Value);
            if (_supplyReplenishmentSourceId >= 0 || _state.ReplenishmentOrders.Count != 1)
                throw new System.Exception("중복 보충 명령 차단 실패");
            GD.Print("[maptestsupplyreplenishmentqa] passed=True target=True inputs=3 confirm=True queued=True duplicateBlocked=True");
            GetTree().Quit();
        }
        catch (System.Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
