using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 卡牌本身类 只用于存储卡牌信息和播放动画
/// </summary>
public partial class cardBase_ : Control
{
    [Export] int height = 0;
    [Export] public IsFriend isFriend = 0;//isfriend 0表示敌方 1表示我方
    [Export] public string id;
    [Export] public string description = "";
    [Export] public int attack = 1;
    [Export] public int defence = 1;
    [Export] public string effect = "";
    [Export] public int cost = 1;
    [Export] public string name = "轻步兵";

                                        // 数据变化闪烁效果相关字段
    private int initialAttack = 1;      // 初始攻击力
    private int initialDefence = 1;     // 初始防御力
    private int initialCost = 1;        // 初始价格
    private int maxHistoryAttack = 1;   // 历史最大攻击力
    private int maxHistoryDefence = 1;  // 历史最大防御力
    private int minHistoryCost = 1;     // 历史最小价格
    private Tween currentFlashTween;    // 当前闪烁动画

    // 悬停高亮和缩放效果
    private Tween hoverTween;
    private Panel hoverHighlight;
    private bool isHovering = false;
    private Vector2 originalScale = Vector2.One;

    [Export] public CardTypes cardType = CardTypes.Infantry;
    [Export] public Rarity rarity = Rarity.Common;
    [Export] public string IconPath = "res://cards/轻步兵.png";
    [Export] public HQ isHq = HQ.normalCard;
    [Export] int moveAble = 0;
    [Export] int attackAble = 0;
    [Export] public TargetType targetType = TargetType.anyTarget;
    
    // 单位特性
    [Export] public UnitTraits traits = UnitTraits.None;
    
    // 特性状态跟踪
    private bool hasSmokeScreen = false;
    private bool hasShock = false;
    
    CardState state;
    Node2D cardBase;
    battlefield_ battleField;

    Texture2D UssrPic;
    Texture2D Germanypic;

                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      /// <summary>
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      /// 卡牌的所在位置,记得要释放
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      /// </summary>
    place_ myPlace;
    int styleInit; //0 未初始化 1 已初始化
    battlefield_ battlefield;

    public void RefreshUnit()
    {
        GD.Print($"RefreshUnit: {this}, Type={cardType}, Before: moveAble={moveAble}, attackAble={attackAble}");
        moveAble = 1;
        attackAble = 1;
        GD.Print($"  After: moveAble={moveAble}, attackAble={attackAble}");
        
        // 奋战特性：单位部署后可立刻战斗
        if (HasTrait(UnitTraits.Determination))
        {
            attackAble = 2; // 可以攻击两次（部署时一次，正常回合一次）
        }
        
    }
    
    /// <summary>
    /// 检查单位是否具有指定特性
    /// </summary>
    public bool HasTrait(UnitTraits trait)
    {
        return (traits & trait) == trait;
    }
    
    /// <summary>
    /// 添加特性
    /// </summary>
    public void AddTrait(UnitTraits trait)
    {
        traits |= trait;
    }
    
    /// <summary>
    /// 移除特性
    /// </summary>
    public void RemoveTrait(UnitTraits trait)
    {
        traits &= ~trait;
    }
    
    /// <summary>
    /// 检查单位是否有烟幕
    /// </summary>
    public bool HasSmokeScreenActive()
    {
        return hasSmokeScreen;
    }
    
    /// <summary>
    /// 移除烟幕
    /// </summary>
    public void RemoveSmokeScreen()
    {
        hasSmokeScreen = false;
    }
    
    /// <summary>
    /// 检查单位是否有冲击
    /// </summary>
    public bool HasShockActive()
    {
        return hasShock;
    }
    
    /// <summary>
    /// 移除冲击
    /// </summary>
    public void RemoveShock()
    {
        hasShock = false;
    }
    


        /// <summary>
        /// 进行移动检查 会进行移动计数
        /// </summary>
        /// <returns></returns>
    public Boolean CheckIfCanMove()
    {
        if(moveAble >= 1){
           return true;
        }
        return false;
    }

    public void HaveMoved()
    {
        moveAble = 0;
    }

