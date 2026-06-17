using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 异步场景加载器：支持后台预加载PackedScene + 加载覆盖层。
/// 使用方法：
///   WorldMap启动时: SceneLoader.BeginPreload("res://bin/battleField.tscn");
///   切换场景时:   await SceneLoader.ChangeSceneAsync(this, "res://bin/battleField.tscn");
/// </summary>
public static class SceneLoader
{
    // 加载覆盖层配置
    private const float OverlayAlpha = 0.7f;
    private const int LabelFontSize = 32;

    // PackedScene缓存：后台预加载完成后存入，切换时直接用ChangeSceneToPacked
    private static Dictionary<string, PackedScene> _sceneCache = new();
    // 正在后台加载中的路径集合，防止重复请求
    private static HashSet<string> _loadingSet = new();

    // 后台线程加载状态轮询间隔（秒），避免过高频率的ToSignal开销
    private const float PollIntervalSec = 0.05f;

    /// <summary>
    /// 在后台线程启动场景预加载（非阻塞）。
    /// 应在WorldMap._Ready等早期位置调用，让用户在浏览时后台完成加载。
    /// </summary>
    public static void BeginPreload(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return;
        if (_sceneCache.ContainsKey(scenePath) || _loadingSet.Contains(scenePath))
            return;

        _loadingSet.Add(scenePath);
        var err = ResourceLoader.LoadThreadedRequest(scenePath);
        if (err != Error.Ok)
        {
            _loadingSet.Remove(scenePath);
            GD.PrintErr($"[SceneLoader] 后台预加载请求失败: {scenePath} (err={err})");
        }
        else
        {
            GD.Print($"[SceneLoader] 后台预加载已启动: {scenePath}");
        }
    }

    /// <summary>
    /// 异步切换场景：优先使用预缓存的PackedScene（ChangeSceneToPacked极快），
    /// 否则等待后台加载完成。切换前显示加载覆盖层，覆盖层添加到当前场景节点，
    /// 随ChangeScene自动被移除。
    /// </summary>
    public static async Task ChangeSceneAsync(Node currentNode, string targetScenePath)
    {
        if (currentNode == null || string.IsNullOrEmpty(targetScenePath))
            return;

        var tree = currentNode.GetTree();
        if (tree == null) return;

        var currentScene = tree.CurrentScene;
        if (currentScene == null) return;

        // 防重入：已在目标场景中
        if (currentScene.SceneFilePath == targetScenePath)
            return;

        // 创建加载覆盖层，添加到当前场景节点（而非Root），随ChangeScene自动销毁
        var overlay = CreateLoadingOverlay(tree);
        currentScene.AddChild(overlay);

        // 让出一帧确保覆盖层先渲染
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        // 尝试获取预缓存的PackedScene
        PackedScene targetScene = null;

        if (_sceneCache.TryGetValue(targetScenePath, out targetScene))
        {
            GD.Print($"[SceneLoader] 使用已缓存场景: {targetScenePath}");
            tree.ChangeSceneToPacked(targetScene);
            return;
        }

        // 等待后台预加载完成
        if (_loadingSet.Contains(targetScenePath))
        {
            while (true)
            {
                var status = ResourceLoader.LoadThreadedGetStatus(targetScenePath);
                if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                {
                    targetScene = ResourceLoader.LoadThreadedGet(targetScenePath) as PackedScene;
                    break;
                }
                if (status == ResourceLoader.ThreadLoadStatus.Failed)
                {
                    GD.PrintErr($"[SceneLoader] 后台加载失败: {targetScenePath}");
                    break;
                }
                // 等待PollIntervalSec秒再检查，避免高频轮询
                await tree.ToSignal(tree.CreateTimer(PollIntervalSec), SceneTreeTimer.SignalName.Timeout);
            }
            _loadingSet.Remove(targetScenePath);
        }

        if (targetScene != null)
        {
            _sceneCache[targetScenePath] = targetScene;
            GD.Print($"[SceneLoader] 后台加载完成，切换场景: {targetScenePath}");
            tree.ChangeSceneToPacked(targetScene);
        }
        else
        {
            // 最终回退：同步加载（覆盖层已可见，用户不会看到黑屏）
            GD.Print($"[SceneLoader] 回退到同步加载: {targetScenePath}");
            tree.ChangeSceneToFile(targetScenePath);
        }
    }

    /// <summary>
    /// 创建加载覆盖层（CanvasLayer + 半透明背景 + Loading文字）
    /// </summary>
    private static CanvasLayer CreateLoadingOverlay(SceneTree tree)
    {
        var viewSize = tree.Root.GetViewport().GetVisibleRect().Size;

        var canvasLayer = new CanvasLayer();
        canvasLayer.Layer = int.MaxValue;
        canvasLayer.Name = "LoadingOverlay";

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, OverlayAlpha);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        canvasLayer.AddChild(bg);

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
