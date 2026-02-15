using Godot;
using System;
using System.Collections.Generic;

public partial class battlefield_ : Control
{

    PackedScene cardRes = ResourceLoader.Load<PackedScene>("res://bin/cardbase.tscn");
    

    /// <summary>
    /// 全局 储存所有场上的卡
    /// </summary>
    private List<cardBase_> cardInPlaces;

    

    /// <summary>
    /// 读取场上所有卡构成的列表
    /// </summary>
    /// <returns></returns>
    public List<cardBase_> ReadCardInPlaces()
    {
        return cardInPlaces;
    }

    /// <summary>
    /// 设置场上所有卡所构成的列表 慎用!
    /// </summary>
    /// <param name="value"></param>
    public void SetCardInPlaces(List<cardBase_> value)
    {
        cardInPlaces = value;
    }

    /// <summary>
    /// 支援阵线
    /// </summary>
    public List<place_> supportLine = [];

    /// <summary>
    /// 前线
    /// </summary>
    public List<place_> frontLine = [];

    /// <summary>
    /// 敌方阵线
    /// </summary>
    public List<place_> enemySupprotLine = [];

    /// <summary>
    /// 抽牌堆
    /// </summary>
    private List<cardBase_> deck;

    public override void _Ready()
    {
        cardInPlaces = new List<cardBase_>();
    }

    cardBase_ cardNowChoose = null;
    Vector2 offset;
    public override void _Input(InputEvent @event)
    {
         if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed)
            {
                // 点击事件
                var mousePosition = GetGlobalMousePosition();
                    foreach (var card in cardInPlaces)
                        {
                            if (card.GetGlobalRect().HasPoint(mousePosition))
                            {
                                // 点击到了卡牌
                                card.setState(CardState.caught);
                                cardNowChoose = card;
                                offset = card.GetGlobalPosition() - mousePosition;
                                return;
                            }
                }
                
            }
            if (mouseButton.Pressed==false)
            {
                cardNowChoose = null;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (Input.IsMouseButtonPressed(MouseButton.Left) && cardNowChoose != null)
        {
            cardNowChoose.SetGlobalPosition(GetGlobalMousePosition()+offset);
        }
    }




/// <summary>
/// 暂时用作测试
/// </summary>
    public void OnNextTurnButtonPressed()
    {
        var card = cardRes.Instantiate() as cardBase_;
        card.SetPosition(new Vector2(100, 100));
        AddChild(card);
        cardInPlaces.Add(card);
        
    }

}

/// <summary>
/// 玩家 手牌和状态记录在这里
/// </summary>
public class Player
{
    private List<cardBase_> cardsInHand;
}


/// <summary>
/// 管理所有卡牌 用于加载卡
/// </summary>
public class CardMaganer
{
    
}
