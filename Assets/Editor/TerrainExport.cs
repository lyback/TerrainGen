using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
public class TerrainExport
{
    static string ExportFolder = "Assets/Data_oe/world/terrains";
    static string MeshDataExportFolder = "Assets/Data_oe/world/terrains/meshdata";
    static string ExportTexFolder = string.Format("{0}/textures", ExportFolder);
    static string ExportMatFolder = string.Format("{0}/materials", ExportFolder);
    static string CONTROL_MAP_NAME = "terrain_mix_";
    static string TERRAIN_RGB_SHADER = "Terrain/Terrain_RGB";
    static string TERRAIN_RGBA_SHADER = "Terrain/Terrain_RGBA";
    static string EXPORT_ROOT_NAME = "__EXPORT_TERRAINS__";
    static int TERRAIN_ROW_COUNT = 2;            //地形行数
    static int TERRAIN_COLUMN_COUNT = 2;            //地形列数
    static int TERRAIN_SIZE = 80;               //地形SIZE
    static int TERRAIN_VERSICE_COUNT = 256;               //地形最大顶点个数
    static int LAYER_GROUND = 8;
    static int TERRAIN_HEIGHT = 10;         // 地形水平面高度
    // static int CONTROL_MAP_SIZE = 512;      // 控制图尺寸

    [MenuItem("Terrain/TerrainConvert")]
    static void ConvertTerrains()
    {
        GameObject root = GameObject.Find("TerrainRoot");
        if (root == null)
        {
            EditorUtility.DisplayDialog("错误", "没有找到TerrainRoot", "确定");
            return;
        }

        List<Terrain> datas = new List<Terrain>();
        for (int i = 0; i < root.transform.childCount; ++i)
        {
            Transform child = root.transform.GetChild(i);

            Terrain terrain = child.GetComponent<Terrain>();
            if (terrain == null)
            {
                continue;
            }

            terrain.gameObject.layer = LAYER_GROUND;
            datas.Add(terrain);
        }

        ConvertTerrains(datas.ToArray());
    }
    static void ConvertTerrains(Terrain[] terrains)
    {
        //一些数据清理
        tempTerrMeshDataDic.Clear();
        matIndex = 0;

        int current = 0;
        int total = terrains.Length + 3;

        //创建TerrainAssets索引配置
        EditorUtility.DisplayProgressBar("Generate...", string.Format("生成地形索引配置"), Mathf.InverseLerp(0, total, current++));
        var terrainAssets = ExportTerrainIndexAssets(terrains);

        //生成TerrainMeshData
        foreach (Terrain terrain in terrains)
        {
            EditorUtility.DisplayProgressBar("Generate...", string.Format("生成地形数据配置:{0}", terrain.name), Mathf.InverseLerp(0, total, current++));
            ExportTerrainMeshData(terrain.terrainData);
        }

        EditorUtility.DisplayProgressBar("Generate...", "导出服务器数据", Mathf.InverseLerp(0, total, current++));
        ExportServerData();

        EditorUtility.DisplayProgressBar("Generate...", "生成运行时地形节点", Mathf.InverseLerp(0, total, current++));
        CreateTerrainRoot(terrainAssets);

        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }
    //创建TerrainAssets索引配置
    static TerrainAssets ExportTerrainIndexAssets(Terrain[] terrains)
    {
        string pathTerrainAssets = string.Format("{0}/TerrainData.asset", ExportFolder);
        TerrainAssets terrainAssets = GetStriptableObject<TerrainAssets>(pathTerrainAssets);
        terrainAssets.terrain_Row_Count = TERRAIN_ROW_COUNT;
        terrainAssets.terrain_Column_Count = TERRAIN_COLUMN_COUNT;
        terrainAssets.terrain_Size = TERRAIN_SIZE;
        terrainAssets.terrainIndex = new string[TERRAIN_ROW_COUNT * TERRAIN_COLUMN_COUNT];
        foreach (Terrain terrain in terrains)
        {
            TerrainData terrainData = terrain.terrainData;
            if (terrainData.size.x == TERRAIN_SIZE && terrainData.size.z == TERRAIN_SIZE)
            {
                int x = (int)terrain.transform.localPosition.x;
                int z = (int)terrain.transform.localPosition.z;
                if (x % TERRAIN_SIZE == 0 && z % TERRAIN_SIZE == 0)
                {
                    int index_x = x / TERRAIN_SIZE;
                    int index_y = z / TERRAIN_SIZE;
                    if (index_x > TERRAIN_ROW_COUNT || index_y > TERRAIN_COLUMN_COUNT)
                    {
                        Debug.LogErrorFormat("地形{0}坐标超出地图行：{1}列：{2}：{3}", terrain.name, TERRAIN_ROW_COUNT, TERRAIN_COLUMN_COUNT, terrain.transform.localPosition);
                        continue;
                    }
                    int index = index_y * TERRAIN_COLUMN_COUNT + index_x;
                    terrainAssets.terrainIndex[index] = terrainData.name;
                }
                else
                {
                    Debug.LogErrorFormat("地形{0}坐标不是Size：{1}的倍数：{2}", terrain.name, TERRAIN_SIZE, terrain.transform.localPosition);
                }
            }
            else
            {
                Debug.LogErrorFormat("地形{0}大小错误：{1}", terrain.name, terrainData.size);
            }
        }
        UnityEditor.EditorUtility.SetDirty(terrainAssets);
        UnityEditor.AssetDatabase.SaveAssets();
        return terrainAssets;
    }
    static Dictionary<string, TerrainMeshData> tempTerrMeshDataDic = new Dictionary<string, TerrainMeshData>();
    
