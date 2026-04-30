using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System;

public class WFCSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] float spawnDelay = 0f;
    [SerializeField] bool instantSpawn = false;

    [Header("Prefab Sets")]
    [SerializeField] GameObject[] prefabs;

    [Header("Grid Settings")]
    [SerializeField] int unitLength = 1;
    [SerializeField] bool skipBedrock = true;
    [SerializeField] int gridX, gridY, gridZ;

    [Header("JSON")]
    public TextAsset adjacenciesJSON;
    public TextAsset weightsJSON;
    public TextAsset numToNamJSON;
    public TextAsset namToNumJSON;

    [Header("Material")]
    [SerializeField] Color tileColor = Color.white;

    Dictionary<int, Tile> tilesDict;
    Dictionary<string, float> weightsDict;
    Dictionary<int, string> numToNamDict;
    Dictionary<string, int> namToNumDict;

    List<GameObject> spawnedObjects = new();
    int setIndex = 6;

    void Start()
    {
        LoadJSONData();

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError("No prefabs assigned.");
            return;
        }
        StartCoroutine(GenerateAndSpawn());
    }

    /// <summary>
    /// Generates a terrain grid and spawns its contents in increments or instantaneously.
    /// </summary>
    IEnumerator GenerateAndSpawn()
    {
        Vector3 offset = new Vector3(gridX * unitLength / 2f, 0, gridZ * unitLength / 2f);

        while (true)
        {
            Task<WFCResult> task = Task.Run(() => RunWFC());
            yield return new WaitUntil(() => task.IsCompleted);

            ClearSpawned();
            setIndex = (setIndex + 1) % prefabs.Length;
            if (instantSpawn)
            {
                foreach (var pos in task.Result.collapseOrder)
                    SpawnAt(pos, task.Result.grid, offset);
            }
            else
            {
                yield return StartCoroutine(SpawnSequential(task.Result, offset));
            }
        }
    }

    /// <summary>
    /// Spawns tiles sequentially
    /// </summary>
    IEnumerator SpawnSequential(WFCResult result, Vector3 offset)
    {
        foreach (var pos in result.collapseOrder)
        {
            int tileID = result.grid[pos.x, pos.y, pos.z].constraintCompliant[0];
            string tileName = numToNamDict[tileID];

            if (tileName == "air_0") continue;
            if (tileName == "bedrock_0" && skipBedrock) continue;

            Transform mesh = prefabs[setIndex].transform.Find(tilesDict[tileID].mesh);
            if (mesh == null) continue;

            SpawnAt(pos, result.grid, offset);

            spawnDelay = Math.Min(0, spawnDelay);
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    /// <summary>
    /// Spawns tiles in a given location, in addition to altering material properties
    /// </summary>
    void SpawnAt(Vector3Int pos, Cell[,,] grid, Vector3 offset)
    {
        int tileName = grid[pos.x, pos.y, pos.z].constraintCompliant[0];
        Tile tile = tilesDict[tileName];

        Transform mesh = prefabs[setIndex].transform.Find(tile.mesh);
        if (mesh == null) return;

        GameObject obj = Instantiate(mesh.gameObject, TileToWorld(pos) - offset, Quaternion.Euler(0, 180 + tile.rotation * -90f, 0));

        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            r.material.color = tileColor;
            r.material.SetFloat("_Metallic", 0);
            r.material.SetFloat("_Smoothness", 0);
        }

        spawnedObjects.Add(obj);
    }

    /// <summary>
    /// Calculates world positions of a given tile
    /// </summary>
    /// 
    Vector3 TileToWorld(Vector3Int cell)
    {
        return new Vector3((cell.x + 0.5f) * unitLength, cell.y * unitLength, (cell.z + 0.5f) * unitLength);
    }

    /// <summary>
    /// Clears spawned tiles
    /// </summary>
    void ClearSpawned()
    {
        foreach (GameObject obj in spawnedObjects)
            if (obj != null) Destroy(obj);
        spawnedObjects.Clear();
    }

    /// <summary>
    /// Generates a new instance of the Wave Function collapse algorithm
    /// </summary>
    WFCResult RunWFC()
    {
        return new WaveFunctionCollapse(new Vector3Int(gridX, gridY, gridZ), tilesDict, weightsDict, numToNamDict, namToNumDict).WFC();
    }

    /// <summary>
    /// Reads in JSON files into accessible dictionaries.
    /// </summary>
    void LoadJSONData()
    {
        tilesDict = JsonConvert.DeserializeObject<Dictionary<int, Tile>>(adjacenciesJSON.text);
        weightsDict = JsonConvert.DeserializeObject<Dictionary<string, float>>(weightsJSON.text);
        numToNamDict = JsonConvert.DeserializeObject<Dictionary<int, string>>(numToNamJSON.text);
        namToNumDict = JsonConvert.DeserializeObject<Dictionary<string, int>>(namToNumJSON.text);
    }
}