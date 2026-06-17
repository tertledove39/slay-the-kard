
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
    private bool hasMobilize = false;
    private bool hasAmbushActive = false;
    // 被守护状态（由两侧守护单位提供，不是自身trait）
    private bool isBeGuardianed = false;

    // 单位存活回合数
    private int lifeTime = 0;
    
    CardState state;
    Node2D cardBase;
    battlefield_ battleField;

    Texture2D UssrPic;
    Texture2D Germanypic;

    // 移动/攻击状态指示灯
    private Sprite2D moveableLight;
    private Texture2D greenLightTex;
    private Texture2D yellowLightTex;
    private Texture2D redLightTex;

    // 效果/trait的attribute图标面板
    private Control _attrPanel;
    private Panel _attrTooltipPanel;
    private Label _attrTooltipLabel;
    private List<Rect2> _attrIconRects = new(); // 图标在_attrPanel中的本地rect
    private List<string> _attrIconDescs = new();
    private Dictionary<string, TextureRect> _attrIconWidgets = new(); // trait名→图标控件
    private const int AttrIconSize = 22;
    private const int AttrMaxVisible = 5;

    // 缓存卡名和标签尺寸，避免每次刷新都重新计算字体大小
    private string lastNameText = string.Empty;
    private Vector2 lastNameLabelSize = Vector2.Zero;

                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      /// <summary>
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      /// 卡牌的所在位置,记得要释放
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      /// </summary>
    place_ myPlace;
    int styleInit; //0 未初始化 1 已初始化
    battlefield_ battlefield;

    public void RefreshUnit()
    {
        moveAble = 1;
        attackAble = 1;

        // 奋战特性：单位可攻击两次
        if (HasTrait(UnitTraits.Determination))
        {
            attackAble = 2;
        }

        // 恢复伏击（新回合）
        RestoreAmbush();

        UpdateMoveableLight();
    }
    
    /// <summary>
    /// 禁用单位的战斗能力（撤退时使用）
    /// </summary>
    public void DisableCombatAbility()
    {
        moveAble = 0;
        attackAble = 0;
        UpdateMoveableLight();
    }

    /// <summary>
    /// 根据moveAble和attackAble状态切换指示灯纹理。
    /// 绿灯：可移动且可攻击；黄灯：可移动或可攻击其一；红灯：均不可。
    /// 仅在单位已放置在战场上且非指令卡时显示。
    /// </summary>
    private void UpdateMoveableLight()
    {
        if (moveableLight == null) return;

        // 已放置或被拖拽中的非指令/非总部单位显示指示灯
        bool shouldShow = (state == CardState.placed || state == CardState.inplaceAndCaught)
                       && isHq != HQ.hq
                       && cardType != CardTypes.Command;

        if (!shouldShow)
        {
            moveableLight.Visible = false;
            return;
        }

        moveableLight.Visible = true;
        bool canMove = moveAble >= 1 && CanMoveFromCurrentPlace();
        bool canAttack = attackAble >= 1;

        if (canMove && canAttack)
            moveableLight.Texture = greenLightTex;
        else if (canMove || canAttack)
            moveableLight.Texture = yellowLightTex;
        else
            moveableLight.Texture = redLightTex;
    }

    /// <summary>
    /// 根据当前位置检查单位是否实际可以移动。
    /// </summary>
    private bool CanMoveFromCurrentPlace()
    {
        if (battleField == null) return true;
        return battleField.CanCardMoveFromPlace(this);
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
        // 初始化对应的运行时状态
        if ((trait & UnitTraits.SmokeScreen) != 0) hasSmokeScreen = true;
        if ((trait & UnitTraits.Shock) != 0) hasShock = true;
        if ((trait & UnitTraits.Mobilize) != 0) hasMobilize = true;
        if ((trait & UnitTraits.Ambush) != 0) hasAmbushActive = true;
        // 闪击/奋战需要刷新行动次数
        if ((trait & (UnitTraits.Blitz | UnitTraits.Determination)) != 0)
            RefreshUnit();
        RefreshDescriptionText();
        BuildAttributePanel();
    }

    /// <summary>
    /// 移除特性
    /// </summary>
    public void RemoveTrait(UnitTraits trait)
    {
        traits &= ~trait;
        // 清理对应的运行时状态
        if ((trait & UnitTraits.SmokeScreen) != 0) hasSmokeScreen = false;
        if ((trait & UnitTraits.Shock) != 0) hasShock = false;
        if ((trait & UnitTraits.Mobilize) != 0) hasMobilize = false;
        if ((trait & UnitTraits.Ambush) != 0) hasAmbushActive = false;
        RefreshDescriptionText();
        BuildAttributePanel();
    }

    /// <summary>
    /// 刷新描述文字（轻量级，不重建整个面板）
    /// </summary>
    private void RefreshDescriptionText()
    {
        var descLabel = GetNode<RichTextLabel>("description");
        if (descLabel != null)
        {
            descLabel.Text = BuildTraitPrefix() + description;
        }
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
        RefreshDescriptionText();
        BuildAttributePanel();
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
        RefreshDescriptionText();
        BuildAttributePanel();
    }

    /// <summary>
    /// 检查单位是否有动员
    /// </summary>
    public bool HasMobilizeActive()
    {
        return hasMobilize;
    }

    /// <summary>
    /// 移除动员（受到伤害后）
    /// </summary>
    public void RemoveMobilize()
    {
        hasMobilize = false;
        RefreshDescriptionText();
        BuildAttributePanel();
    }

    /// <summary>
    /// 检查伏击是否可用（本回合尚未触发）
    /// </summary>
    public bool HasAmbushActive()
    {
        return hasAmbushActive;
    }

    /// <summary>
    /// 使用伏击（触发后标记为已用）
    /// </summary>
    public void UseAmbush()
    {
        hasAmbushActive = false;
        RefreshDescriptionText();
        BuildAttributePanel();
    }

    /// <summary>
    /// 恢复伏击（新回合开始时）
    /// </summary>
    public void RestoreAmbush()
    {
        if (HasTrait(UnitTraits.Ambush))
        {
            hasAmbushActive = true;
            RefreshDescriptionText();
            BuildAttributePanel();
        }
    }

    /// <summary>
    /// 设置被守护状态
    /// </summary>
    /// <summary>
    /// trait图标闪烁动画：触发时闪烁trait图标（参照FlashAttributeWithColor模式）
    /// </summary>
    public async void FlashTraitIcon(string traitName)
    {
        if (!_attrIconWidgets.TryGetValue(traitName, out var icon)) return;
        if (icon == null) return;

        Color flashColor = new Color(1f, 1f, 0.3f); // 亮黄色闪烁
        Color originalColor = icon.SelfModulate;

        for (int i = 0; i < 3; i++)
        {
            if (!IsInstanceValid(this) || !IsInstanceValid(icon)) return;
            icon.SelfModulate = flashColor;
            await Task.Delay(100);
            if (!IsInstanceValid(this) || !IsInstanceValid(icon)) return;
            icon.SelfModulate = originalColor;
            await Task.Delay(100);
        }

        if (!IsInstanceValid(this) || !IsInstanceValid(icon)) return;
        icon.SelfModulate = originalColor;
    }

    public void SetBeGuardianed(bool value)
    {
        if (isBeGuardianed != value)
        {
            isBeGuardianed = value;
            BuildAttributePanel(); // 刷新图标面板
        }
    }

    /// <summary>
    /// 检查是否被守护
    /// </summary>
    public bool IsBeGuardianed()
    {
        return isBeGuardianed;
    }

    /// <summary>
    /// 读取单位已存活的回合数
    /// </summary>
    /// <returns>存活回合数</returns>
    public int ReadLifeTime()
    {
        return lifeTime;
    }

    /// <summary>
    /// 增加单位存活回合数
    /// </summary>
    public void IncrementLifeTime()
    {
        lifeTime++;
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
        // 只有坦克可以在移动后攻击，其他单位移动后不能攻击
        if (cardType != CardTypes.Tank)
        {
            attackAble = 0;
        }
        UpdateMoveableLight();
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
        UpdateMoveableLight();
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

    public int shouldBeRemoved = 0;
    public bool isDiscarding = false; // 正在播放弃牌动画，不参与ZIndex重置
    List<Change> ChangeList = new List<Change>();
    public async Task ExecChangeList()
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
                case ChangeType.DiscardCard:
                    shouldBeRemoved = 1;
                    break;
            }
        }
        ChangeList.Clear();
    }

    /// <summary>
    /// 挂起弃置操作 - 在所有效果结算完成后执行
    /// </summary>
    public void AddPendingDiscard()
    {
        AddChange(ChangeType.DiscardCard, 0);
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
        int oldCost = cost;
        if (cost + n <= 99) cost += n;
        else cost = 99;
        // 触发闪烁效果（价格越小越好，所以isInverted=true）
        FlashAttributeWithColor("cost", cost, initialCost, minHistoryCost, isInverted: true);
        RefreshState();
        _ = AnimateCostRoll(oldCost, cost);
    }

    /// <summary>
    /// 减少价格
    /// </summary>
    public void ReduceCost(int n)
    {
        int oldCost = cost;
        if (cost - n >= 0) cost -= n;
        else cost = 0;
        // 更新历史最小值并触发闪烁效果
        if (cost < minHistoryCost) minHistoryCost = cost;
        FlashAttributeWithColor("cost", cost, initialCost, minHistoryCost, isInverted: true);
        RefreshState();
        _ = AnimateCostRoll(oldCost, cost);
    }

    /// <summary>
    /// 费用数字滚动动画：从旧值逐步滚动到新值
    /// </summary>
    private async Task AnimateCostRoll(int fromValue, int toValue)
    {
        if (fromValue == toValue) return;
        var costLabel = GetNode<Label>("cost");
        if (costLabel == null) return;

        int steps = Math.Abs(toValue - fromValue);
        int direction = toValue > fromValue ? 1 : -1;
        // 每步间隔80-150ms，变化越多越快但确保每步可见
        float delaySec = Math.Clamp(1.0f / Math.Max(steps, 1), 0.08f, 0.15f);

        int current = fromValue;
        costLabel.Text = current.ToString();
        while (current != toValue)
        {
            await ToSignal(GetTree().CreateTimer(delaySec), SceneTreeTimer.SignalName.Timeout);
            current += direction;
            if (IsInstanceValid(this) && IsInstanceValid(costLabel))
                costLabel.Text = current.ToString();
        }
    }

    public override void _Ready()
    {
        height               = 25;
        state                = CardState.inHand;
        MouseFilter          = MouseFilterEnum.Stop;
        cardBase             = GetNode<Node2D>("Cardbase");
        cardBase.ProcessMode = Node.ProcessModeEnum.Disabled;
        cardBase.Visible     = false;

        battleField = GetTree().Root.GetNodeOrNull<battlefield_>("BattleField");
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

        // 初始化移动/攻击状态指示灯——使用编辑器中已摆放的Sprite2D
        moveableLight = GetNode<Sprite2D>("moveableDisplay");
        greenLightTex = ResourceManager.Instance?.GetTexture("res://assest/greenLight.png")
                     ?? GD.Load<Texture2D>("res://assest/greenLight.png");
        yellowLightTex = ResourceManager.Instance?.GetTexture("res://assest/yellowLight.png")
                      ?? GD.Load<Texture2D>("res://assest/yellowLight.png");
        redLightTex = ResourceManager.Instance?.GetTexture("res://assest/redLight.png")
                   ?? GD.Load<Texture2D>("res://assest/redLight.png");
        UpdateMoveableLight();

        if (this.cardType == CardTypes.Command)
        {
            GetNode<Label>("defence").Visible = false;
            GetNode<Label>("attack").Visible = false;
            GetNode<Sprite2D>("cardbase").Texture = GD.Load<Texture2D>("res://cards/卡背_command.png");
        }

        battleField = GetTree().Root.GetNodeOrNull<battlefield_>("BattleField");

        // 记录初始缩放并创建悬停高亮框
        originalScale = Scale;
        SetupHoverHighlight();

        // 构建attribute图标面板
        BuildAttributePanel();
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

    private void SetupHoverHighlight()
    {
        if (hoverHighlight != null && hoverHighlight.IsInsideTree())
            return;

        // 尝试从已有节点中获取（如果在编辑器中已经添加）
        hoverHighlight = GetNodeOrNull<Panel>("HoverHighlight");
        if (hoverHighlight == null)
        {
            hoverHighlight = new Panel();
            hoverHighlight.Name = "HoverHighlight";
            hoverHighlight.ZIndex = 30;

            // 将高亮框定位与卡牌主纹理一致（使用与箭头相同的偏移量，使高亮对齐卡牌）
            var cardSprite = GetNode<Sprite2D>("cardbase");
            if (cardSprite != null && cardSprite.Texture != null)
            {
                var highlightOffset = new Vector2(90, 120);
                hoverHighlight.Position = cardSprite.Position - highlightOffset;
                hoverHighlight.Size = cardSprite.Texture.GetSize();
            }

            var style = new StyleBoxFlat();
            style.BorderColor = new Color(1f, 0.85f, 0.2f, 0.95f);
            style.BorderWidthTop = 4;
            style.BorderWidthBottom = 4;
            style.BorderWidthLeft = 4;
            style.BorderWidthRight = 4;
            style.ContentMarginLeft = 4;
            style.ContentMarginTop = 4;
            style.ContentMarginRight = 4;
            style.ContentMarginBottom = 4;
            style.BgColor = new Color(0, 0, 0, 0);

            hoverHighlight.AddThemeStyleboxOverride("panel", style);
            hoverHighlight.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(hoverHighlight);
        }

        hoverHighlight.Visible = false;
        hoverHighlight.Modulate = new Color(1, 1, 1, 0);
    }

    public void SetHover(bool hover, float targetScale = 1f, float targetRotationDeg = 0f)
    {
        // 无需重复设置（避免创建过多 Tween）
        float desiredRotation = Mathf.DegToRad(targetRotationDeg);
        if (isHovering == hover &&
            Mathf.IsEqualApprox(Scale.X, hover ? targetScale : originalScale.X) &&
            Mathf.IsEqualApprox(Scale.Y, hover ? targetScale : originalScale.Y) &&
            Mathf.IsEqualApprox(Rotation, desiredRotation))
            return;

        isHovering = hover;
        SetupHoverHighlight();

        if (hoverTween != null && hoverTween.IsValid())
            hoverTween.Kill();

        hoverTween = CreateTween();
        hoverTween.SetTrans(Tween.TransitionType.Sine);
        hoverTween.SetEase(Tween.EaseType.Out);

        Vector2 wantedScale = hover ? new Vector2(targetScale, targetScale) : originalScale;
        const float hoverAnimDuration = 0.10f;
        hoverTween.TweenProperty(this, "scale", wantedScale, hoverAnimDuration);
        hoverTween.TweenProperty(this, "rotation", desiredRotation, hoverAnimDuration);

        // 亮度边框（通过 alpha 逐渐显示/隐藏）
        hoverHighlight.Visible = true;
        float targetAlpha = hover ? 1f : 0f;
        hoverTween.TweenProperty(hoverHighlight, "modulate:a", targetAlpha, hoverAnimDuration);

        ZIndex = hover ? 30 : 10;
    }

    /// <summary>
    /// 进入拖动/操作状态时立刻重置视觉状态（无缓动）
    /// </summary>
    public void ResetVisualsInstant()
    {
        if (hoverTween != null && hoverTween.IsValid())
            hoverTween.Kill();

        Scale = originalScale;
        Rotation = 0f;
        hoverHighlight.Visible = false;
        hoverHighlight.Modulate = new Color(1f, 1f, 1f, 0f);
        ZIndex = 10;
        isHovering = false;
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
        
        // 初始化烟幕、冲击、动员和伏击状态
        hasSmokeScreen = HasTrait(UnitTraits.SmokeScreen);
        hasShock = HasTrait(UnitTraits.Shock);
        hasMobilize = HasTrait(UnitTraits.Mobilize);
        hasAmbushActive = HasTrait(UnitTraits.Ambush);

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
    /// <summary>
    /// 根据traits位标记自动合成加黑描述前缀。不包含效果描述，仅trait名。
    /// </summary>
    private string BuildTraitPrefix()
    {
        if (traits == UnitTraits.None) return "";

        var names = new System.Text.StringBuilder();
        if ((traits & UnitTraits.Blitz) != 0)
            names.Append("闪击 ");
        if ((traits & UnitTraits.Determination) != 0)
            names.Append("奋战 ");
        if ((traits & UnitTraits.HeavyArmor) != 0)
            names.Append("重甲 ");
        if ((traits & UnitTraits.SmokeScreen) != 0)
            names.Append("烟幕 ");
        if ((traits & UnitTraits.Guardian) != 0)
            names.Append("守护 ");
        if ((traits & UnitTraits.Shock) != 0)
            names.Append("冲击 ");
        if ((traits & UnitTraits.Ambush) != 0)
            names.Append("伏击 ");
        if ((traits & UnitTraits.Immunity) != 0)
            names.Append("免疫 ");
        if ((traits & UnitTraits.Mobilize) != 0)
            names.Append("动员 ");
        if ((traits & UnitTraits.SharedHatred) != 0)
            names.Append("同仇 ");

        if (names.Length == 0) return "";
        // 去除末尾空格
        names.Length--;
        return names.ToString() + "\n";
    }

    /// <summary>
    /// 从effect字符串末尾解析[icon=xxx,description=yyy]属性。若无则返回默认action。
    /// </summary>
    private EffectAttribute ParseEffectAttribute(string effectStr)
    {
        if (string.IsNullOrEmpty(effectStr))
            return new EffectAttribute { IconName = "action", Description = "" };

        // 查找最后一个[...]
        int lastBracket = effectStr.LastIndexOf('[');
        if (lastBracket < 0 || !effectStr.EndsWith("]"))
            return new EffectAttribute { IconName = "action", Description = "" };

        string meta = effectStr.Substring(lastBracket + 1, effectStr.Length - lastBracket - 2);
        string iconName = "action";
        string desc = "";

        foreach (var part in meta.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
            {
                if (kv[0].Trim() == "icon") iconName = kv[1].Trim();
                if (kv[0].Trim() == "description") desc = kv[1].Trim();
            }
        }

        return new EffectAttribute { IconName = iconName, Description = desc, IsTrait = false };
    }

    /// <summary>
    /// 收集该卡所有attribute：effect属性 + trait属性（含状态着色）。
    /// </summary>
    public List<EffectAttribute> GetAllAttributes()
    {
        var list = new List<EffectAttribute>();

        // 效果属性
        if (!string.IsNullOrEmpty(effect))
        {
            string cleanEffect = effect;
            int colonIdx = cleanEffect.IndexOf(':');
            if (colonIdx > 0) cleanEffect = cleanEffect.Substring(colonIdx + 1);
            list.Add(ParseEffectAttribute(cleanEffect));
        }

        // 被守护指示器（不是trait，是状态）
        if (isBeGuardianed)
        {
            list.Add(new EffectAttribute
            {
                IconName = "beGuardianed",
                Description = "被守护：两侧有守护单位保护",
                IsTrait = false,
                TraitName = "BeGuardianed",
                IconTint = Colors.White
            });
        }

        // trait属性（含状态着色）
        foreach (UnitTraits t in Enum.GetValues(typeof(UnitTraits)))
        {
            if (t == UnitTraits.None) continue;
            if ((traits & t) != 0)
            {
                Color tint = GetTraitIconTint(t);
                list.Add(new EffectAttribute
                {
                    IconName = IconCache.GetTraitIconName(t),
                    Description = GetTraitDescription(t),
                    IsTrait = true,
                    TraitName = t.ToString(),
                    IconTint = tint
                });
            }
        }

        return list;
    }

    /// <summary>
    /// 根据trait的运行时状态返回图标着色
    /// </summary>
    private Color GetTraitIconTint(UnitTraits t)
    {
        switch (t)
        {
            case UnitTraits.Determination:
                // 黄色=可攻2次，白色=可攻1次，灰色=0次
                if (attackAble >= 2) return new Color(1f, 1f, 0.24f); // 黄色
                if (attackAble == 1) return Colors.White;
                return new Color(0.5f, 0.5f, 0.5f); // 灰色

            case UnitTraits.SmokeScreen:
                return hasSmokeScreen ? Colors.White : new Color(0.5f, 0.5f, 0.5f);

            case UnitTraits.Shock:
                return hasShock ? new Color(1f, 1f, 0.24f) : new Color(0.5f, 0.5f, 0.5f);

            case UnitTraits.Ambush:
                return hasAmbushActive ? Colors.White : new Color(0.5f, 0.5f, 0.5f);

            case UnitTraits.Mobilize:
                return hasMobilize ? Colors.White : new Color(0.5f, 0.5f, 0.5f);

            default:
                return Colors.White;
        }
    }

    /// <summary>
    /// 获取trait的中文描述文本
    /// </summary>
    private static string GetTraitDescription(UnitTraits t) => t switch
    {
        UnitTraits.Blitz => "闪击：部署后可立刻战斗",
        UnitTraits.Determination => "奋战：可再战斗1次",
        UnitTraits.HeavyArmor => "重甲：受到的战斗伤害-1",
        UnitTraits.SmokeScreen => "烟幕：首次行动前不能被攻击",
        UnitTraits.Guardian => "守护：两侧单位不能被攻击",
        UnitTraits.Shock => "冲击：攻击不受到反击，攻击后失去",
        UnitTraits.Ambush => "伏击：被攻击时先造成反击伤害",
        UnitTraits.Immunity => "免疫：不受到战斗伤害",
        UnitTraits.Mobilize => "动员：友方回合开始时+1+1，受伤后消失",
        UnitTraits.SharedHatred => "同仇：被指向时，其他友方同仇单位+1+1",
        _ => ""
    };

    public void RefreshState()
    {
        // 指令卡使用特殊的背景图
        if (cardType == CardTypes.Command)
        {
            if (FileAccess.FileExists(IconPath))
                GetNode<Sprite2D>("icon").Texture = ResourceManager.Instance?.GetTexture(IconPath) ?? GD.Load<Texture2D>(IconPath);
            GetNode<Sprite2D>("cardbase").Texture = ResourceManager.Instance?.GetTexture("res://cards/卡背_command.png") ?? GD.Load<Texture2D>("res://cards/卡背_command.png");
        }
        else if(isHq != HQ.hq)
        {
            if (FileAccess.FileExists(IconPath))
                GetNode<Sprite2D>("icon").Texture = ResourceManager.Instance?.GetTexture(IconPath) ?? GD.Load<Texture2D>(IconPath);
        }
        else
        {
            if (FileAccess.FileExists(IconPath))
                GetNode<Sprite2D>("cardbase").Texture = ResourceManager.Instance?.GetTexture(IconPath) ?? GD.Load<Texture2D>(IconPath);
        }
        if (name != null)
        {
            var nameLabel = GetNode<Label>("name");
            string currentName = name;
            nameLabel.Text = currentName;

            // 只有在名字或标签尺寸改变时才重新计算字体大小，避免频繁刷新带来的性能开销
            if (currentName != lastNameText || nameLabel.Size != lastNameLabelSize)
            {
                AdjustFontSizeToFit(nameLabel);
                lastNameText = currentName;
                lastNameLabelSize = nameLabel.Size;
            }
        }
        GetNode<Label>("attack").Text = attack.ToString();
        GetNode<Label>         ("defence").Text                                                    = defence.ToString();
        GetNode<Label>         ("cost").Text                                                       = cost.ToString();
        if                     (description!= null)
        {
            var descriptionLabel = GetNode<RichTextLabel>("description");
            descriptionLabel.Text = BuildTraitPrefix() + description;
            // 只在第一次调用时调整字体大小
            if (descriptionLabel.GetMeta("fontSizeInitialized", false).AsBool() == false)
            {
                AdjustRichTextFontSizeToFit(descriptionLabel);
                descriptionLabel.SetMeta("fontSizeInitialized", true);
            }
        }

        
        OnRefreshUnitType((int)cardType);

        // 指令卡的特殊UI设置
        if (cardType == CardTypes.Command)
        {
            GetNode<Label>("defence").Visible = false;
            GetNode<Label>("attack").Visible = false;
        }

        // 构建attribute图标面板
        BuildAttributePanel();
    }

    /// <summary>
    /// 构建或刷新卡牌右侧的attribute图标面板
    /// </summary>
    private void BuildAttributePanel()
    {
        // 指令卡不显示attribute
        if (cardType == CardTypes.Command) return;

        // 清除旧面板和tooltip
        if (_attrPanel != null)
        {
            _attrPanel.QueueFree();
            _attrPanel = null;
        }
        if (_attrTooltipPanel != null)
        {
            _attrTooltipPanel.QueueFree();
            _attrTooltipPanel = null;
            _attrTooltipLabel = null;
        }
        _attrIconRects.Clear();
        _attrIconDescs.Clear();
        _attrIconWidgets.Clear();

        var attrs = GetAllAttributes();
        if (attrs.Count == 0) return;

        _attrPanel = new Control();
        _attrPanel.MouseFilter = MouseFilterEnum.Ignore;
        _attrPanel.Position = new Vector2(155, 30);
        AddChild(_attrPanel);

        int visibleCount = Math.Min(attrs.Count, AttrMaxVisible);
        int y = 0;
        int bgMargin = 2;
        int itemH = AttrIconSize + bgMargin * 2;

        for (int i = 0; i < visibleCount; i++)
        {
            var attr = attrs[i];

            // 半透明黑色背景
            var bg = new ColorRect();
            bg.Size = new Vector2(itemH, itemH);
            bg.Position = new Vector2(0, y);
            bg.Color = new Color(0, 0, 0, 0.5f);
            bg.MouseFilter = MouseFilterEnum.Ignore;
            _attrPanel.AddChild(bg);

            // 图标
            var icon = new TextureRect();
            icon.Texture = IconCache.GetIcon(attr.IconName);
            icon.Size = new Vector2(AttrIconSize, AttrIconSize);
            icon.Position = new Vector2(bgMargin, y + bgMargin);
            icon.MouseFilter = MouseFilterEnum.Ignore;
            icon.SelfModulate = attr.IconTint;
            _attrPanel.AddChild(icon);

            // 记录trait图标控件引用（用于闪烁等动画）
            if (attr.IsTrait && !string.IsNullOrEmpty(attr.TraitName))
            {
                _attrIconWidgets[attr.TraitName] = icon;
            }

            // 记录图标在面板中的本地rect和对应描述
            _attrIconRects.Add(new Rect2(0, y, itemH, itemH));
            _attrIconDescs.Add(attr.Description ?? "");

            y += itemH + 2;
        }

        // 折叠提示
        if (attrs.Count > AttrMaxVisible)
        {
            var moreLabel = new Label();
            moreLabel.Text = $"+{attrs.Count - AttrMaxVisible}";
            moreLabel.Position = new Vector2(2, y);
            moreLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
            moreLabel.AddThemeFontSizeOverride("font_size", 10);
            moreLabel.MouseFilter = MouseFilterEnum.Ignore;
            _attrPanel.AddChild(moreLabel);
        }

        // 创建悬停提示（Panel + Label，初始隐藏）
        _attrTooltipPanel = new Panel();
        _attrTooltipPanel.Visible = false;
        _attrTooltipPanel.MouseFilter = MouseFilterEnum.Ignore;
        _attrTooltipPanel.ZIndex = 999;
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0, 0, 0, 0.75f);
        panelStyle.ContentMarginLeft = 4;
        panelStyle.ContentMarginRight = 4;
        panelStyle.ContentMarginTop = 2;
        panelStyle.ContentMarginBottom = 2;
        _attrTooltipPanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_attrTooltipPanel);

        _attrTooltipLabel = new Label();
        _attrTooltipLabel.MouseFilter = MouseFilterEnum.Ignore;
        _attrTooltipLabel.AddThemeColorOverride("font_color", Colors.White);
        // 使用与卡牌描述文字相同的字号
        int descFontSize = GetNode<RichTextLabel>("description").GetThemeFontSize("normal_font_size");
        if (descFontSize <= 0) descFontSize = 14;
        _attrTooltipLabel.AddThemeFontSizeOverride("font_size", descFontSize);
        _attrTooltipPanel.AddChild(_attrTooltipLabel);
    }

    /// <summary>
    /// 调整 Label 字体大小以适应容器（尽可能大且不超出边框）。
    /// 使用二分法减少测量次数，避免在卡牌大量刷新时出现卡顿。
    /// 改进版：添加边距考虑，确保文本不会紧贴边界
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

        // 添加边距：左右各留2像素，上下各留1像素
        Vector2 effectiveSize = new Vector2(containerSize.X - 4, containerSize.Y - 2);

        int bestSize = FindBestFontSizeForLabel(label, font, text, 6, maxSize, effectiveSize);

        // 确保 Label 有 LabelSettings 并正确设置字体
        label.LabelSettings = new LabelSettings();
        label.LabelSettings.Font = font;
        label.LabelSettings.FontSize = bestSize;
    }

    private int FindBestFontSizeForLabel(Label label, Font font, string text, int minSize, int maxSize, Vector2 containerSize)
    {
        int low = minSize;
        int high = maxSize;
        int best = minSize;

        // 创建临时 Label 用于测量，避免修改原 Label 的属性
        Label tempLabel = new Label();
        tempLabel.Text = text;
        tempLabel.LabelSettings = new LabelSettings();
        tempLabel.LabelSettings.Font = font;
        // 临时添加到场景树以确保正确测量
        AddChild(tempLabel);

        while (low <= high)
        {
            int mid = (low + high) / 2;

            // 设置临时 Label 的字体大小
            tempLabel.LabelSettings.FontSize = mid;

            // 获取实际内容大小
            Vector2 measuredSize = tempLabel.GetMinimumSize();

            if (measuredSize.X <= containerSize.X && measuredSize.Y <= containerSize.Y)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        // 清理临时 Label
        RemoveChild(tempLabel);
        tempLabel.QueueFree();

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

    public void ClearMyPlace()
    {
        if (myPlace != null)
        {
            myPlace.UnbondCard();
            myPlace = null;
        }
    }

    /// <summary>
    /// 检测鼠标是否悬停在attribute图标上，显示/隐藏描述tooltip
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (_attrIconRects.Count == 0 || _attrTooltipPanel == null) return;

        if (!(@event is InputEventMouseMotion)) return;

        var mousePos = GetLocalMousePosition();
        var panelPos = (_attrPanel != null) ? _attrPanel.Position : Vector2.Zero;
        bool found = false;

        for (int i = 0; i < _attrIconRects.Count; i++)
        {
            if (_attrIconRects[i].HasPoint(mousePos - panelPos))
            {
                _attrTooltipLabel.Text = _attrIconDescs[i];
                _attrTooltipPanel.Position = mousePos + new Vector2(16, 8);
                _attrTooltipPanel.Size = _attrTooltipLabel.GetMinimumSize() + new Vector2(8, 4);
                _attrTooltipPanel.Visible = true;
                found = true;
                break;
            }
        }

        if (!found)
            _attrTooltipPanel.Visible = false;
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
        UpdateMoveableLight();
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
    /// 执行弃置动画并从战场上移除卡牌
    /// </summary>


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

            targetLabel.AddThemeColorOverride("font_color", targetColor);
            await Task.Delay(100);

            // 再次检查对象是否已被释放
            if (!IsInstanceValid(this) || !IsInstanceValid(targetLabel))
                return;

            targetLabel.AddThemeColorOverride("font_color", Colors.White);
            await Task.Delay(100);
        }

        // 最后一次检查
        if (!IsInstanceValid(this) || !IsInstanceValid(targetLabel))
            return;

        targetLabel.AddThemeColorOverride("font_color", targetColor);


            // 创建闪烁动画：快速变亮再变暗
    }

    /// <summary>
    /// 鼠标进入卡片时调用 - 使卡片出现在最上层
    /// </summary>
    public void MouceEntered()
    {
        SetHover(true);
    }

    /// <summary>
    /// 鼠标离开卡片时调用 - 还原卡片Z位置
    /// </summary>
    public void MouceExited()
    {
        SetHover(false);
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
    Immunity = 1 << 7,
    /// <summary>动员：友方回合开始时+1攻击+1防御，受到伤害后消失</summary>
    Mobilize = 1 << 8,
    /// <summary>同仇：被指向时，所有其他友方同仇单位+1攻击+1防御</summary>
    SharedHatred = 1 << 9
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
    takingDamage,              // 本单位收到伤害时
    bePicked                   // 被指向（成为友方/敌方选择目标时）
}

