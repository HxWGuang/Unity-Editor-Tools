using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System;

public class SceneSwitcherWindowV6 : EditorWindow
{
    // --- 常量 ---
    // **V5 布局修复**: 为工具栏和搜索栏保留一个固定的、经过计算的高度，以防止布局塌陷。
    private const float TOOLBAR_AREA_HEIGHT = 42f; 

    // --- 数据结构与枚举 ---
    private enum SceneSource { BuildSettings, AllProjectAssets }
    private struct SceneInfo { public string path; public string displayName; public bool isEnabledInBuild; public int buildIndex; }

    // --- 状态与样式变量 ---
    private List<SceneInfo> cachedSceneList = new List<SceneInfo>();
    private Vector2 scrollPosition;
    private string filterText = "";
    private SceneSource currentSource = SceneSource.BuildSettings;
    private string currentScenePath;
    
    // **V4 性能优化**: 缓存GUIStyle，避免在OnGUI中每帧都创建新对象，提升性能。
    private GUIStyle boldLabelStyle;
    private bool stylesInitialized = false;

    // --- 初始化与生命周期 ---
    [MenuItem("Tools/Scene Switcher (Final Version)")]
    public static void ShowWindow() { GetWindow<SceneSwitcherWindowV6>("Scene Switcher"); }

    private void OnEnable()
    {
        EditorBuildSettings.sceneListChanged += RefreshSceneList;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        UpdateCurrentSceneInfo();
        RefreshSceneList();
    }

    private void OnDisable()
    {
        EditorBuildSettings.sceneListChanged -= RefreshSceneList;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
    }

