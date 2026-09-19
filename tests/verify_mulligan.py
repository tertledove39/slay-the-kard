#!/usr/bin/env python3
"""起手换牌验证。

规则：开局抽 5 张平铺在屏幕前，玩家点选要换掉的牌（可换任意张，含一张不换或全换），
确认后选中的牌洗回牌库并补抽等量张，新牌在原位展示后随全部手牌归位，然后进入游戏。
"""
import re
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(condition, message):
    print(f"[{'PASS' if condition else 'FAIL'}] {message}")
    return condition


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def main():
    battle = read("bin/battlefield_.cs")
    screen = read("bin/MulliganScreen.cs")
    scene = (ROOT / "bin" / "mulligan_screen.tscn")

    mulligan_start = screen.split("public partial class MulliganScreen")[1]
    apply_body = mulligan_start.split("private async Task ApplyMulligan()")[1].split("private void ClearMarkers()")[0]

    # ------------------------------ 冒烟测试 ------------------------------
    print("--- 冒烟测试 ---")
    smoke = [
        check(scene.exists(), "mulligan_screen.tscn 存在"),
        check((ROOT / "bin" / "MulliganScreen.cs").exists(), "MulliganScreen.cs 存在"),
        check('private const int OpeningHandSize = 5;' in battle, "开局抽牌数收敛为具名常量，不再是魔鬼数字"),
        check("await player1.DrawCard(OpeningHandSize);" in battle, "开局按常量抽起手牌"),
        check("player1.DrawCard(5)" not in battle, "不再有写死的抽 5 张"),
        check("await MulliganScreen.ShowAsync(this, player1);" in battle, "抽完起手牌后进入换牌界面"),
    ]

    # --------------------------- 平铺与点选 ---------------------------
    print("\n--- 平铺与点选 ---")
    layout = [
        check("card.Reparent(_cardLayer);" in screen, "起手牌被移进覆盖层接管显示"),
        check("private async Task DealIn()" in screen, "提供发牌飞入动画"),
        check("SlotPosition(i)" in screen and "private Vector2 SlotPosition(int index)" in screen,
              "按序号计算平铺落位"),
        check("(CardScale - 1f) * (CardW / 2f)" in screen,
              "落位补偿了以中心为轴缩放带来的偏移，整排不会偏"),
        check("private void Toggle(cardBase_ card)" in screen, "点击可切换该牌的选中状态"),
        check("if (!_selected.Remove(card)) _selected.Add(card);" in screen,
              "选中是开关式，不限制张数（可换任意张）"),
        check("_pickCount" not in screen, "没有张数上限"),
    ]

    # --------------------------- X 标记 ---------------------------
    print("\n--- X 标记 ---")
    marker = [
        check('MarkerTexturePath = "res://assest/dead.png"' in screen,
              "标记使用占位图（assest 无叉号素材，暂用 dead.png）"),
        check("marker.Visible = _selected.Contains(card);" in screen, "选中时显示标记，取消时隐藏"),
        check("MouseFilter = MouseFilterEnum.Ignore," in screen, "标记不拦截鼠标，点击仍由点击层处理"),
    ]

    # --------------------------- 换牌与补抽 ---------------------------
    print("\n--- 换牌与补抽 ---")
    exchange = [
        check("public async Task<List<cardBase_>> MulliganAsync(List<cardBase_> returned)" in battle,
              "Player 提供换牌方法"),
        check("if (card == null || !cardsInHand.Remove(card)) continue;" in battle,
              "换掉的牌从手牌移出"),
        check("deck.Add(card);" in battle, "换掉的牌回到牌库"),
        check(battle.index("ShuffleDeck();", battle.index("MulliganAsync")) <
              battle.index("await DrawCard(count);", battle.index("MulliganAsync")),
              "先洗回整副牌库再补抽（炉石做法）"),
        check("await _player.MulliganAsync(returned);" in apply_body, "界面确认后调用换牌"),
        check("_cards[slots[i]] = card;" in apply_body, "新牌补进被换掉的那几张原来的位置"),
        check("await card.MoveToPosition(SlotPosition(slots[i]), FlyInDuration);" in apply_body,
              "新牌飞入并原位展示"),
        check("RevealHoldSeconds" in apply_body, "补抽后有停留时间，让玩家看清换到了什么"),
    ]

    # --------------------------- 一次收束 ---------------------------
    print("\n--- 一次收束 ---")
    single = [
        check("await _confirmRequested.Task;" in mulligan_start, "等待玩家点确认"),
        check("_confirmButton.Disabled = true;" in mulligan_start, "确认后禁用按钮，不会二次换牌"),
        check("if (returned.Count == 0) return;" in apply_body,
              "一张不选时直接进入游戏，不做任何换牌"),
        check("_confirmRequested?.TrySetResult(true);" in screen, "确认按钮只结算一次"),
    ]

    # --------------------------- 关键回归：卡牌所有权 ---------------------------
    print("\n--- 关键回归：卡牌所有权 ---")
    ownership = [
        check("card.GetParent() != _field) card.Reparent(_field);" in screen,
              "结束时把牌还给战场，否则会随覆盖层一起被销毁"),
        check("foreach (var card in returned)" in apply_body and
              "card.Scale = Vector2.One;" in apply_body,
              "被换掉的牌先还回战场（此时已进牌库、不在手牌里）"),
        check("private readonly List<Node> _overlays = new();" in screen,
              "显式记录本界面加到卡牌上的子节点"),
        check("card is ColorRect || card is TextureRect" not in screen,
              "不按类型遍历卡牌子节点删除——卡牌自身的美术资源也是 TextureRect"),
        check("player.RefreshMyHand();" in mulligan_start, "收尾刷新手牌布局"),
    ]

    # RefreshMyHand 必须跳过已 reparent 的卡，否则补抽会把屏幕上的牌拉回手牌区
    refresh = battle.split("public void RefreshMyHand()")[1].split("public void UpdateHover")[0]
    ownership.append(check("if (cardsInHand[i].GetParent() != battlefield)" in refresh,
                           "RefreshMyHand 跳过已 reparent 的卡（补抽不会打断屏幕展示）"))

    # RefreshAllCardDisplayOrder 每帧从 _Process 调用，同样必须跳过已 reparent 的卡。
    # 该函数里的手牌循环原先没有守卫，换牌期间会报
    # "Child is not a child of this node"（scene/main/node.cpp:487 move_child）。
    display_order = battle.split("void RefreshAllCardDisplayOrder()")[1].split("Boolean CheckIfThePlaceIsOccupied")[0]
    hand_loop = display_order.split("var handCards = player1.GetCardsInHand();")[1]
    hand_loop = hand_loop[:hand_loop.index("MoveChild(")]
    ownership.append(check("GetParent() != this" in hand_loop,
                           "手牌顺序刷新对已 reparent 的卡有守卫（否则换牌期间每帧报错）"))

    # 通用约束：本文件每一处 MoveChild 之前都必须先确认父子关系
    unguarded = []
    for m in re.finditer(r"MoveChild\(", battle):
        if "GetParent()" not in battle[max(0, m.start() - 400):m.start()]:
            line_no = battle[:m.start()].count("\n") + 1
            unguarded.append(line_no)
    ownership.append(check(not unguarded,
                           f"battlefield_ 中每处 MoveChild 都先校验了父子关系（未校验的行号：{unguarded}）"))

    # --------------------------- 健壮性 ---------------------------
    print("\n--- 健壮性 ---")
    robust = [
        check("ForbidControl();" in battle.split("private async Task StartOpeningHandAsync()")[1].split("private async Task")[0],
              "换牌期间锁住战场操作"),
        check("finally" in battle.split("private async Task StartOpeningHandAsync()")[1].split("/// <summary>")[0],
              "换牌流程用 finally 解锁，出异常也不会把玩家卡死"),
        check("AllowControl();" in battle.split("private async Task StartOpeningHandAsync()")[1].split("/// <summary>")[0],
              "流程结束解锁操作"),
        check("if (player.GetCardsInHand().Count == 0) return;" in screen, "手牌为空时不显示界面"),
        check("Callable.From(() => catcher.MouseFilter = MouseFilterEnum.Stop).CallDeferred();" in screen,
              "点击层延迟启用，避免打开界面那一次点击被立刻吃掉"),
    ]

    # 卡牌尺寸不被改动，靠缩放放大，避免卡内布局被拉伸
    robust.append(check("card.Size = new Vector2(CardW, CardH);" in screen and
                        "card.Scale = new Vector2(CardScale, CardScale);" in screen,
                        "沿用设计尺寸 180x240，靠缩放放大而非改 Size"))

    # --------------------------- 场景结构 ---------------------------
    print("\n--- 场景结构 ---")
    tscn = scene.read_text(encoding="utf-8")
    structure = [
        check('path="res://bin/MulliganScreen.cs"' in tscn, "场景挂载 MulliganScreen 脚本"),
        check("uid=" not in tscn, "手写场景不写 uid（沿用 settlement_panel.tscn 等先例）"),
        check('[node name="CardLayer" type="Control" parent="."]' in tscn, "场景含 CardLayer"),
        check('[node name="ConfirmButton" type="Button" parent="."]' in tscn, "场景含 ConfirmButton"),
        check(tscn.index('name="Dim"') < tscn.index('name="CardLayer"'),
              "遮罩排在卡牌层之前，卡牌不会被遮罩盖住"),
        check("parent=\"CardLayer\"" not in tscn, "卡牌由代码动态挂入 CardLayer"),
    ]

    results = smoke + layout + marker + exchange + single + ownership + robust + structure
    failed = results.count(False)
    print(f"\nResult: {len(results) - failed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
