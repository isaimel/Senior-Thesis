using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using System.Collections;

public class WaveFunctionCollapse
{
    public Dictionary<int, Tile> tilesDict;
    public Dictionary<string, float> weightsDict;
    public Dictionary<int, string> numToNamDict;
    public Dictionary<string, int> namToNumDict;
    List<Vector3Int> collapsedOrder;
    Vector3Int gridDims;
    public Cell[,,] tileGrid;
    
    System.Random rng = new System.Random();
    
    public WaveFunctionCollapse(Vector3Int gridDimensions, Dictionary<int, Tile> tilesDictionary, Dictionary<string, float> weightsDictionary, Dictionary<int, string> numToNamDictionary, Dictionary<string, int> namToNumDictionary)
    {
        tilesDict = new Dictionary<int, Tile>(tilesDictionary);
        weightsDict = new Dictionary<string, float>(weightsDictionary);
        numToNamDict = new Dictionary<int, string>(numToNamDictionary);
        namToNumDict = new Dictionary<string, int>(namToNumDictionary);
        gridDims = gridDimensions;
        collapsedOrder = new List<Vector3Int>();
        ApplyWeights();
    }
    /// <summary>
    /// Initializes the necessary components for terrain generation and conducts the WFC Loop, spawning the terrain if the loop successfully ends or stopping if an error occurs
    /// </summary>
    public WFCResult WFC()
    {   
        bool terrainComplete = false;
        while (!terrainComplete){
            collapsedOrder.Clear();
            InitializeGrid();
            FillHorizontalLayer(0, "bedrock_0");
            FillHorizontalLayer(gridDims.y-1, "air_0");
            terrainComplete = PrimaryWFCLoop();
        }
        return new WFCResult(tileGrid, new List<Vector3Int>(collapsedOrder));
    }
    /// <summary>
    /// Conducts the lowest entropy cell search, collapse, and propogation steps until the entirety is collapsed, or until an error is encountered
    /// </summary>
    bool PrimaryWFCLoop()
    {
        while (!IsGridCollapsed())
        {
            Vector3Int lowestEntropyCell = SelectCellToCollapse();
            if (lowestEntropyCell.x == -1)
            {
                return false;
            }
            Collapse(lowestEntropyCell);
            if (!Propogate(lowestEntropyCell)) return false;
        }
        return true;
    }

    #region "WFC Setup Functions"

    /// <summary>
    /// Corrects tiles' weight according to a dictionary.
    /// </summary>
    void ApplyWeights()
    {
        foreach (var entry in weightsDict)
        {
            string baseName = entry.Key;
            float weight = entry.Value;

            for (int i = 0; i < 4; i++)
            {
                string tileName = baseName + "_" + i;
                if (!namToNumDict.ContainsKey(tileName))
                {
                    Debug.Log($"{tileName} does not exist in the tileset.");
                    continue;
                }
                int baseNameNum = namToNumDict[tileName];
                tilesDict[baseNameNum].weight = weight;
            }
        }
    }
    #endregion

