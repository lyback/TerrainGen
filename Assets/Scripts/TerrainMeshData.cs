using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainMeshData : ScriptableObject
{
    public int matIndex;
    public Material material;
	public int resolusion;
    public List<int> indexes;
    public List<float> heights;
    public Dictionary<int, float> heightMap;

    public void Init()
    {
        if (heightMap != null)
        {   
            return;
        }
        heightMap = new Dictionary<int, float>();
        for (int i = 0; i < indexes.Count; i++)
        {
            heightMap.Add(indexes[i], heights[i]);
        }
    }
}