    // --- 样式初始化 ---
    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        // **V4 UX优化**: 为当前场景的标签创建一个醒目的粗体样式，提供更强的视觉反馈。
        boldLabelStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : Color.black }
        };
        
        stylesInitialized = true;
    }

    // --- 事件处理 ---
    private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
    {
        UpdateCurrentSceneInfo();
        Repaint();
    }

    private void UpdateCurrentSceneInfo() { currentScenePath = EditorSceneManager.GetActiveScene().path; }

    #region DataLogic
    private void RefreshSceneList()
    {
        cachedSceneList.Clear();
        switch (currentSource)
        {
            case SceneSource.BuildSettings: PopulateFromBuildSettings(); break;
            case SceneSource.AllProjectAssets: PopulateFromAllAssets(); break;
        }
        Repaint();
    }
    private void PopulateFromBuildSettings()
    {
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            var scene = EditorBuildSettings.scenes[i];
            if (string.IsNullOrEmpty(scene.path)) continue;
            cachedSceneList.Add(new SceneInfo { path = scene.path, displayName = Path.GetFileNameWithoutExtension(scene.path), isEnabledInBuild = scene.enabled, buildIndex = i });
        }
    }
    private void PopulateFromAllAssets()
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        var buildScenes = new Dictionary<string, (bool isEnabled, int buildIndex)>();
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            var scene = EditorBuildSettings.scenes[i];
            if (!string.IsNullOrEmpty(scene.path)) buildScenes[scene.path] = (scene.enabled, i);
        }
        
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string displayName = Path.GetFileNameWithoutExtension(path);

            // **V6 数据逻辑修复**: 修复了一个潜在bug。之前使用TryGetValue的默认值会导致
            // 未在BuildSettings中的场景错误地获得buildIndex 0。现在逻辑更清晰、正确。
            if (buildScenes.TryGetValue(path, out var buildInfo))
            {
                // 该场景存在于Build Settings中
                cachedSceneList.Add(new SceneInfo { path = path, displayName = displayName, isEnabledInBuild = buildInfo.isEnabled, buildIndex = buildInfo.buildIndex });
            }
            else
            {
                // 该场景不在Build Settings中，buildIndex设为-1表示无效
                cachedSceneList.Add(new SceneInfo { path = path, displayName = displayName, isEnabledInBuild = false, buildIndex = -1 });
            }
        }
    }
    #endregion

    // --- GUI 渲染 (核心重构) ---
    void OnGUI()
    {
        InitializeStyles();

        // **V5 终极布局修复**: 解决列表覆盖工具栏的问题。我们不再依赖单一的自动布局流，
        // 而是手动将窗口分割成两个精确的矩形区域（`Rect`），一个用于工具栏，一个用于列表。
        // 这从根本上保证了布局的稳定性和正确性。
        Rect toolbarArea = new Rect(0, 0, position.width, TOOLBAR_AREA_HEIGHT);
        Rect listArea = new Rect(0, toolbarArea.yMax, position.width, position.height - toolbarArea.height);

        // 在各自隔离的区域内进行绘制，互不干扰。
        DrawToolbarAndFilter(toolbarArea);
        DrawSceneList(listArea);
    }

    private void DrawToolbarAndFilter(Rect area)
    {
        // **V5 布局修复**: 使用 `GUILayout.BeginArea` 将所有后续绘制指令限制在该矩形区域内。
        GUILayout.BeginArea(area);
        
        // 在Area内部，我们仍然可以愉快地使用自动布局来简化控件排列。
        EditorGUILayout.BeginVertical();

        // 工具栏: 切换源 + 刷新 (第一行)
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUI.BeginChangeCheck();
        currentSource = (SceneSource)GUILayout.Toolbar((int)currentSource, Enum.GetNames(typeof(SceneSource)), EditorStyles.toolbarButton);
        if (EditorGUI.EndChangeCheck()) RefreshSceneList();
        
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("Refresh", "Manually refresh"), EditorStyles.toolbarButton)) RefreshSceneList();
        EditorGUILayout.EndHorizontal();

        // 搜索栏 (第二行)
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        // **V3 BUG修复**: 修复了`NullReferenceException`和`ArgumentException`。
        // 通过使用`??`操作符，当`FindStyle`返回null时，我们能优雅地降级到默认样式，
        // 保证了工具在任何Unity版本或UI环境下的健壮性。
        var searchFieldStyle = GUI.skin.FindStyle("ToolbarSeachTextField") ?? GUI.skin.textField;
        filterText = EditorGUILayout.TextField(filterText, searchFieldStyle, GUILayout.ExpandWidth(true));
        
        var cancelButtonstyle = GUI.skin.FindStyle("ToolbarSeachCancelButton") ?? GUI.skin.button;
        if (GUILayout.Button("Clear", cancelButtonstyle))
        {
            filterText = "";
            GUI.FocusControl(null); // 清除焦点，改善体验
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawSceneList(Rect area)
    {
        GUILayout.BeginArea(area);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // **V6 序号逻辑修复**: 创建一个动态的显示序号，仅用于“所有场景”模式。
        int displayIndex = 1;
        bool hasVisibleItems = false;

        foreach (var sceneInfo in cachedSceneList)
        {
            if (!(string.IsNullOrEmpty(filterText) || sceneInfo.displayName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

            hasVisibleItems = true;
            bool isCurrentScene = sceneInfo.path == currentScenePath;

            // **V4 UX优化**: 使用更醒目的颜色高亮当前已打开的场景。
            Color originalBgColor = GUI.backgroundColor;
            if (isCurrentScene) GUI.backgroundColor = new Color(0.24f, 0.5f, 0.85f);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            string labelText = sceneInfo.displayName;
            // **V6 序号逻辑修复**: 根据当前模式决定显示哪种序号。
            if (currentSource == SceneSource.AllProjectAssets)
            {
                labelText = $"[{displayIndex}] {labelText}";
            }
            else // BuildSettings模式
            {
                if (sceneInfo.buildIndex != -1) labelText = $"[{sceneInfo.buildIndex}] {labelText}";
            }
            
            if (currentSource == SceneSource.BuildSettings && !sceneInfo.isEnabledInBuild) labelText += " (Disabled)";

            GUIContent labelContent = new GUIContent(labelText, sceneInfo.path);
            var labelStyle = isCurrentScene ? boldLabelStyle : EditorStyles.label;
            
            var originalContentColor = GUI.contentColor;
            if (currentSource == SceneSource.BuildSettings && !sceneInfo.isEnabledInBuild) GUI.contentColor = Color.gray;
            
            GUILayout.Label(labelContent, labelStyle, GUILayout.ExpandWidth(true));
            GUI.contentColor = originalContentColor;

            // **V4 UX优化**: 当前已打开的场景的"Open"按钮应为不可点击状态，防止误操作。
            bool wasEnabled = GUI.enabled;
            GUI.enabled = !isCurrentScene;
            if (GUILayout.Button("Open", GUILayout.Width(80)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(sceneInfo.path, OpenSceneMode.Single);
                }
            }
            GUI.enabled = wasEnabled; // 立即恢复GUI状态，不影响后续控件。

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = originalBgColor;

            displayIndex++; // 序号仅为可见项递增
        }

        if (!hasVisibleItems)
        {
            EditorGUILayout.HelpBox("No scenes found matching your criteria.", MessageType.Info);
        }
        
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
