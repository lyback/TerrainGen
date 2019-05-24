using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class TerrainRoot : RenderDynamicMesh
{
    TerrainAssets m_asset;
    Dictionary<string, TerrainMeshData> m_meshData = new Dictionary<string, TerrainMeshData>();
    public GameObject RootTerrain;                      // 场景根节点
#region 显示属性
    float m_visibleSize;					            // 显示宽度
    float m_visibleHalfSize;                            // 显示宽度Half
#endregion
#region 临时变量
    TerrainMeshData m_terrainMeshData;                  //当前地形网格数据
    int m_maxResolusion;                                //当前地形网格最大分辨率
    int m_visibleVerticeCount;                          //生成网格一行的顶点数量
    float m_posDis = 0f;                                //一距离多少顶点
    float m_verticeDis = 0f;                            // 一顶点多少距离
    float m_uvDis;                                      //uv间距
    Vector2Int m_moveToVecticePoint = Vector2Int.zero;  //网格顶点中心点（像素）
    Vector3 m_moveToPoint = Vector3.zero;               //网格坐标中心点（世界坐标）
    string m_terrainName = "";                          //当前顶点所在地形名称
    int m_terrainIndex;                                 //当前顶点所在地形索引
    int m_curTriIndex;                                  //当前用到第几个subMesh的三角形列表
    Vector3 m_Vector3_temp = Vector3.zero;
    Vector2 m_Vector2_temp = Vector2.zero;
    //----顶点生成缓存---//
    int m_curVerticeIndex;                //当前顶点索引
    Dictionary<int, int> m_curVerticeCache = new Dictionary<int, int>(); //当前顶点索引缓存
    //--------end------//
    int m_lastPos_x;
    int m_lastPos_z;
#endregion
    protected override GameObject Root
    {
        get
        {
            return RootTerrain;
        }
    }

    public void Init(TerrainAssets config, float size)
    {
        m_asset = config;
        m_visibleSize = size;
        m_visibleHalfSize = size / 2f;

        m_uvDis = 1f / m_asset.terrain_Size;

    }

    public void MoveTo(float x, float z)
    {
        x = Mathf.Floor(x);
        z = Mathf.Floor(z);
        if (m_lastPos_x == x && m_lastPos_z == z)
        {
            return;
        }

        ClearMeshData();
        m_maxResolusion = GetMaxResolusion(x, z)-1;
        m_verticeDis = m_asset.terrain_Size * 1f / m_maxResolusion;
        m_posDis = m_maxResolusion * 1f / m_asset.terrain_Size * 1f;
        m_visibleVerticeCount = Mathf.CeilToInt(m_posDis * m_visibleSize);

        m_moveToPoint.x = x;
        m_moveToPoint.z = z;
        m_moveToVecticePoint.x = Mathf.FloorToInt(x / m_verticeDis);
        m_moveToVecticePoint.y = Mathf.FloorToInt(z / m_verticeDis);

        MakeMesh();

        BuildMesh();
        // TestBuildMesh();
    }
    void TestBuildMesh()
    {
        m_mesh.Clear(false);
        m_mesh.subMeshCount = 4;
        List<Vector3> vs = new List<Vector3>();
        vs.Add(new Vector3(0, 0, 0));
        vs.Add(new Vector3(0, 0, 1));
        vs.Add(new Vector3(1, 0, 0));
        vs.Add(new Vector3(1, 0, 1));
        m_mesh.SetVertices(vs);
        List<Vector2> uvs = new List<Vector2>();
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        m_mesh.SetUVs(0, uvs);
        List<int> tri = new List<int>();
        tri.Add(0);
        tri.Add(3);
        tri.Add(2);
        tri.Add(0);
        tri.Add(1);
        tri.Add(3);
        m_mesh.SetTriangles(tri, 0);
        m_mesh.RecalculateNormals();
    }
    void BuildMesh()
    {
        m_mesh.Clear(false);
        m_mesh.subMeshCount = 4;
        m_mesh.SetVertices(m_vertices);
        m_mesh.SetUVs(0, m_uvs);
        foreach (var kv in m_subMeshIndexDic)
        {
            m_mesh.SetTriangles(m_triangles[kv.Value], kv.Key);
        }
        m_mesh.RecalculateNormals();

        RootTerrain.transform.localPosition = m_moveToPoint;
    }
    void MakeMesh()
    {
        for (int x = 0; x < m_visibleVerticeCount; ++x)
        {
            for (int y = 0; y < m_visibleVerticeCount; ++y)
            {

                MakeRectangle(x-m_visibleVerticeCount/2, y-m_visibleVerticeCount/2);
            }
        }
        m_curTriIndex = 0;
        m_curVerticeIndex = 0;
        m_curVerticeCache.Clear();
    }
    void MakeRectangle(int x, int z)
    {
        int x_vpos = x + m_moveToVecticePoint.x;
        int z_vpos = z + m_moveToVecticePoint.y;
        int terrain_size = m_asset.terrain_Size;
        int column = m_asset.terrain_Column_Count;
        int row = (x_vpos) / m_maxResolusion;
        int col = (z_vpos) / m_maxResolusion;
        m_terrainIndex = col * column + row;
        m_terrainName = GetTerrainNameByIndex(m_terrainIndex);
        m_terrainMeshData = GetTerrainMeshData(m_terrainName);
        if (m_terrainMeshData == null)
        {
            return;
        }
        AddVertice(x, z, 0);
        AddVertice(x + 1, z, 1);
        AddVertice(x, z + 1, 2);
        AddVertice(x + 1, z + 1, 3);
        int matIndex = m_terrainMeshData.matIndex;
        int triIndex;
        if (!m_subMeshIndexDic.TryGetValue(matIndex, out triIndex))
        {
            m_subMeshIndexDic.Add(matIndex, m_curTriIndex);
            triIndex = m_curTriIndex++;
        }
        m_triangles[triIndex].Add(INDEXS[0]);
        m_triangles[triIndex].Add(INDEXS[2]);
        m_triangles[triIndex].Add(INDEXS[3]);
        m_triangles[triIndex].Add(INDEXS[0]);
        m_triangles[triIndex].Add(INDEXS[3]);
        m_triangles[triIndex].Add(INDEXS[1]);
    }

    void AddVertice(int x, int z, int index)
    {
        float height = GetVerticeHeight(x, z, m_terrainName);
        float x_pos = x*m_verticeDis;
        float z_pos = z*m_verticeDis;
        m_Vector3_temp.y = height;
        m_Vector3_temp.x = x_pos;
        m_Vector3_temp.z = z_pos;
        m_vertices.Add(m_Vector3_temp);
        m_uvs.Add(GetUV(x, z));
        INDEXS[index] = m_curVerticeIndex++;
    }
    float GetVerticeHeight(float x, float y, string terrainName)
    {
        var heightMap = m_terrainMeshData.heightMap;
        var resolusion = m_terrainMeshData.resolusion;
        int index = (int)(x * m_posDis + y * m_posDis * resolusion);
        float height = 0f;
        if (heightMap != null && heightMap.TryGetValue(index, out height))
        {
            return height;
        }
        return height;
    }
    Vector2 GetUV(int x, int y)
    {
        int terrain_size = m_asset.terrain_Size;
        m_Vector2_temp.x = (x + m_moveToVecticePoint.x)*1f / m_maxResolusion*1f;
        m_Vector2_temp.y = (y + m_moveToVecticePoint.y)*1f / m_maxResolusion*1f;
        return m_Vector2_temp;
    }
    int GetMaxResolusion(float x, float y)
    {
        int terrain_size = m_asset.terrain_Size;
        int column = m_asset.terrain_Column_Count;
        float Left_x = (x - m_visibleHalfSize) / terrain_size;
        float Top_y = (y + m_visibleHalfSize) / terrain_size;
        float Bottom_y = (y - m_visibleHalfSize) / terrain_size;
        float Rigth_x = (x + m_visibleHalfSize) / terrain_size;

        int terrainCount = m_asset.terrainIndex.Length;
        //左上
        int index = (int)(Top_y * column + Left_x);
        string leftTop_Name = GetTerrainNameByIndex(index);
        //右下
        index = (int)(Bottom_y * column + Rigth_x);
        string rightBottom_Name = GetTerrainNameByIndex(index);
        //判断是否在同一块地形内
        if (leftTop_Name == rightBottom_Name && !string.IsNullOrEmpty(leftTop_Name))
        {
            return GetTerrainMeshData_Resolusion(leftTop_Name);
        }
        //左下
        index = (int)(Bottom_y * column + Left_x);
        string leftBottom_Name = GetTerrainNameByIndex(index);
        //右上
        index = (int)(Top_y * column + Rigth_x);
        string rightTop_Name = GetTerrainNameByIndex(index);
        //获取最大分辨率
        int leftTop_resolusion = GetTerrainMeshData_Resolusion(leftTop_Name);
        int rightBottom_resolusion = GetTerrainMeshData_Resolusion(rightBottom_Name);
        int leftBottom_resolusion = GetTerrainMeshData_Resolusion(leftBottom_Name);
        int rightTop_resolusion = GetTerrainMeshData_Resolusion(rightTop_Name);
        return Mathf.Max(leftTop_resolusion, rightBottom_resolusion, leftBottom_resolusion, rightTop_resolusion);

    }
    #region TerrainMeshData Interface
    int GetTerrainMeshData_Resolusion(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 0;
        }
        return GetTerrainMeshData(name).resolusion;
    }
    TerrainMeshData GetTerrainMeshData(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        TerrainMeshData data = null;
        if (m_meshData.TryGetValue(name, out data))
        {
            return data;
        }
        data = LoadTerrainMeshData(name);
        data.Init();
        m_meshData.Add(name, data);
        return data;
    }
    TerrainMeshData LoadTerrainMeshData(string name)
    {
#if UNITY_EDITOR
        Debug.Log("LoadTerrainMeshData：" + name);
        return AssetDatabase.LoadAssetAtPath<TerrainMeshData>(string.Format("Assets/Data_oe/world/terrains/meshdata/{0}.asset", name));
#endif
    }
    #endregion
    string GetTerrainNameByPos(float x, float y)
    {
        int terrain_size = m_asset.terrain_Size;
        int column = m_asset.terrain_Column_Count;
        int index = (int)(y * column + x);
        return GetTerrainNameByIndex(index);
    }
    string GetTerrainNameByIndex(int index)
    {
        int terrainCount = m_asset.terrainIndex.Length;
        if (index < terrainCount && index >= 0)
        {
            return m_asset.terrainIndex[index];
        }
        return null;
    }
    void ClearMeshData()
    {
        m_vertices.Clear();
        m_uvs.Clear();
        m_subMeshIndexDic.Clear();
        for (int i = 0; i < m_triangles.Count; i++)
        {
            m_triangles[i].Clear();
        }
    }
}
