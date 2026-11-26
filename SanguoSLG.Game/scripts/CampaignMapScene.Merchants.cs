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
        if (_advancing) return true;
        OpenMerchantModal(visit.City);
        return true;
    }

    private void OpenMerchantModal(CityId cityId)
        => OpenMerchantTradeWindow(cityId);

    private void ConfirmMerchantAction(string message, Action onConfirm)
        => ShowConfirm("상인 거래 확인", message, onConfirm);

}
