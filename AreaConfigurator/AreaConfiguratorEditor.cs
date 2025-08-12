using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(AreaConfigurator))]
public class AreaConfiguratorEditor : Editor
{
    private AreaConfigurator configurator;

    private void OnEnable()
    {
        configurator = (AreaConfigurator)target;
        // --- 变更点 1: 工具启用时，主动生成一次Mesh，确保打开场景时显示正确 ---
        // 确保场景加载或代码编译后，如果已有数据，区域能被立即显示出来
        GenerateAreaMesh();
    }

    public override void OnInspectorGUI()
    {
        // --- 变更点: 使用 BeginChangeCheck/EndChangeCheck 监听Inspector的变化 ---
        // 在绘制Inspector之前，开始监听变化
        EditorGUI.BeginChangeCheck();
        // 绘制默认的Inspector面板 (这会绘制出我们的点位列表)
        DrawDefaultInspector();
        // 检查从BeginChangeCheck()到这里是否有任何GUI控件的值被用户修改
        if (EditorGUI.EndChangeCheck())
        {
            // 如果有变化，立即重新生成Mesh以保持同步
            GenerateAreaMesh();
        }
        // 保留辅助提示信息
        EditorGUILayout.HelpBox("在场景视图中编辑点位:\n- Shift + 左键: 添加点\n- Ctrl + 左键: 删除点\n- 拖动点位手柄进行移动", MessageType.Info);
    }
    
    private void OnSceneGUI()
    {
        if (configurator == null) return;

        // 统一处理所有输入事件
        HandleInputEvents();

        // 绘制并处理所有点
        DrawAndHandlePoints();

        // 绘制点之间的连线
        DrawLinesBetweenPoints();
    }

    private void HandleInputEvents()
    {
        Event e = Event.current;

        // 添加点 (Shift + 左键)
        if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
        {
            AddPoint(e);
            e.Use(); // 消费事件
        }

        // 删除点 (Ctrl + 左键)
        if (e.type == EventType.MouseDown && e.button == 0 && e.control)
        {
            DeletePoint(e);
            e.Use(); // 消费事件
        }
    }

    private void DrawAndHandlePoints()
    {
        Handles.color = Color.yellow;
        for (int i = 0; i < configurator.points.Count; i++)
        {
            Vector3 worldPoint = configurator.transform.TransformPoint(configurator.points[i]);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPoint = Handles.PositionHandle(worldPoint, Quaternion.identity);

            // --- 变更点 3: 拖动点位时实时更新 ---
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(configurator, "Move Point");
                configurator.points[i] = configurator.transform.InverseTransformPoint(newWorldPoint);
                EditorUtility.SetDirty(configurator);
                GenerateAreaMesh(); // 关键：在点位变化后立刻重新生成Mesh
            }

            Handles.Label(worldPoint + Vector3.up * 0.2f, $"P{i}");
        }
    }

    private void AddPoint(Event e)
    {
        Ray worldRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Physics.Raycast(worldRay, out RaycastHit hitInfo))
        {
            Undo.RecordObject(configurator, "Add Point");
            configurator.points.Add(configurator.transform.InverseTransformPoint(hitInfo.point));
            EditorUtility.SetDirty(configurator);
            GenerateAreaMesh(); // 关键：添加点后立刻重新生成Mesh
        }
    }

    private void DeletePoint(Event e)
    {
        int pointToDelete = -1;
        float minDistanceToMouse = 30f; // 点击的容错像素距离
        for (int i = 0; i < configurator.points.Count; i++)
        {
            Vector3 worldPoint = configurator.transform.TransformPoint(configurator.points[i]);
            float distance = HandleUtility.DistanceToCircle(worldPoint, 0);
            if (distance < minDistanceToMouse)
            {
                pointToDelete = i;
            }
        }
        
        if (pointToDelete != -1)
        {
            Undo.RecordObject(configurator, "Delete Point");
            configurator.points.RemoveAt(pointToDelete);
            EditorUtility.SetDirty(configurator);
            GenerateAreaMesh();
        }
    }

    private void DrawLinesBetweenPoints()
    {
        Handles.color = Color.cyan;
        for (int i = 0; i < configurator.points.Count; i++)
        {
            Vector3 p1 = configurator.transform.TransformPoint(configurator.points[i]);
            Vector3 p2 = configurator.transform.TransformPoint(configurator.points[(i + 1) % configurator.points.Count]);
            Handles.DrawDottedLine(p1, p2, 4.0f);
        }
    }

    // --- 这是我们重点优化的方法 ---
    private void GenerateAreaMesh()
    {
        MeshFilter meshFilter = configurator.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = configurator.gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = configurator.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = configurator.gameObject.AddComponent<MeshRenderer>();
        
        // --- 变更点 1: 获取或创建Mesh，而不是每次都销毁 ---
        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            Debug.Log("创建新的mesh");
            // 只有当Mesh不存在时，才创建一个新的，并赋给MeshFilter
            mesh = new Mesh { name = "GeneratedAreaMesh" };
            meshFilter.mesh = mesh;
        }
        // 如果点少于3个，无法构成面，清空Mesh并隐藏渲染器
        if (configurator.points.Count < 3)
        {
            mesh.Clear(); // 清空Mesh数据
            meshRenderer.enabled = false; // 隐藏渲染
            return;
        }
        
        // 确保渲染器是启用的
        meshRenderer.enabled = true;
        // --- 变更点 2: 清空现有数据，准备接收新数据 ---
        mesh.Clear();
        // 填充顶点和三角形数据
        mesh.vertices = configurator.points.ToArray();
        List<int> triangles = new List<int>();
        for (int i = 1; i < configurator.points.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i);
        }
        mesh.triangles = triangles.ToArray();
        // 重新计算法线和边界
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        // 配置材质和渲染器属性（这部分逻辑不变）
        if (meshRenderer.sharedMaterial == null || meshRenderer.sharedMaterial.shader.name != "Unlit/Color")
        {
            Shader unlitShader = Shader.Find("Unlit/Color");
            if(unlitShader != null)
            {
                meshRenderer.sharedMaterial = new Material(unlitShader);
            }
        }
        
        if(meshRenderer.sharedMaterial != null)
        {
            meshRenderer.sharedMaterial.color = configurator.areaColor;
        }
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }
}
