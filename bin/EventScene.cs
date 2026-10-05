using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// 事件场景界面：在WorldMap或ChooseMission上以CanvasLayer叠加显示。
/// 布局：左侧400x600事件配图、上方金色标题栏、右侧可滚动剧情描述、下方选项按钮。
/// 玩家选择选项后执行对应效果（如materialPoints/replaceCard/replaceRandomCard），完成时标记区域并返回世界地图。
/// </summary>
public partial class EventScene : CanvasLayer
{
    /// <summary>当前展示的事件数据，包含标题、描述、配图路径和选项列表</summary>
    private EventData _event;
    /// <summary>触发此事件的区域名，完成后标记为已完成</summary>
    private string _areaName;
    private const float HoverScale = 1.08f;
    private const float HoverDuration = 0.12f;

    /// <summary>悬浮预览面板的场景路径</summary>
    private const string PreviewScenePath = "res://bin/event_card_preview.tscn";

    /// <summary>左侧事件配图宽度（像素）</summary>
    private const float ImageWidth = 400f;
    private const float ImageHeight = 600f;

    /// <summary>
    /// 静态入口：在parent节点上创建EventScene叠加层并运行事件流程。
    /// 流程完成后自动QueueFree清理。
    /// </summary>
    /// <param name="parent">父节点（通常是当前场景的根Control）</param>
    /// <param name="eventData">从event.ini加载的事件数据</param>
    /// <param name="areaName">触发此事件的区域名，用于完成后标记</param>
    public static async Task Show(Node parent, EventData eventData, string areaName)
    {
        var scene = new EventScene { Layer = 2 };
        parent.AddChild(scene);

        // 事件期间收起任务选择面板，但**保留**本次抽到的那一批（走 CloseMissionPanel
        // 而不是 Dismiss）：事件结束后玩家回到地图，看到的还是同样三个选项。
        //
        // ⚠️ 这里必须**向上找** WorldMap，不能写 `parent is WorldMap`：
        // 事件的开场是 ChooseMission.StartEvent → EventScene.Show(this, ...)，
        // 传进来的 parent 是**任务选择面板**而不是世界地图，直接判类型会永远为 false，
        // 面板收不起来、区域按钮也锁不住（暗幕已改成不拦鼠标，点击就会穿透过去）。
        var map = FindWorldMap(parent);
        map?.EnterEventOverlay();

        bool campaignCompleted = await scene.Run(eventData, areaName);
        scene.QueueFree();

        // 事件结算完毕才丢弃这一批——烈度已经被这次事件消耗掉了
        map?.ExitEventOverlay();

        if (campaignCompleted)
            await CampaignVictory.ShowAndReturnToMenu(parent);
    }

    /// <summary>
    /// 从任意节点向上找所属的世界地图。事件叠层的调用方可能是世界地图本身，
    /// 也可能是它下面的任务选择面板，所以要沿父链找而不是直接判类型。
    /// </summary>
    private static WorldMap FindWorldMap(Node from)
    {
        for (var node = from; node != null; node = node.GetParent())
        {
            if (node is WorldMap map)
                return map;
        }
        return null;
    }

    /// <summary>
    /// 运行事件的主流程：绘制背景遮罩→左侧配图→标题栏→右侧可滚动描述→选项按钮→等待玩家选择→执行效果→标记完成→返回世界地图。
    /// 整个流程异步执行，玩家选择前场景处于等待状态。
    /// </summary>
    /// <returns>true 表示本次事件使最后一个区域的烈度归零，战役通关</returns>
    private async Task<bool> Run(EventData eventData, string areaName)
    {
        _event = eventData;
        _areaName = areaName;

        // 悬浮预览面板：结构与配色在 event_card_preview.tscn 里，这里只实例化挂载
        _cardPreview = ResourceLoader.Load<PackedScene>(PreviewScenePath)?.Instantiate() as EventCardPreview;
        if (_cardPreview != null)
        {
            _cardPreview.ZIndex = 100;
            AddChild(_cardPreview);
        }

        // 暗幕只负责压暗，**不拦截鼠标**：事件期间玩家要能点到世界地图上的
        // 商店与卡组按钮（那两个弹层分别在 Layer 10 与 Layer 2 后加，天然盖在事件之上）。
        // 挡点击的活交给 WorldMap.EnterEventOverlay()：它把区域按钮锁掉，
        // 避免点到事件背后的区域又开一个任务面板。
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.75f);
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(bg);

        var viewSize = GetViewport().GetVisibleRect().Size;
        float totalW = ImageWidth + 40 + 500; // 图片 + 间距 + 文字区
        float startX = (viewSize.X - totalW) / 2;
        float startY = 60;

        // --- 左侧事件图片 ---
        var imgRect = new TextureRect();
        imgRect.Position = new Vector2(startX, startY);
        imgRect.Size = new Vector2(ImageWidth, ImageHeight);
        imgRect.StretchMode = TextureRect.StretchModeEnum.Scale;
        if (Godot.FileAccess.FileExists(_event.Image))
            imgRect.Texture = ResourceLoader.Load<Texture2D>(_event.Image);
        else
            imgRect.SelfModulate = new Color(0.3f, 0.3f, 0.3f);
        AddChild(imgRect);

