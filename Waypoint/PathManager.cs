// PathManager.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存储路径点数据的组件。
/// 路径点坐标存储的是相对于此GameObject的本地坐标。
/// </summary>
public class PathManager : MonoBehaviour
{
    // 在Inspector中可见，方便直接查看和微调数据
    [Tooltip("路径点列表")]
    public List<Vector3> waypoints = new List<Vector3>();
}