    public Boolean CheckIfCanAttack()
    {
        GD.Print($"CheckIfCanAttack: {this}, Type={cardType}, attackAble={attackAble}, moveAble={moveAble}");

        // 移除了将moveAble设置为0的逻辑，因为步兵移动后应该能够攻击
        
        // 检查正常攻击次数
        if (attackAble >= 1)
        {
            return true;
        }
        
        // 检查奋战特性提供的额外攻击次数

        
        GD.Print($"  attackAble < 1, returning false");
        return false;
    }

    public void HaveAttacked()
    {
        attackAble --;
    }

    public int ReadAttackable()
    {
        return attackAble;
    }


/// <summary>
/// 设置自身阵营
/// </summary>
/// <param name="isFriend"></param>
    public void SetIsFriend(IsFriend isFriend)
    {
        switch (isFriend)
        {
            case IsFriend.friend:
                UssrPic = ResourceManager.Instance?.GetTexture("res://cards/ussr.png") ?? GD.Load<Texture2D>("res://cards/ussr.png");
                GetNode<Sprite2D>("country").Texture = UssrPic;
                break;
            case IsFriend.enemy:
                Germanypic = ResourceManager.Instance?.GetTexture("res://cards/Germany.png") ?? GD.Load<Texture2D>("res://cards/Germany.png");
                GetNode<Sprite2D>("country").Texture = Germanypic;
                break;
        }

        this.isFriend = isFriend;
    }

/// <summary>
/// 获取自身阵营
/// </summary>
/// <returns></returns>
    public IsFriend GetIsFriend()
    {
        return isFriend;
    }


    public void AddChange(ChangeType type, int value)
    {
        ChangeList.Add(new Change(type, value));
    }

    List<Change> ChangeList = new List<Change>();
    public void ExecChangeList()
    {
        foreach (var change in ChangeList)
        {
            switch (change.Type)
            {
                case ChangeType.GetAttack:
                    GetAttack(change.Value);
                    break;
                case ChangeType.LoseAttack:
                    LoseAttack(change.Value);
                    break;
                case ChangeType.GetDefence:
                    GetDefence(change.Value);
                    break;
                case ChangeType.LoseDefence:
                    LoseDefence(change.Value);
                    break;
                case ChangeType.SetDefence:
                    SetDefence(change.Value);
                    break;
                
            }
        }
        ChangeList.Clear();
    }

/// <summary>
/// 获得防御力
/// </summary>
/// <param name="n"></param>
    public void GetDefence(int n)
    {
        if(defence+n <= 99) defence += n;
        else defence = 99; 
        // 更新历史最大值并触发闪烁效果
        if (defence > maxHistoryDefence) maxHistoryDefence = defence;
        FlashAttributeWithColor("defence", defence, initialDefence, maxHistoryDefence, isInverted: false);
        RefreshState();
    }

/// <summary>
/// 设置防御力为指定值
/// </summary>
/// <param name="n">要设置的防御力值</param>
    public void SetDefence(int n)
    {
        if(n <= 99) defence = n;
        else defence = 99;
        // 更新历史最大值并触发闪烁效果
        if (defence > maxHistoryDefence) maxHistoryDefence = defence;
        FlashAttributeWithColor("defence", defence, initialDefence, maxHistoryDefence, isInverted: false);
        RefreshState();
    }

/// <summary>
/// 失去防御力
/// </summary>
/// <param name="n"></param>
    public async Task LoseDefence(int n)
    {
        defence -= n;
        // 触发闪烁效果
        if(defence > 0 )FlashAttributeWithColor("defence", defence, initialDefence, maxHistoryDefence, isInverted: false);
        RefreshState();
    }

/// <summary>
/// 增加攻击力
/// </summary>
/// <param name="n"></param>
    public void GetAttack(int n)
    {
        if (attack + n <= 99) attack += n;
        else attack = 99;
        // 更新历史最大值并触发闪烁效果
        if (attack > maxHistoryAttack) maxHistoryAttack = attack;
        FlashAttributeWithColor("attack", attack, initialAttack, maxHistoryAttack, isInverted: false);
        RefreshState();
    }

/// <summary>
/// 减少攻击力
/// </summary>
/// <param name="n"></param>
    public void LoseAttack(int n)
    {
        if (attack - n >= 0) attack -= n;
        else attack = 0;
        // 触发闪烁效果
        FlashAttributeWithColor("attack", attack, initialAttack, maxHistoryAttack, isInverted: false);
        RefreshState();
    }

/// <summary>
/// 读取防御力
/// </summary>
/// <returns></returns>
    public int ReadDefence()
    {
        return defence;
    }

