using Godot;

/// <summary>
/// 商店的「买血」入口（<see cref="Store"/> 的一部分）。
///
/// 单独拆一个文件是因为 Store.cs 加上这块已经到 306 行、破了规范里 300 行的红线；
/// 本块自成一体（一个按钮 + 购买 + 状态着色），拆出来两边都更好读。
///
/// 买血与买卡的区别：卡位是有限的 7 个槽位、有折扣与售罄状态，而买血是**无限量**的——
/// 血量在 BattleStateManager 里没有上限，所以这里没有「已满」的判断，只判断资源点够不够。
/// </summary>
public partial class Store
{
    private Button _buyHpButton;

    /// <summary>取节点、接信号、按当前余额着一次色。由 Store._Ready 调用。</summary>
    private void SetupHpShop()
    {
        _buyHpButton = GetNodeOrNull<Button>("BuyHp");
        if (_buyHpButton == null) return;

        _buyHpButton.Text = $"买血 {BattleStateManager.HpPrice}";
        _buyHpButton.MouseEntered += () => AnimateButton(_buyHpButton, HoverScale);
        _buyHpButton.MouseExited += () => AnimateButton(_buyHpButton, 1f);
        _buyHpButton.Pressed += OnBuyHpPressed;

        RefreshBuyHpState();
    }

    /// <summary>
    /// 花固定资源点买 1 点血量。资源点不够时只把按钮闪一下，不做任何扣减。
    /// </summary>
    private void OnBuyHpPressed()
    {
        if (BattleStateManager.MaterialPoints < BattleStateManager.HpPrice)
        {
            GD.Print($"[Store] 资源点不足，无法买血: {BattleStateManager.MaterialPoints} < {BattleStateManager.HpPrice}");
            FlashBuyHp();
            return;
        }

        BattleStateManager.MaterialPoints -= BattleStateManager.HpPrice;
        BattleStateManager.AddHp(1);
        GD.Print($"[Store] 买血成功，血量 {BattleStateManager.Hp}，剩余资源点 {BattleStateManager.MaterialPoints}");

        UpdateMaterialPointsLabel();
        RefreshBuyHpState();
    }

    /// <summary>买血按钮按余额着色：买得起白色、买不起标红。</summary>
    private void RefreshBuyHpState()
    {
        if (_buyHpButton == null) return;
        _buyHpButton.AddThemeColorOverride("font_color",
            BattleStateManager.MaterialPoints >= BattleStateManager.HpPrice ? Colors.White : ColorCantAfford);
    }

    /// <summary>买不起时闪一下按钮（与卡位买不起时的反馈一致）。</summary>
    private void FlashBuyHp()
    {
        if (_buyHpButton == null) return;
        var tween = CreateTween();
        tween.TweenProperty(_buyHpButton, "modulate", ColorCantAfford, 0.1);
        tween.TweenProperty(_buyHpButton, "modulate", Colors.White, 0.3);
    }
}