    //生成TerrainMeshData
    static void ExportTerrainMeshData(TerrainData terrainData)
    {
        if (tempTerrMeshDataDic.ContainsKey(terrainData.name))
        {
            return;
        }
        string pathMeshData = string.Format("{0}/{1}.asset", MeshDataExportFolder, terrainData.name);
        TerrainMeshData meshData = GetStriptableObject<TerrainMeshData>(pathMeshData);
        //网格分辨率
        meshData.resolusion = terrainData.heightmapResolution;
        //采样高度信息
        SamplingHeightMap(terrainData, ref meshData);
        //采样控制贴图
        bool useAphla = terrainData.splatPrototypes.Length % 4 == 0; //基础图素是4的倍数用RGBA
        List<Color[]> controlMapColors = SamplingControlMap(terrainData, useAphla);
        //创建控制贴图
        for (int i = 0; i < controlMapColors.Count; i++)
        {
            CreateTexture(controlMapColors[i], terrainData.name.ToLower(), i, useAphla ? TextureFormat.RGBA32 : TextureFormat.RGB24);
        }
        //创建材质
        CreateMat(terrainData, ref meshData, useAphla);

        tempTerrMeshDataDic.Add(terrainData.name, meshData);
        UnityEditor.EditorUtility.SetDirty(meshData);
    }
    //采样高度信息
    static void SamplingHeightMap(TerrainData terrainData, ref TerrainMeshData meshData)
    {
        meshData.indexes = new List<int>();
        meshData.heights = new List<float>();
        float[,] datas = terrainData.GetHeights(0, 0, terrainData.heightmapWidth, terrainData.heightmapHeight);
        for (int i = 0; i < terrainData.heightmapWidth; i++)
        {
            for (int j = 0; j < terrainData.heightmapHeight; j++)
            {
                float height = datas[i, j] * terrainData.size.y - TERRAIN_HEIGHT;
                if (height > 0.01 || height < -0.01)
                {
                    int index = j * terrainData.heightmapWidth + i;
                    meshData.indexes.Add(index);
                    meshData.heights.Add(height);
                }
            }
        }
    }
    //采样控制贴图
    static List<Color[]> SamplingControlMap(TerrainData terrainData, bool useAphla)
    {
        List<Color[]> controlMapColors = new List<Color[]>();
        float[,,] alphas = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
        int baseMapCount = terrainData.splatPrototypes.Length;
        int CONTROL_MAP_SIZE = terrainData.alphamapResolution;

        int crontrolMapCount = useAphla ? (int)(baseMapCount / 4f) : (baseMapCount % 3 == 0 ? baseMapCount / 3 : (int)(baseMapCount / 3f + 1));
        for (int i = 0; i < crontrolMapCount; i++)
        {
            controlMapColors.Add(new Color[CONTROL_MAP_SIZE * CONTROL_MAP_SIZE]);
        }

        for (int x = 0; x < CONTROL_MAP_SIZE; x++)
        {
            for (int y = 0; y < CONTROL_MAP_SIZE; y++)
            {
                int indexAphla = 0;
                for (int i = 0; i < crontrolMapCount; i++)
                {
                    indexAphla = useAphla ? i * 4 : i * 3;
                    int indexPos = x * CONTROL_MAP_SIZE + y;
                    controlMapColors[i][indexPos].r = indexAphla < baseMapCount ? alphas[x, y, indexAphla++] : 0;
                    controlMapColors[i][indexPos].g = indexAphla < baseMapCount ? alphas[x, y, indexAphla++] : 0;
                    controlMapColors[i][indexPos].b = indexAphla < baseMapCount ? alphas[x, y, indexAphla++] : 0;
                    if (useAphla)
                    {
                        controlMapColors[i][indexPos].a = indexAphla < baseMapCount ? alphas[x, y, indexAphla++] : 0;
                    }
                }
            }
        }
        return controlMapColors;
    }
    //创建控制贴图
    static void CreateTexture(Color[] colors, string name, int index, TextureFormat textureFormat)
    {
        int size = (int)Mathf.Sqrt(colors.Length);
        Texture2D textureControl = new Texture2D(size, size, textureFormat, false);
        textureControl.wrapMode = TextureWrapMode.Clamp;
        textureControl.anisoLevel = 9;
        textureControl.SetPixels(colors);
        textureControl.Apply();

        string texDir = string.Format("{0}/{1}", ExportTexFolder, name);
        if (!Directory.Exists(texDir))
        {
            Directory.CreateDirectory(texDir);
        }
        string pathTexture = string.Format("{0}/{1}{2}_{3}.png", texDir, CONTROL_MAP_NAME, name, index);
        byte[] data = textureControl.EncodeToPNG();
        File.WriteAllBytes(pathTexture, data);

        AssetDatabase.ImportAsset(pathTexture, ImportAssetOptions.ForceUpdate);
        TextureImporter import = TextureImporter.GetAtPath(pathTexture) as TextureImporter;
        if (import != null)
        {
            import.isReadable = true;
            import.anisoLevel = 9;
            import.mipmapEnabled = false;
            import.wrapMode = TextureWrapMode.Repeat;
            import.textureCompression = TextureImporterCompression.Uncompressed;
            AssetDatabase.ImportAsset(pathTexture, ImportAssetOptions.ForceUpdate);
        }
    }
    static int matIndex = 0;
    //创建材质
    static void CreateMat(TerrainData terrainData, ref TerrainMeshData meshdata, bool useAphla)
    {
        if (!Directory.Exists(ExportMatFolder))
        {
            Directory.CreateDirectory(ExportMatFolder);
        }
        string pathMaterial = string.Format("{0}/{1}.mat", ExportMatFolder, terrainData.name.ToLower());
        Shader shader = useAphla ? Shader.Find(TERRAIN_RGBA_SHADER) : Shader.Find(TERRAIN_RGB_SHADER);
        Material material = null;
        if (File.Exists(pathMaterial))
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(pathMaterial);
        }
        else
        {
            material = new Material(shader);
        }
        material.shader = shader;
        //设置基础纹理
        for (int i = 0; i < terrainData.splatPrototypes.Length; ++i)
        {
            Texture2D splat = null;
            Vector2 scale = Vector2.zero;
            SplatPrototype sp = terrainData.splatPrototypes[i];
            splat = sp.texture;
            scale = new Vector2 (terrainData.size.x / sp.tileSize.x, terrainData.size.x / sp.tileSize.y);
            material.SetTexture("_Splat" + (i + 1), splat);
            material.SetTextureScale ("_Splat" + (i + 1), scale);
        }
        //设置控制纹理
        int baseMapCount = terrainData.splatPrototypes.Length;
        int crontrolMapCount = useAphla ? (int)(baseMapCount / 4f) : (baseMapCount % 3 == 0 ? baseMapCount / 3 : (int)(baseMapCount / 3f + 1));
        for (int i = 0; i < crontrolMapCount; i++)
        {
            string name = terrainData.name.ToLower();
            string texDir = string.Format("{0}/{1}", ExportTexFolder, name);
            string pathTexture = string.Format("{0}/{1}{2}_{3}.png", texDir, CONTROL_MAP_NAME, name, i);
            Texture2D textureControl = AssetDatabase.LoadAssetAtPath<Texture2D>(pathTexture);
            material.SetTexture("_Control" + (i + 1), textureControl);
        }
        if (File.Exists(pathMaterial))
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
        else
        {
            AssetDatabase.CreateAsset(material, pathMaterial);
        }
        meshdata.matIndex = matIndex;
        matIndex++;
        meshdata.material = material;
        AssetDatabase.ImportAsset(pathMaterial, ImportAssetOptions.ForceUpdate);
    }
    static void CreateTerrainRoot(TerrainAssets terrainAssets)
    {
        GameObject root = GameObject.Find(EXPORT_ROOT_NAME);
        GameObject.DestroyImmediate(root);
        root = new GameObject(EXPORT_ROOT_NAME);
        TerrainRoot hierarchy = root.AddComponent<TerrainRoot>();

        GameObject go = new GameObject("Terrain");
        go.transform.SetParent(root.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        //MeshFilter
        MeshFilter filter = go.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = go.AddComponent<MeshFilter>();
        }
        //MeshRenderer
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = go.AddComponent<MeshRenderer>();
        }
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        //Material

        Dictionary<int, Material> matDic = new Dictionary<int, Material>();
        for (int i = 0; i < terrainAssets.terrainIndex.Length; i++)
        {
            string terrainName = terrainAssets.terrainIndex[i];
            if (string.IsNullOrEmpty(terrainName))
            {
                continue;
            }
            string pathMeshData = string.Format("{0}/{1}.asset", MeshDataExportFolder, terrainAssets.terrainIndex[i]);
            TerrainMeshData meshData = GetStriptableObject<TerrainMeshData>(pathMeshData);
            if (!matDic.ContainsKey(meshData.matIndex))
            {
                matDic.Add(meshData.matIndex, meshData.material);
            }
        }
        Material[] mats = new Material[matDic.Count];
        foreach (var kv in matDic)
        {
            mats[kv.Key] = kv.Value;
        }
        renderer.sharedMaterials = mats;
        //BoxCollider
        BoxCollider collider = go.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = go.AddComponent<BoxCollider>();
        }

        hierarchy.RootTerrain = go;

        string path = string.Format("{0}/TerrainRoot.prefab", ExportFolder);
        if (File.Exists(path))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            PrefabUtility.ReplacePrefab(root, prefab);
        }
        else
        {
            PrefabUtility.CreatePrefab(path, root);
        }
        GameObject.DestroyImmediate(root);
    }
    static void ExportServerData()
    {

    }

    static T GetStriptableObject<T>(string path) where T : ScriptableObject
    {
        T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
        }
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            UnityEditor.AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }
}