    /// <summary>
    /// 读取历史最大防御力
    /// </summary>
    /// <returns></returns>
    public int ReadMaxHistoryDefence()
    {
        return maxHistoryDefence;
    }

/// <summary>
/// 读取攻击力
/// </summary>
/// <returns></returns>
    public int ReadAttack()
    {
        return attack;
    }


    /// <summary>
    /// 读取价格
    /// </summary>
    public int ReadCost()
    {
        return cost;
    }

    /// <summary>
    /// 增加价格
    /// </summary>
    public void AddCost(int n)
    {
        if (cost + n <= 99) cost += n;
        else cost = 99;
        // 触发闪烁效果（价格越小越好，所以isInverted=true）
        FlashAttributeWithColor("cost", cost, initialCost, minHistoryCost, isInverted: true);
        RefreshState();
    }

    /// <summary>
    /// 减少价格
    /// </summary>
    public void ReduceCost(int n)
    {
        if (cost - n >= 0) cost -= n;
        else cost = 0;
        // 更新历史最小值并触发闪烁效果
        if (cost < minHistoryCost) minHistoryCost = cost;
        FlashAttributeWithColor("cost", cost, initialCost, minHistoryCost, isInverted: true);
        RefreshState();
    }

    public override void _Ready()
    {
        height               = 25;
        state                = CardState.inHand;
        MouseFilter          = MouseFilterEnum.Stop;
        cardBase             = GetNode<Node2D>("Cardbase");
        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
        cardBase.Visible     = false;

        battleField = GetTree().Root.GetNode<battlefield_>("BattleField");
        attackAble = 0;
        moveAble = 0;
        ZIndex = 10;

        UssrPic = GD.Load<Texture2D>("res://cards/ussr.png");
        Germanypic = GD.Load<Texture2D>("res://cards/Germany.png");


        if (this.isHq == HQ.hq && styleInit == 0)
        {
            GetNode<Label>("defence").Position = new Vector2(75, 120);
            GetNode<Label>("attack").Visible = false;
            GetNode<Sprite2D>("icon").Visible = false;
            GetNode<Sprite2D>("unitType").Visible = false;
            GetNode<Label>("cost").Visible = false;
            GetNode<Label>("name").Visible = false;
            GetNode<RichTextLabel>("description").Position -= new Vector2(0, -20);
            styleInit = 1;

        }

        if (this.cardType == CardTypes.Command)
        {
            GetNode<Label>("defence").Visible = false;
            GetNode<Label>("attack").Visible = false;
            GetNode<Sprite2D>("cardbase").Texture = GD.Load<Texture2D>("res://cards/卡背_command.png");
        }

        battleField = GetTree().Root.GetNode<battlefield_>("BattleField");

        
    }


