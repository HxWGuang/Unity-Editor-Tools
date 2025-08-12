using UnityEngine;
using System.Collections.Generic;

// 这个组件用于存储区域的点数据
// 你可以把它挂载到场景中的任何一个空对象上
public class AreaConfigurator : MonoBehaviour
{
    public Color areaColor = new Color(0.0f, 0.8f, 1.0f, 1f);
    // 公开的点列表，方便在Inspector和Editor脚本中访问
    public List<Vector3> points = new List<Vector3>();

    // 在Scene视图中绘制一个图标，方便我们找到这个对象
    private void OnDrawGizmos()
    {
        Gizmos.DrawIcon(transform.position, "d_compass");
    }
}