/// <summary>
/// 效果的attribute元数据，包含icon和描述
/// </summary>
public class EffectAttribute
{
    public string IconName;
    public string Description;
    public bool IsTrait; // true表示来自trait，false表示来自effect
    public string TraitName; // 仅IsTrait时有效
    public Color IconTint = Colors.White; // 图标颜色，用于表示trait状态
}

/// <summary>
/// 图标缓存管理器——预加载有限的icon纹理，避免每次加载
/// </summary>
public static class IconCache
{
    private static Dictionary<string, Texture2D> _cache = new();
    private static bool _initialized = false;

    // 所有可用的icon文件名（不含扩展名）
    private static readonly string[] KnownIcons = new[]
    {
        "action", "Determination", "Guardian",
        "greenLight", "yellowLight", "redLight",
        "blitz", "mobilize", "smoke", "impact",
        "ambush", "heavyArmour", "beGuardianed", "hatred"
    };

    // trait -> icon 映射
    private static readonly Dictionary<UnitTraits, string> TraitIcons = new()
    {
        { UnitTraits.Blitz, "blitz" },
        { UnitTraits.Determination, "Determination" },
        { UnitTraits.HeavyArmor, "heavyArmour" },
        { UnitTraits.SmokeScreen, "smoke" },
        { UnitTraits.Guardian, "Guardian" },
        { UnitTraits.Shock, "impact" },
        { UnitTraits.Ambush, "ambush" },
        { UnitTraits.Immunity, "Immunity" },
        { UnitTraits.Mobilize, "mobilize" },
        { UnitTraits.SharedHatred, "hatred" },
    };

    public static void Init()
    {
        if (_initialized) return;
        foreach (var name in KnownIcons)
        {
            var path = $"res://assest/{name}.png";
            var tex = ResourceManager.Instance?.GetTexture(path)
                   ?? GD.Load<Texture2D>(path);
            if (tex != null) _cache[name] = tex;
        }
        _initialized = true;
    }

    public static Texture2D GetIcon(string name)
    {
        Init();
        return _cache.TryGetValue(name, out var tex) ? tex : null;
    }

    public static string GetTraitIconName(UnitTraits trait)
    {
        return TraitIcons.TryGetValue(trait, out var name) ? name : "action";
    }
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
    DiscardCard,

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