    public void SetHqImage(int number)
    {
        switch (number)
        {
            case 0: 
                GetNode<Sprite2D>("cardbase").Texture = ResourceManager.Instance?.GetTexture("res://cards/HQ_moscow.png") ?? GD.Load<Texture2D>("res://cards/HQ_moscow.png");
                break;
            case 1:
                GetNode<Sprite2D>("cardbase").Texture = ResourceManager.Instance?.GetTexture("res://cards/HQ_berlin.png") ?? GD.Load<Texture2D>("res://cards/HQ_berlin.png");
                break;
        }
    }


/// <summary>
/// 设置卡牌信息
/// </summary>
/// <param name="cardData"></param>
    public void SetCardInformation(CardData cardData)
    {

        name        = cardData.Name;
        attack      = cardData.Attack;
        defence     = cardData.Defense;
        effect      = cardData.Effect;
        cost        = cardData.Cost;
        cardType    = cardData.CardType;
        rarity      = cardData.Rarity;
        IconPath    = cardData.IconPath;
        description = cardData.Description;
        cardType    = cardData.CardType;
        isHq        = cardData.IsHq;
        targetType  = cardData.TargetType;
        traits      = cardData.Traits;
        id = cardData.Id;
        
        // 初始化烟幕和冲击状态
        hasSmokeScreen = HasTrait(UnitTraits.SmokeScreen);
        hasShock = HasTrait(UnitTraits.Shock);
        
        // 初始化历史追踪值
        initialAttack = attack;
        initialDefence = defence;
        initialCost = cost;
        maxHistoryAttack = attack;
        maxHistoryDefence = defence;
        minHistoryCost = cost;
        
        RefreshState();
        
    }


/// <summary>
/// 将内存中的状态和现实出来的刷新一下，一般用于卡牌信息改变的时候
/// </summary>
    public void RefreshState()
    {
        if(isHq != HQ.hq)
        {
            if (FileAccess.FileExists(IconPath))
                GetNode<Sprite2D>("icon").Texture = ResourceManager.Instance?.GetTexture(IconPath) ?? GD.Load<Texture2D>(IconPath);
        }
        else
        {
            if (FileAccess.FileExists(IconPath))
                GetNode<Sprite2D>("cardbase").Texture = ResourceManager.Instance?.GetTexture(IconPath) ?? GD.Load<Texture2D>(IconPath);
        }
        if                     (name!= null)
        {
            var nameLabel = GetNode<Label>("name");
            nameLabel.Text = name;
            // 只在第一次调用时调整字体大小
            if (nameLabel.GetMeta("fontSizeInitialized", false).AsBool() == false)
            {
                AdjustFontSizeToFit(nameLabel);
                nameLabel.SetMeta("fontSizeInitialized", true);
            }
        }
        GetNode<Label>         ("attack").Text                                                     = attack.ToString();
        GetNode<Label>         ("defence").Text                                                    = defence.ToString();
        GetNode<Label>         ("cost").Text                                                       = cost.ToString();
        if                     (description!= null)
        {
            var descriptionLabel = GetNode<RichTextLabel>("description");
            descriptionLabel.Text = description;
            // 只在第一次调用时调整字体大小
            if (descriptionLabel.GetMeta("fontSizeInitialized", false).AsBool() == false)
            {
                AdjustRichTextFontSizeToFit(descriptionLabel);
                descriptionLabel.SetMeta("fontSizeInitialized", true);
            }
        }
        OnRefreshUnitType((int)cardType);

    }

    /// <summary>
    /// 调整 Label 字体大小以适应容器（尽可能大且不超出边框）。
    /// 使用二分法减少测量次数，避免在卡牌大量刷新时出现卡顿。
    /// </summary>
    private void AdjustFontSizeToFit(Label label)
    {
        if (label == null) return;

        Vector2 containerSize = label.Size;
        if (containerSize.X <= 0 || containerSize.Y <= 0) return;

        Font font = label.GetThemeFont("font");
        if (font == null) return;

        string text = label.Text;
        if (string.IsNullOrEmpty(text)) return;

        int maxSize = label.GetThemeFontSize("font_size");
        if (maxSize <= 0) maxSize = 12;

        int bestSize = FindBestFontSizeForLabel(label, font, text, 8, maxSize, containerSize);
        label.AddThemeFontSizeOverride("font_size", bestSize);
    }

    private int FindBestFontSizeForLabel(Label label, Font font, string text, int minSize, int maxSize, Vector2 containerSize)
    {
        int low = minSize;
        int high = maxSize;
        int best = minSize;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, containerSize.X, mid);