    #region "Collapse Helper Function"
    /// <summary>
    /// Iterates through the grid, selects a cell with the lowest entropy
    /// </summary>
    Vector3Int SelectCellToCollapse()
    {
        int lowestEntropy = int.MaxValue;

        List<Vector3Int> groundedCandidates = new List<Vector3Int>();
        List<Vector3Int> floatingCandidates = new List<Vector3Int>();

        for (int x = 0; x < gridDims.x; x++)
        {
            for (int y = 0; y < gridDims.y; y++)
            {
                for (int z = 0; z < gridDims.z; z++)
                {
                    Cell cell = tileGrid[x, y, z];
                    if (cell.collapsed) continue;

                    int count = cell.constraintCompliant.Count;

                    if (count == 0)
                    {
                        Debug.Log($"Cell [{x},{y},{z}] has zero constraint-complicant cells.");
                        return new Vector3Int(-1, -1, -1);
                    }

                    if (count < lowestEntropy)
                    {
                        lowestEntropy = count;
                        groundedCandidates.Clear();
                        floatingCandidates.Clear();
                    }

                    if (count == lowestEntropy)
                    {
                        Vector3Int pos = new Vector3Int(x, y, z);

                        if (cell.grounded)
                        {
                            groundedCandidates.Add(pos);
                        }
                        else
                        {
                            floatingCandidates.Add(pos);
                        }
                    }
                }
            }
        }

        if (groundedCandidates.Count > 0)
        {
            return groundedCandidates[rng.Next(groundedCandidates.Count)];
        }

        if (floatingCandidates.Count > 0)
        {
            return floatingCandidates[rng.Next(floatingCandidates.Count)];
        }
        Debug.Log("No valid candidates for collapse found.");
        return new Vector3Int(-1, -1, -1);
            
    }
    /// <summary>
    /// Given a coordinate, its corresponding cell is marked as collapsed and its constraint-compliant list is reduced to a single tile.
    /// </summary>
    void Collapse(Vector3Int lowEntCell)
    {
        Cell cell = GetCell(lowEntCell);
        int chosenTile = GetRandomTileWeighted(cell.constraintCompliant);
        cell.constraintCompliant = new List<int> { chosenTile };
        cell.collapsed = true;
        collapsedOrder.Add(lowEntCell);
        if (chosenTile != namToNumDict["air_0"]){
            if (!cell.grounded) cell.grounded = true;
            RedefineMidairNeighbors(lowEntCell);
        }
    }
    /// <summary>
    /// Sets a cell's directional neighbors to grounded if the cell is collapsed to a non-air tile.
    /// </summary>
    void RedefineMidairNeighbors(Vector3Int origin)
    {
        Vector3Int[] directions =
        {
            new Vector3Int(1,0,0),
            new Vector3Int(-1,0,0),
            new Vector3Int(0,1,0),
            new Vector3Int(0,-1,0),
            new Vector3Int(0,0,1),
            new Vector3Int(0,0,-1),
        };

        foreach (var dir in directions)
        {
            Vector3Int neighborPos = origin + dir;

            if (!ValidCoordinate(neighborPos))
                continue;

            Cell neighbor = GetCell(neighborPos);

            if (!neighbor.grounded) neighbor.grounded = true;
        }
    }
    #endregion

    #region "Propogation and Helper Functions"
    bool Propogate(Vector3Int lowEntCell)
    {    
        Dictionary<string, Vector3Int> faceVector = new()
        {
            { "+x", new Vector3Int(1,0,0)},
            { "-x", new Vector3Int(-1,0,0)},
            { "+y", new Vector3Int(0,1,0)},
            { "-y", new Vector3Int(0,-1,0)},
            { "+z", new Vector3Int(0,0,1)},
            { "-z", new Vector3Int(0,0,-1)}
        };
        
        Queue<Vector3Int> toPropogate = new Queue<Vector3Int>();
        toPropogate.Enqueue(lowEntCell);

        while (toPropogate.Count != 0)
        {
            Vector3Int originCoord = toPropogate.Dequeue();
            Cell originCell = GetCell(originCoord);
 
            if (originCell.constraintCompliant.Count == 0)
            {
                Debug.Log($"Cell [{originCoord.x}, {originCoord.y}, {originCoord.z}] has zero constraint-complicant cells.");
                return false;
            }

            foreach (var entry in faceVector)
            {
                string face = entry.Key;
                Vector3Int vector = entry.Value;

                Vector3Int neighborCoord = originCoord + vector;

                if (ValidCoordinate(neighborCoord) && !GetCell(neighborCoord).collapsed && !toPropogate.Contains(neighborCoord))
                {
                    Cell neighborCell = GetCell(neighborCoord);
                    HashSet<int> allDirAdjs = AggregateDirectionalAdjacencies(originCell.constraintCompliant, face);
                    List<int> newPoss = neighborCell.constraintCompliant.Intersect(allDirAdjs).ToList();
                    if (!Equivalent(newPoss, neighborCell.constraintCompliant))
                    {
                        neighborCell.constraintCompliant = newPoss;
                        toPropogate.Enqueue(neighborCoord);
                    }
                }
            }
        }
        return true;
    }
    /// <summary>
    /// Redifines a neighbor's constraint-compliant tiles to retain compliance with the current cell's tiles
    /// </summary>
    HashSet<int> AggregateDirectionalAdjacencies(List<int> originPoss, string face)
    {
        
        HashSet<int> finalSet = new HashSet<int>();

        foreach (int tile in originPoss)
        {
            if (!tilesDict.ContainsKey(tile)) continue;

            if (!tilesDict[tile].adjacencyDict.TryGetValue(face, out var faceAdjacency))
            {
                Debug.LogError($"Missing adjacency for {tile} and {face} + face");
                continue;
            }
            finalSet.UnionWith(faceAdjacency);
        }        
        return finalSet;
    }
    /// <summary>
    /// Performs an intersection between two lists
    /// </summary>
    List<int> Intersection(List<int> listA, List<int> listB)
    {
        HashSet<int> hashSet = new HashSet<int>(listA);
        hashSet.IntersectWith(listB);
        return hashSet.ToList();
    }
    /// <summary>
    /// Determines whether two lists are equivalent (same length and content regardless of order)
    /// </summary>
    bool Equivalent(List<int> a, List<int> b)
    {
        return a.Count == b.Count && !a.Except(b).Any();
    }
    #endregion

