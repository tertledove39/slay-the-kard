using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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

/// <summary>
/// 给定一个若干张卡组成的列表 重新按照从上到下从左到右排序
/// </summary>
/// <param name="list"></param>
/// <returns></returns>
    List<cardBase_> SortCardList(List<cardBase_> list)
    {
        var sortedCards = list
            .OrderBy(list => list.GlobalPosition.Y)   // 先按 Y（行）
            .ThenBy(list => list.GlobalPosition.X)    // 再按 X（列）
            .ToList();
        return sortedCards;
    }
/// <summary>
/// 返回给定全局坐标应当返回的第一张卡
/// </summary>
/// <returns></returns>
    cardBase_ CheckCardClick(Godot.Vector2 mousePosition)
    {

        if(cardInPlaces== null) return null;
        foreach (var card in cardInPlaces)
        {
            if (card.GetGlobalRect().HasPoint(mousePosition))
            {
                // 点击到了卡牌
                return card;
            }
        }
        return null;
    }

    public override void _Ready()
    {
        cardInPlaces = new List<cardBase_>();
    }

    cardBase_ cardNowChoose = null;
    Godot.Vector2 offset;
    public override void _Input(InputEvent @event)
    {
         if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed)
            {
                // 点击事件
                var mousePosition = GetGlobalMousePosition();
                var card = CheckCardClick(mousePosition);
                if (card != null)
                {
                    offset = card.GetGlobalPosition() - mousePosition;
                    cardNowChoose = card;
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
        card.SetPosition(new Godot.Vector2(100, 100));
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