            if (size.X <= containerSize.X && size.Y <= containerSize.Y)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }

    /// <summary>
    /// 调整 RichTextLabel 字体大小以适应容器（尽可能大且不超出边框）。
    /// 使用二分搜索，避免逐步缩小造成的多次重绘卡顿。
    /// </summary>
    private void AdjustRichTextFontSizeToFit(RichTextLabel label)
    {
        if (label == null) return;

        Vector2 containerSize = label.Size;
        if (containerSize.X <= 0 || containerSize.Y <= 0) return;

        string text = label.Text;
        if (string.IsNullOrEmpty(text)) return;

        int maxSize = label.GetThemeFontSize("normal_font_size");
        if (maxSize <= 0) maxSize = 10;

        int bestSize = FindBestFontSizeForRichText(label, 8, maxSize, containerSize.Y);
        label.AddThemeFontSizeOverride("normal_font_size", bestSize);
    }

    private int FindBestFontSizeForRichText(RichTextLabel label, int minSize, int maxSize, float containerHeight)
    {
        int low = minSize;
        int high = maxSize;
        int best = minSize;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            label.AddThemeFontSizeOverride("normal_font_size", mid);
            label.FitContent = true;

            float contentHeight = label.GetContentHeight();
            if (contentHeight <= containerHeight)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }

    public void OnRefreshUnitType(int x)
    {
        Sprite2D unitType = GetNode<Sprite2D>("unitType");
        var _cardType = (CardTypes)x;
        unitType.Texture = _cardType switch
        {
            CardTypes.Tank => ResourceManager.Instance?.GetTexture("res://cards/tank.png") ?? GD.Load<Texture2D>("res://cards/tank.png"),
            CardTypes.Plane => ResourceManager.Instance?.GetTexture("res://cards/fighter.png") ?? GD.Load<Texture2D>("res://cards/fighter.png"),
            CardTypes.Bomber => ResourceManager.Instance?.GetTexture("res://cards/bomber.png") ?? GD.Load<Texture2D>("res://cards/bomber.png"),
            CardTypes.Artillery => ResourceManager.Instance?.GetTexture("res://cards/arlitery.png") ?? GD.Load<Texture2D>("res://cards/arlitery.png"),
            CardTypes.Command => ResourceManager.Instance?.GetTexture("res://cards/command.png") ?? GD.Load<Texture2D>("res://cards/command.png"),
            _ => ResourceManager.Instance?.GetTexture("res://cards/inf.png") ?? GD.Load<Texture2D>("res://cards/inf.png"),
        };
    }

    /// <summary>
    /// 将卡牌设置为黑白（灰度）状态
    /// </summary>
    public void SetGrayscale()
    {
        // 使用 Modulate 将卡牌变成黑白：将颜色调整为灰度
        // 方式：设置 Modulate 为灰色（R=G=B，A=1.0）
        Modulate = new Color(0.6f, 0.6f, 0.6f, 1.0f);
    }

    /// <summary>
    /// 恢复卡牌的原始颜色（还原黑白状态）
    /// </summary>
    public void RestoreColor()
    {
        // 恢复 Modulate 为白色（正常状态）
        Modulate = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    }

    public place_ GetMyPlace()
    {
        return myPlace;
    }

/// <summary>
/// 重新绑定自己的格子
/// </summary>
/// <param name="place"></param>
    public void SetMyPlace(place_ place)
    {
        if(myPlace != null)
        {
            myPlace.UnbondCard();
        }
        myPlace = place;
        myPlace.BondCard(this);
    }