    #region "Cell Getters and Setters"
    /// <summary>
    /// Retrieves a cell from the grid corresponding to the given coordinate.
    /// </summary>
    Cell GetCell(Vector3Int origin)
    {
        return tileGrid[origin.x, origin.y, origin.z];
    }
    /// <summary>
    /// Empties a cell's constraint compliant tile list, defining a given tile as its single member.
    /// </summary>
    void DefineCell(Cell targetCell, int tile)
    {
        targetCell.constraintCompliant.Clear();
        targetCell.constraintCompliant.Add(tile);
    }
    /// <summary>
    /// Empties a cell's constraint compliant tile list, defining a given tile as its single member.
    /// </summary>
    void DefineCell(Vector3Int targetCell, int tile)
    {
        if (!ValidCoordinate(targetCell) || !tilesDict.ContainsKey(tile)) return;
        DefineCell(GetCell(targetCell), tile);
    }
    /// <summary>
    /// Selects a single tile from a cell's constraint compliant tile list through their respective weighted values. 
    /// </summary>
    int GetRandomTileWeighted(List<int> possibleTiles)
    {
        float totalWeight = 0;

        foreach (int tile in possibleTiles)
        {
            totalWeight += tilesDict[tile].weight;
        }

        float randomValue = (float)rng.NextDouble() * totalWeight;

        float cumulative = 0;
        foreach (int tile in possibleTiles)
        {
            cumulative += tilesDict[tile].weight;
            if (cumulative > randomValue)
            {
                return tile;
            }
        }

        return possibleTiles[0];
    }
    #endregion

    #region "Grid Initialization Functions"
    /// <summary>
    /// Fills the entirety of a horizontal plane at a given height with a uniform tile.
    /// </summary>
    void FillHorizontalLayer(int level, string tile)
    {
        if (level > gridDims.y-1 || level < 0) return;
        for (int x = 0; x < gridDims.x; x++)
        {
            for (int z = 0; z < gridDims.z; z++)
            {
                DefineCell(new Vector3Int(x, level, z), namToNumDict[tile]);
            }
        }
    }
    /// <summary>
    /// Creates an 3-entry array determined by the provided dimensions. Each cell contains a copy of every single tile ID in the tileset.
    /// </summary>
    void InitializeGrid()
    {
        tileGrid = new Cell[gridDims.x, gridDims.y, gridDims.z];
        List<int> allTileNames = new List<int>(tilesDict.Keys);

        for (int x = 0; x < gridDims.x; x++)
        {
            for (int y = 0; y < gridDims.y; y++)
            {
                for (int z = 0; z < gridDims.z; z++)
                {
                    tileGrid[x, y, z] = new Cell
                    {
                        constraintCompliant = new List<int>(allTileNames)
                    };
                }
            }
        }
    }
    #endregion

    #region "Grid and Cell Check Functions"
    /// <summary>
    /// Determines whether all cells in the grid have been collapsed
    /// </summary>
    bool IsGridCollapsed()
    {
        for (int x = 0; x < gridDims.x; x++)
        {
            for (int y = 0; y < gridDims.y; y++)
            {
                for (int z = 0; z < gridDims.z; z++)
                {
                    if (!tileGrid[x, y, z].collapsed)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }
    /// <summary>
    /// Determines whether a provided coordinate lies inside the grid boundaries
    /// </summary>
    bool ValidCoordinate(Vector3Int origin)
    {
        return origin.x >= 0 && origin.x < gridDims.x && origin.y >= 0 && origin.y < gridDims.y && origin.z >= 0 && origin.z < gridDims.z;
    }
    #endregion
}
