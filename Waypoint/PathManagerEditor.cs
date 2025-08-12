// PathManagerEditor.cs
using UnityEngine;
using UnityEditor;
using System.IO; // 用于文件/目录操作

/// <summary>
/// PathManager的自定义编辑器。
/// 提供在Scene视图中可视化编辑路径点以及在Inspector中管理路径的功能。
/// </summary>
[CustomEditor(typeof(PathManager))]
public class PathManagerEditor : Editor
{
    private PathManager pathManager;

    private void OnEnable()
    {
        // 获取对目标组件和其Transform的引用
        pathManager = (PathManager)target;
    }

    /// <summary>
    /// 在Scene视图中绘制自定义GUI
    /// </summary>
    private void OnSceneGUI()
    {
        if (pathManager.waypoints == null || pathManager.waypoints.Count == 0)
        {
            return;
        }

        // --- 绘制路径点之间的连线 ---
        Handles.color = Color.yellow;
        for (int i = 0; i < pathManager.waypoints.Count - 1; i++)
        {
            // 将本地坐标转换为世界坐标进行绘制
            Vector3 startPoint = pathManager.waypoints[i];
            Vector3 endPoint = pathManager.waypoints[i + 1];
            Handles.DrawDottedLine(startPoint, endPoint, 4.0f); // 使用虚线更美观
        }

        // --- 绘制和操作每个路径点的句柄 ---
        for (int i = 0; i < pathManager.waypoints.Count; i++)
        {
            // 开始检查GUI是否有变化，以便支持Undo/Redo
            EditorGUI.BeginChangeCheck();

            // 将本地坐标转换为世界坐标
            Vector3 worldPos = pathManager.waypoints[i];

            // 在Scene视图中创建一个可自由移动的句柄
            // 参数: 位置, 旋转, 大小, 捕捉值, 句柄形状
            Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, 0.25f, Vector3.one * 0.1f, Handles.SphereHandleCap);
            
            // 添加一个标签显示路径点的索引
            Handles.Label(worldPos + Vector3.up * 0.3f, $"P{i}");

            // 如果句柄被移动了 (GUI发生了变化)
            if (EditorGUI.EndChangeCheck())
            {
                // 记录撤销操作
                Undo.RecordObject(pathManager, "Move Waypoint");

                // 将新的世界坐标转换回本地坐标并保存
                pathManager.waypoints[i] = newWorldPos;
                
                // 标记场景为已修改，以便保存
                EditorUtility.SetDirty(pathManager);
            }
        }
    }

    /// <summary>
    /// 绘制自定义Inspector面板
    /// </summary>
    public override void OnInspectorGUI()
    {
        // --- 添加自定义按钮 ---
        if (GUILayout.Button("在末尾添加路径点"))
        {
            Undo.RecordObject(pathManager, "Add Waypoint");
            Vector3 newPoint = pathManager.waypoints.Count > 0
                ? pathManager.waypoints[pathManager.waypoints.Count - 1] + Vector3.forward * 2f // 在最后一个点的前方创建
                : pathManager.transform.position; // 如果是第一个点，则在原点创建
            pathManager.waypoints.Add(newPoint);
            EditorUtility.SetDirty(pathManager);
        }

        if (GUILayout.Button("清空所有路径点"))
        {
            if (EditorUtility.DisplayDialog("确认操作", "确定要清空所有路径点吗？此操作不可撤销。", "确定", "取消"))
            {
                Undo.RecordObject(pathManager, "Clear All Waypoints");
                pathManager.waypoints.Clear();
                EditorUtility.SetDirty(pathManager);
            }
        }

        EditorGUILayout.Space(10);
        
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 1f, 0.8f); // 让创建按钮更显眼
        if (GUILayout.Button("创建路径预制体"))
        {
            CreatePathPrefab();
        }
        GUI.backgroundColor = originalColor;
        
        EditorGUILayout.Space(10);
        // 绘制默认的Inspector字段 (即waypoints列表)
        DrawDefaultInspector();
    }

    private void CreatePathPrefab()
    {
        // 检查路径是否为空
        if (pathManager.waypoints.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "路径点列表为空，无法创建预制体。", "好的");
            return;
        }

        // 定义预制体保存路径
        string directory = "Assets/Prefabs/Paths";
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 创建一个唯一的预制体文件名
        string prefabPath = $"{directory}/{pathManager.gameObject.name}.prefab";
        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

        // 创建预制体
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pathManager.gameObject, prefabPath, out bool success);
        
        if (success)
        {
            EditorUtility.DisplayDialog("成功", $"预制体已成功保存在:\n{prefabPath}", "太棒了！");
            // 让新创建的预制体在Project窗口中高亮显示
            EditorGUIUtility.PingObject(prefab);
        }
        else
        {
            EditorUtility.DisplayDialog("失败", "创建预制体失败，请查看控制台获取更多信息。", "好的");
        }
    }
}
