namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed partial class CampaignMapScene
{
    private Node3D _merchantLayer = null!;
    private readonly Dictionary<int, (int ArrivedDay, Node3D Node)> _merchantViews = [];

    private void RedrawMerchants()
    {
        if (_merchantLayer is null) return;
        var current = _state.Merchants.Select(visit => visit.City.Value).ToHashSet();
        foreach (var city in _merchantViews.Keys.Where(id => !current.Contains(id)).ToList())
        {
            _merchantViews[city].Node.QueueFree();
            _merchantViews.Remove(city);
        }
        foreach (var visit in _state.Merchants)
        {
            if (_merchantViews.TryGetValue(visit.City.Value, out var view)
                && view.ArrivedDay == visit.ArrivedDay) continue;
            if (_merchantViews.Remove(visit.City.Value, out var old)) old.Node.QueueFree();
            var scene = GD.Load<PackedScene>("res://assets/models/troop-merchant.glb");
            var model = scene?.Instantiate<Node3D>();
            if (model is null) continue;
            var root = new Node3D
            {
                Name = $"Merchant_{visit.City.Value}",
                Position = _view.HexToWorld(visit.Position) + new Vector3(0f, _view.TileTopY, 0f),
            };
            _merchantLayer.AddChild(root);
            root.AddChild(model);
            var name = new Label3D
            {
                Text = "상인",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Font = _font,
                FontSize = 36,
                OutlineSize = 8,
                PixelSize = CommanderPortraitView3D.LabelPixelSize,
                NoDepthTest = true,
                Position = new Vector3(0f, 0.65f, 0f),
                Modulate = GoldBright,
            };
            root.AddChild(name);
            _merchantViews[visit.City.Value] = (visit.ArrivedDay, root);
            _fog.Register(root, visit.Position);
        }
    }

    private bool TryOpenMerchantAt(HexCoord hex)
    {
        var visit = _state.Merchants.FirstOrDefault(item => item.Position == hex);
        if (visit is null) return false;
        OpenMerchantModal(visit.City);
        return true;
    }

    private void OpenMerchantModal(CityId cityId)
    {
        var visit = _state.Merchants.FirstOrDefault(item => item.City == cityId);
        var city = _state.Cities.FirstOrDefault(item => item.Id == cityId);
        if (visit is null || city is null) return;
        var editable = city.Owner == Player;
        var basePrice = _commander.MarketUnitPricePer100(_state, city, MarketResource.Grain);
        var buyPrice = (basePrice * visit.BuyGrainPercent + 99) / 100;
        var sellPrice = basePrice * visit.SellGrainPercent / 100;
        var width = Mathf.Min(760f, GetViewport().GetVisibleRect().Size.X - 32f);
        var height = Mathf.Min(650f, GetViewport().GetVisibleRect().Size.Y - 32f);
        var box = SystemView($"상인 · {city.Name}", width, out var scroll,
            out var panel, out var titleRow, useModalChrome: true);
        // ModalChrome의 본문 뷰포트는 콘텐츠 최소 크기를 외곽에 전달하지 않는다.
        // 상인 창은 외곽 크기를 지정해야 헤더·본문·푸터가 함께 펼쳐진다.
        panel.CustomMinimumSize = new Vector2(width, height);
        panel.Size = panel.CustomMinimumSize;
        var dismiss = titleRow.GetNodeOrNull<Button>("SystemViewBack");
        if (dismiss is not null)
        {
            dismiss.ButtonDown -= OpenSystemPalette;
            dismiss.Text = "상인 떠나 보내기";
            dismiss.Disabled = !editable;
            dismiss.CustomMinimumSize = new Vector2(150f, 34f);
            dismiss.Pressed += () => ConfirmMerchantAction("상인을 떠나 보내시겠습니까?", () =>
            {
                _state = MerchantService.Dismiss(_state, cityId);
                CloseModal();
                Redraw("상인이 떠났습니다.");
            });
        }
        var upper = new HBoxContainer();
        upper.AddThemeConstantOverride("separation", 12);
        box.AddChild(upper);
        void FoodSection(string heading, bool buying)
        {
            var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            card.AddThemeStyleboxOverride("panel", Frame(InkSoft, Gold, 1, 8, 12));
            upper.AddChild(card);
            var content = new VBoxContainer();
            card.AddChild(content);
            content.AddChild(MakeLabel(heading, 17, GoldBright));
            content.AddChild(MakeLabel($"100군량당 금 {(buying ? buyPrice : sellPrice):N0}", 13, Parchment));
            content.AddChild(MakeLabel($"보유 금 {city.Gold:N0} · 군량 {city.Provisions:N0}", 12, Parchment));
            var units = new SpinBox { MinValue = 100, MaxValue = 100000, Step = 100,
                Value = 100, CustomMinimumSize = new Vector2(150f, 32f) };
            content.AddChild(units);
            var action = MakeButton(buying ? "군량 구매" : "군량 판매", accent: true);
            action.Disabled = !editable;
            action.Pressed += () =>
            {
                var amount = (int)units.Value;
                ConfirmMerchantAction($"군량 {amount:N0}을 {(buying ? "구매" : "판매")}하시겠습니까?", () =>
                {
                    var result = buying
                        ? MerchantService.BuyGrain(_state, cityId, Player, amount, basePrice)
                        : MerchantService.SellGrain(_state, cityId, Player, amount, basePrice);
                    ApplyMerchantTrade(result, cityId);
                });
            };
            content.AddChild(action);
        }
        FoodSection("군량 구매", true);
        FoodSection("군량 판매", false);
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("유물 구매 · 구매 후 단서·보물 보관함에서 장착", 17, GoldBright));
        var stock = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stock.AddThemeConstantOverride("h_separation", 10);
        stock.AddThemeConstantOverride("v_separation", 10);
        box.AddChild(stock);
        foreach (var code in visit.ArtifactCodes)
        {
            var artifact = MerchantCatalog.Find(code);
            if (artifact is null) continue;
            var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            card.AddThemeStyleboxOverride("panel", Frame(InkSoft, Gold, 1, 8, 10));
            stock.AddChild(card);
            var content = new VBoxContainer();
            card.AddChild(content);
            content.AddChild(MakeLabel(artifact.Name, 15, GoldBright));
            var effect = artifact.Effect switch
            {
                "might_percent" => $"무력 +{artifact.Value}%",
                "movement_percent" => $"부대 이동 +{artifact.Value}%",
                _ => $"지력 +{artifact.Value}%",
            };
            if (artifact.DefensePercent > 0) effect += $" · 지휘 병종 방어 +{artifact.DefensePercent}%";
            content.AddChild(MakeLabel(effect, 12, Parchment));
            var buy = MakeButton($"금 {artifact.GoldCost:N0} · 구매");
            buy.Disabled = !editable;
            buy.Pressed += () => ConfirmMerchantAction($"{artifact.Name}을 금 {artifact.GoldCost:N0}에 구매하시겠습니까?", () =>
                ApplyMerchantTrade(MerchantService.BuyArtifact(_state, cityId, Player, artifact.Code), cityId));
            content.AddChild(buy);
        }
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private void ConfirmMerchantAction(string message, Action onConfirm)
    {
        var dialog = new ConfirmationDialog
        {
            Title = "상인",
            DialogText = message,
            OkButtonText = "확인",
        };
        AddChild(dialog);
        dialog.Confirmed += onConfirm;
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCentered();
    }

    private void ApplyMerchantTrade(CommandResult result, CityId cityId)
    {
        if (!result.Ok)
        {
            ShowNotice("상인 거래", result.Error ?? "거래할 수 없습니다.");
            return;
        }
        _state = result.State;
        CloseModal();
        Redraw("상인 거래를 마쳤습니다.");
        OpenMerchantModal(cityId);
    }
}