/// <summary>
/// 死亡函数这块
/// </summary> 
    public void Dead()
    {
        if (myPlace != null)
        {
            myPlace.UnbondCard();
            myPlace = null;
        }

        state = CardState.destroyed;

        // 如果启用了资源池，则返回到池中，否则直接销毁
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ReleaseEmptyCard(this);
        }
        else
        {
            this.QueueFree();
        }
    }


    /// <summary>
    /// 返回卡牌的当前状态
    /// </summary>
    /// <returns>当前状态</returns>
    public CardState getState()
    {
        return state;
    }

    /// <summary>
    /// 设置卡牌的当前状态
    /// </summary>
    /// <param name="state">目标状态</param>
    public void setState(CardState state)
    {
        this.state = state;
    }

    public Boolean CheckIfPointIsIn(Vector2 point)
    {
        return _HasPoint(point);
    }

    //以下为动画区域===============================================================================================

    /// <summary>
    /// 移动到目标地点的动画 异步
    /// </summary>
    /// <param name="destination">目标地点</param>
    /// <param name="duration">时长</param>
    /// <returns></returns>
    async public Task MoveToPosition(Vector2 destination, float duration = 0.5f)
    {
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(this, "position", destination, duration);

        // 等待 Tween 完成
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>
    /// 弃置卡牌动画 - 从左侧进入、停留3s、旋转着向左侧飞出并删除
    /// </summary>
    /// <returns></returns>
    async public Task DiscardCard()
    {
        // 设置旋转中心为卡牌中心 (宽180/2=90, 高240/2=120)
        var Offset = new Vector2(90, 120);
        
        // 获取屏幕参数
        Vector2 screenSize = GetViewportRect().Size;
        
        // 计算左侧屏幕中间位置（以中心计算，因为Offset已设置）
        Vector2 leftScreenPos = new Vector2(100, screenSize.Y / 2 - 90);  // 屏幕左侧中间
        
        // 步骤1: 从左侧飞入，停在屏幕左侧中间 (1秒)
        var enterTween = CreateTween();
        enterTween.SetTrans(Tween.TransitionType.Quad);
        enterTween.SetEase(Tween.EaseType.Out);
        enterTween.TweenProperty(this, "global_position", leftScreenPos, 1.0f);
        await ToSignal(enterTween, Tween.SignalName.Finished);
        
        // 步骤2: 正着停留3秒（不动不转）
        await Task.Delay(1000);
        
                                                                     // 步骤3: 旋转着向左侧飞出 (1.5秒)
        Vector2 exitPosition = new Vector2(-1400, screenSize.Y / 2);  // 飞出到左侧
        var exitTween = CreateTween();
        exitTween.SetTrans(Tween.TransitionType.Quad);
        exitTween.SetEase(Tween.EaseType.In);
        exitTween.SetParallel(true); // 并行执行以下动画
        exitTween.TweenProperty(this, "global_position", exitPosition, 1.5f);
        exitTween.TweenProperty(this, "rotation", Mathf.Pi * 4, 1.5f); // 旋转两圈
        await ToSignal(exitTween, Tween.SignalName.Finished);
    }

      /// <summary>
      /// 属性数据变化时的闪烁和颜色变化效果
      /// 对于attack和defence（越大越好）：
      ///   - 等于初始值：黑色
      ///   - >= 极值：绿色
      ///   - < 极值：红色
      /// 对于cost（越小越好）：
      ///   - 等于初始值：黑色
      ///   - <= 极值：绿色
      ///   - > 极值：红色
      /// </summary>
    private async void FlashAttributeWithColor(string attributeName, int currentValue, int initialValue, int extremeValue, bool isInverted = false)
    {
          // 根据属性名获取对应的Label节点
        Label targetLabel = attributeName switch
        {
            "attack"  => GetNode<Label>("attack"),
            "defence" => GetNode<Label>("defence"),
            "cost"    => GetNode<Label>("cost"),
            _         => null
        };

        if (targetLabel == null)
            return;

          // 根据当前值、初始值和极值确定颜色
        Color targetColor;
        if (currentValue == initialValue)
        {
            targetColor = Colors.White;  // 等于初始值 - 黑色
        }
        else if (!isInverted)
        {
              // 非反向：越大越好（attack, defence）
            if (currentValue >= extremeValue)
                targetColor = Colors.Green;  // >= 极值（最大值） - 绿色
            else
                targetColor = Colors.Red;  // < 极值 - 红色
        }
        else
        {
              // 反向：越小越好（cost）
            if (currentValue <= extremeValue)
                targetColor = Colors.Green;  // <= 极值（最小值） - 绿色
            else
                targetColor = Colors.Red;  // > 极值 - 红色
        }

        GD.Print($"闪烁 {attributeName}: 目标颜色 = {targetColor}, 当前值 = {currentValue}");

        for(int i = 0; i < 3; i++)
        {
            // 检查对象是否已被释放
            if (!IsInstanceValid(this) || !IsInstanceValid(targetLabel))
                return;
            
            targetLabel.LabelSettings.FontColor = targetColor;
            await Task.Delay(100);
            
            // 再次检查对象是否已被释放
            if (!IsInstanceValid(this) || !IsInstanceValid(targetLabel))
                return;
            
            targetLabel.LabelSettings.FontColor = Colors.White;
            await Task.Delay(100);
        }
        
        // 最后一次检查
        if (!IsInstanceValid(this) || !IsInstanceValid(targetLabel))
            return;
        
        targetLabel.LabelSettings.FontColor = targetColor;


            // 创建闪烁动画：快速变亮再变暗
    }

    async public Task AttackInf(cardBase_ target)
    {
        
    }

}

public enum HQ
{
    normalCard,
    hq,
    command
}

public enum CardTypes
{
    Plane,
    Bomber,
    Tank,
    Infantry,
    Artillery,
    Command,
}

