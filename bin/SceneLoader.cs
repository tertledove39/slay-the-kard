using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 异步场景加载器：切换场景前显示加载覆盖层，改善感知性能。
/// 使用方法：await SceneLoader.ChangeSceneAsync(currentNode, "res://bin/target.tscn");
/// </summary>
public static class SceneLoader
{
    // 加载覆盖层配置
    private const float OverlayAlpha = 0.7f;
    private const int LabelFontSize = 32;

    /// <summary>
    /// 异步切换到目标场景：先显示加载覆盖层，让出一帧后再切换场景。
    /// 虽然ChangeSceneToFile本身是同步阻塞的，但加载覆盖层会先渲染，避免用户看到黑屏。
    /// </summary>
    /// <param name="currentNode">当前场景树中的任意节点</param>
    /// <param name="targetScenePath">目标场景路径，如 "res://bin/battleField.tscn"</param>
    public static async Task ChangeSceneAsync(Node currentNode, string targetScenePath)
    {
        if (currentNode == null || string.IsNullOrEmpty(targetScenePath))
            return;

        var tree = currentNode.GetTree();
        if (tree == null)
            return;

        // 如果当前根节点已经是目标场景（防重入），直接返回
        var currentRoot = tree.Root;
        if (currentRoot != null && currentRoot.SceneFilePath == targetScenePath)
            return;

        // 创建加载覆盖层
        var overlay = CreateLoadingOverlay(tree);
        currentRoot?.AddChild(overlay);

        // 强制让出当前帧，确保覆盖层被渲染
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        // 在覆盖层可见后切换场景（同步阻塞，但用户看到的是加载画面而非黑屏）
        tree.ChangeSceneToFile(targetScenePath);
    }

    /// <summary>
    /// 创建加载覆盖层（CanvasLayer + 半透明背景 + Loading文字）
    /// </summary>
    private static CanvasLayer CreateLoadingOverlay(SceneTree tree)
    {
        var viewSize = tree.Root.GetViewport().GetVisibleRect().Size;

        var canvasLayer = new CanvasLayer();
        canvasLayer.Layer = int.MaxValue; // 最上层
        canvasLayer.Name = "LoadingOverlay";

        // 半透明黑色背景
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, OverlayAlpha);
        bg.MouseFilter = Control.MouseFilterEnum.Stop; // 阻止操作穿透
        canvasLayer.AddChild(bg);

        // 中央Loading文字
        var label = new Label();
        label.Text = "Loading...";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SetAnchorsPreset(Control.LayoutPreset.Center);
        label.SetCustomMinimumSize(new Vector2(300, 80));
        label.AddThemeFontSizeOverride("font_size", LabelFontSize);
        label.AddThemeColorOverride("font_color", Colors.White);
        canvasLayer.AddChild(label);

        return canvasLayer;
    }
}
