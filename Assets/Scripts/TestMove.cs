using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

public class TestMove : MonoBehaviour
{
    public TerrainRoot root;
    public int size = 1;
    void Start()
    {
        var obj = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format("Assets/Data_oe/world/terrains/{0}.prefab", "TerrainRoot"));
        root = Instantiate(obj).GetComponent<TerrainRoot>();
        var config = AssetDatabase.LoadAssetAtPath<TerrainAssets>(string.Format("Assets/Data_oe/world/terrains/{0}.asset", "TerrainData"));
        root.Init(config, size);
        
        var pos = transform.position;
        root.MoveTo(pos.x, pos.z);
    }
    void Update()
    {
        var pos = transform.position;
        root.MoveTo(pos.x, pos.z);
    }

}
