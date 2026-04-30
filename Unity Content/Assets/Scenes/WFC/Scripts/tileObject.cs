using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;

[System.Serializable]
public class Tile
{
    [JsonProperty("adjacency_dict")]
    public Dictionary<string, List<int>> adjacencyDict= new Dictionary<string, List<int>>();
    public string mesh;
    public int rotation;

    public float weight = 1.0f;
}
public class Cell
{
    public List<int> constraintCompliant;
    public bool collapsed = false;

    public bool grounded = false;
    
}
public class WFCResult
{
    public Cell[,,] grid;
    public List<Vector3Int> collapseOrder;
    public WFCResult(Cell[,,] grid2, List<Vector3Int> order)
    {
        grid = grid2;
        collapseOrder = order;
    }
}