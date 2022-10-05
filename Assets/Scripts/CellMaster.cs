using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellMaster : MonoBehaviour
{
    public static IntPair[] PAIR_DIRECTIONS = new IntPair[] { new IntPair(0, 1), new IntPair(1, 0), new IntPair(0, -1), new IntPair(-1, 0) };
    public struct IntPair
    {
        public int left;
        public int right;
        public IntPair(int leftI, int rightI) { left = leftI; right = rightI; }
        public static IntPair operator +(IntPair intP) => intP;
        public static IntPair operator -(IntPair intP) => new IntPair(-intP.left, -intP.right);
        public static IntPair operator +(IntPair intP, IntPair intP2)
        {
            return new IntPair(intP.left + intP2.left, intP.right + intP2.right);
        }
        public static IntPair operator -(IntPair intP, IntPair intP2)
        {
            return new IntPair(intP.left - intP2.right, intP.right - intP2.right);
        }
    }
    public static Vector2Int IntPairToVector(IntPair intp)
    {
        return new Vector2Int(intp.left, intp.right);
    }
    public static IntPair VectorToIntPair(Vector2Int vec)
    {
        return new IntPair(vec.x, vec.y);
    }

    public Dictionary<IntPair, MapCell> cellMap = new Dictionary<IntPair, MapCell>();
    [SerializeField] public MapCell[] preAddedCells = new MapCell[1];

    [SerializeField] public List<MapCell> possibleCells = new List<MapCell>();

    private void Awake()
    {
        foreach (MapCell cell in preAddedCells)
        {
            IntPair coord = GetCoordinate(cell);
            cellMap.Add(coord, cell);
        }
    }

    public IntPair GetCoordinate(MapCell cell)
    {
        return new IntPair((int)(cell.transform.position.x / cell.size.x), (int)(cell.transform.position.y / cell.size.y));
    }

    public bool GetCellOptions(IntPair coord, out List<MapCell> options)
    {
        bool[] requiredDirs = new bool[] { false, false, false, false };
        bool[] optionalDirs = new bool[] { true, true, true, true };
        for (int i = 0; i < PAIR_DIRECTIONS.Length; i++)
        {
            MapCell.Direction dir = MapCell.VectorToDirection(IntPairToVector(PAIR_DIRECTIONS[i]));
            MapCell cell;
            if (cellMap.TryGetValue(coord + PAIR_DIRECTIONS[i], out cell))
            {
                optionalDirs[i] = false;
                if ((cell.directions & MapCell.MirrorDirection(dir)) > 0)
                {
                    requiredDirs[i] = true;
                }
            }
        }
        MapCell.Direction requiredDir = MapCell.Direction.NONE;
        MapCell.Direction optionalDir = MapCell.Direction.NONE;
        for (int i = 0, j = 1; i < 4; i++)
        {
            if (requiredDirs[i])
                requiredDir |= (MapCell.Direction)j;
            else if (optionalDirs[i])
                optionalDir |= (MapCell.Direction)j;
            j *= 2;
        }

        options = new List<MapCell>();
        foreach (MapCell option in possibleCells)
        {
            if (ValidCell(option, requiredDir, optionalDir))
            {
                options.Add(option);
            }
        }

        return options.Count > 0;
    }

    public bool ValidCell(MapCell cell, MapCell.Direction required, MapCell.Direction optional)
    {
        return MapCell.DirectionHasAll(cell.directions, required) && MapCell.DirectionHasAny(cell.directions, optional);
    }

    public void GenerateCellNeighbors(MapCell cell)
    {
        IntPair coord = GetCoordinate(cell);
        for (int i = 1; i <= 8; i*=2)
        {
            MapCell.Direction dir = (MapCell.Direction)i;
            int index = MapCell.DirIndex(dir);
            IntPair targetCoord = coord + VectorToIntPair(MapCell.DirectionToVector(dir));
            if ((cell.directions & dir) > 0 && !(cellMap.ContainsKey(targetCoord)))
            {
                GenerateCell(targetCoord, dir, cell);
            }
        }
    }

    public bool GenerateCell(IntPair coord, MapCell.Direction generatedFrom, MapCell genCell)
    {
        List<MapCell> options;
        if (GetCellOptions(coord, out options))
        {
            int selection = Random.Range(0, options.Count);
            MapCell generatedCell = Instantiate<MapCell>(options[selection]);
            generatedCell.PlaceCell(this, IntPairToVector(coord) * generatedCell.size, genCell, generatedFrom);
            cellMap.Add(coord, generatedCell);
            return true;
        }
        return false;
    }

    public void DegenerateCellNeighbors(MapCell cell)
    {
        if (cell.playerController != null)
            return;

        IntPair coord = GetCoordinate(cell);
        for (int i = 1; i <= 8; i *= 2)
        {
            MapCell.Direction dir = (MapCell.Direction)i;
            int index = MapCell.DirIndex(dir);
            IntPair targetCoord = coord + VectorToIntPair(MapCell.DirectionToVector(dir));
            MapCell target;
            if (cellMap.TryGetValue(targetCoord, out target))
            {
                if (target.playerController != null)
                    continue;
                cellMap.Remove(targetCoord);
                cell.adjacentCells[index] = null;
                Destroy(target.gameObject);
            }
        }
    }
}