        // --- 上方标题栏 ---
        var titleBg = new ColorRect();
        titleBg.Position = new Vector2(startX, startY - 44);
        titleBg.Size = new Vector2(totalW, 40);
        titleBg.Color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        AddChild(titleBg);

        float textX = startX + ImageWidth + 40;
        var titleLabel = new Label();
        titleLabel.Text = _event.Title;
        titleLabel.Position = titleBg.Position;
        titleLabel.Size = titleBg.Size;
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.VerticalAlignment = VerticalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 24);
        titleLabel.AddThemeColorOverride("font_color", Colors.Gold);
        AddChild(titleLabel);

        // --- 右侧可滚动文字 ---
        float textAreaH = ImageHeight - 200; // 留出下方选项空间
        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(textX, startY);
        scroll.Size = new Vector2(500, textAreaH);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        AddChild(scroll);

        var descLabel = new RichTextLabel();
        descLabel.BbcodeEnabled = true;
        descLabel.FitContent = true;
        descLabel.CustomMinimumSize = new Vector2(480, 0);
        descLabel.AddThemeColorOverride("default_color", Colors.White);
        // 必须先AddChild再设Text，否则节点不在树中无法正确测量字体大小
        scroll.AddChild(descLabel);
        descLabel.Text = "[font_size=18]" + _event.Description + "[/font_size]";

        // --- 选项按钮区 ---
        float choicesY = startY + textAreaH + 20;
        var tcs = new TaskCompletionSource<string>();

        for (int i = 0; i < _event.Choices.Count; i++)
        {
            var choice = _event.Choices[i];
            var btn = new Button();
            btn.Position = new Vector2(textX, choicesY + i * 50);
            btn.Size = new Vector2(480, 42);
            btn.AddThemeFontSizeOverride("font_size", 16);

            // 资源点不足的选项不可选：只把「花费」算进去（负数之和），
            // 带收益的选项不因收益而放宽，避免出现「花了之后才发现不够」。
            int cost = EventEffectRunner.ParseMaterialCost(choice.Effect);
            btn.Text = cost > BattleStateManager.MaterialPoints
                     ? $"{choice.Text}（需要 {cost} 资源点）"
                     : choice.Text;
            btn.Disabled = cost > BattleStateManager.MaterialPoints;

            // 会往卡组加卡的选项：悬浮时预览这些卡
            var preview = EventEffectRunner.ParseAddedCards(choice.Effect);
            if (preview.Count > 0)
            {
                btn.MouseEntered += () => ShowCardPreview(preview, btn);
                btn.MouseExited += HideCardPreview;
            }

            btn.MouseEntered += () => AnimateButton(btn, HoverScale);
            btn.MouseExited += () => AnimateButton(btn, 1f);
            int idx = i;
            btn.Pressed += () =>
            {
                tcs.TrySetResult(_event.Choices[idx].Effect);
            };
            AddChild(btn);
        }

        string effect = await tcs.Task;
        if (string.IsNullOrEmpty(effect)) effect = "";

        // --- 执行效果 ---
        await EventEffectRunner.Execute(this, effect);

        // 事件与战斗同等消耗1点区域烈度；归零时解锁下一区域
        bool areaCleared = BattleStateManager.ConsumeAreaIntensity(_areaName);
        GD.Print($"[EventScene] 事件完成，区域 {_areaName} 剩余烈度 {BattleStateManager.ReadAreaIntensity(_areaName)}");
        return areaCleared && BattleStateManager.IsFinalArea(_areaName);
    }

    private static void AnimateButton(Button button, float scale)
    {
        button.PivotOffset = button.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale", Vector2.One * scale, HoverDuration);
    }

    // ============================ 卡牌预览 ============================

    /// <summary>「选项会加入哪些卡」的悬浮预览面板；结构与配色都在场景里，本类只负责开关与填数据。</summary>
    private EventCardPreview _cardPreview;

    private void ShowCardPreview(List<(CardData card, int count)> cards, Control anchor)
    {
        _cardPreview?.Show(cards, anchor);
    }

    private void HideCardPreview()
    {
        _cardPreview?.Hide();
    }
}

/// <summary>
/// 事件数据模型：从event.ini解析得到的单个事件的全部信息。
/// 包含事件标识、标题、配图路径、剧情描述和最多4个选项。
/// </summary>
public class EventData
{
    /// <summary>事件唯一标识符，如 "supply_drop"、"ambush" 等，对应event.ini中的节名</summary>
    public string Id;
    /// <summary>事件标题，显示在界面上方金色标题栏中</summary>
    public string Title;
    /// <summary>事件配图的资源路径，如 "res://assest/event_supply.png"，显示在界面左侧</summary>
    public string Image;
    /// <summary>事件的剧情描述文本，支持BBCode格式，显示在右侧可滚动文字区</summary>
    public string Description;
    /// <summary>事件的选项列表（最多4个），每个选项包含显示文字和效果字符串</summary>
    public List<EventChoice> Choices = new();
}

/// <summary>
/// 事件选项模型：玩家在事件界面中可选择的单个选项。
/// 每个选项有一段描述文字和对应的效果指令字符串。
/// </summary>
public class EventChoice
{
    /// <summary>选项按钮上显示的文本，如"接受补给"、"继续前进"</summary>
    public string Text;
    /// <summary>选项的效果指令字符串，支持 materialPoints(n)、replaceCard(id)、replaceRandomCard(id) 或 "none"</summary>
    public string Effect;
}