/// <summary>
/// 单位特性枚举
/// </summary>
[Flags]
public enum UnitTraits
{
    None = 0,
    /// <summary>闪击：单位部署后可立刻战斗</summary>
    Blitz = 1 << 0,
    /// <summary>奋战：单位可再战斗1次</summary>
    Determination = 1 << 1,
    /// <summary>重甲：单位受到的战斗伤害-1</summary>
    HeavyArmor = 1 << 2,
    /// <summary>烟幕：第一次移动/攻击之前不能成为攻击的目标</summary>
    SmokeScreen = 1 << 3,
    /// <summary>守护：在此单位左侧和右侧的目标不能成为攻击的目标</summary>
    Guardian = 1 << 4,
    /// <summary>冲击：攻击时不受到反击，攻击后失去冲击</summary>
    Shock = 1 << 5,
    /// <summary>伏击：被攻击时先造成反击伤害</summary>
    Ambush = 1 << 6,
    /// <summary>免疫：不受到战斗伤害</summary>
    Immunity = 1 << 7
}



public enum Rarity
{
    Common,
    Rare,
    Epic,
    Legendary,
    Unobtainable
}

public enum Stage
{
    Prepare,
    Draw,
    Battle,
    End,
    EnemyPrepare,
    EnemyDraw,
    EnemyBattle,
    EnemyEnd
}

public enum CardState
{
    inHand,
    caught,
    placed,
    played,
    attack,
    beAttacked,
    destroyed,
    inplaceAndCaught,
    commandCardCaught,
}

public enum Times
{
    played,
    attack,
    beingAttack,
    dead,
    deployed,
    none,
    aFriendlyUnitDeployed,
    aFriendlyUnitAttacked,
    aFriendlyUnitBeingAttacked,
    aFriendlyUnitDead,
    aEnemyUnitDeployed,
    aEnemyUnitAttacked,
    aEnemyUnitBeingAttacked,
    aEnemyUnitDead,
    myHqBeingAttacked,
    enemyHqBeingAttacked,
    // 新增时点
    friendlyTankDeployed,      // 友方坦克部署时
    friendlyTurnBegin,         // 友方回合开始时
    enemyTurnBegin,            // 敌方回合开始时
    becomingAttackTarget,      // 成为敌方攻击的目标时
    friendlyInfantryDeployed,  // 友方步兵部署/加入时
    fightingInfantry,          // 对战步兵时
    attackingHq,               // 攻击总部时
    takingDamage               // 本单位收到伤害时
}

//储存卡牌信息
public partial class CardData : Resource
{
    // 基础信息
    [Export] public string Id { get; set; } = "";
    [Export] public HQ IsHq = HQ.normalCard;
    [Export] public string Name { get; set; } = "Unknown Card";
    [Export] public string Description { get; set; } = "";

    // 核心属性
    [Export] public int Attack { get; set; } = 1;
    [Export] public int Defense { get; set; } = 1;
    [Export] public int Cost { get; set; } = 1;
    [Export] public string Effect { get; set; } = "";

    // 元数据
    [Export] public CardTypes CardType { get; set; } = CardTypes.Infantry; // plane bomber tank infantry artillery
    [Export] public Rarity Rarity { get; set; } = Rarity.Common;   // "Common", "Rare", "Epic", "Legendary"
    [Export] public UnitTraits Traits { get; set; } = UnitTraits.None; // 单位特性

    // 资源引用
    [Export] public Texture2D Icon { get; set; }
    [Export] public string IconPath { get; set; } = "";  // 用于加载时暂存路径
    [Export] public TargetType TargetType { get; set; } = TargetType.anyTarget;


}


public enum IsFriend
{
    friend,
    enemy,
    neutral,
    enemyNeutral
}

public enum ChangeType
{
    GetAttack,
    LoseAttack,
    GetDefence,
    LoseDefence,
    SetDefence,

}

public enum TargetType
{
    aPlace,
    aUnit,
    aFriendlyUnit,
    anEnemyUnit,
    myHq,
    enemyHq,
    anyTarget,
    friendlyTarget,
    enemyTarget,
    NOTarget
}

struct Change
{
    public ChangeType Type;
    public int Value;

    public Change(ChangeType type, int value)
    {
        Type = type;
        Value = value;
    }
}
