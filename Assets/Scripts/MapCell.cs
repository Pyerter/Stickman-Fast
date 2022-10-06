using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapCell : MonoBehaviour
{
    [System.Flags]
    public enum Direction
    {
        NONE = 0,
        UP = 1,
        RIGHT = 2,
        DOWN = 4,
        LEFT = 8
    }

    [SerializeField] public Direction directions;
    public List<int> GetDirections()
    {
        List<int> validDirections = new List<int>();
        for (int i = 1; i <= 8; i *= 2)
        {
            if ((i & (int)directions) > 0)
            {
                validDirections.Add((int)directions);
            }
        }
        return validDirections;
    }
    public static Direction MirrorDirection(Direction dir)
    {
        Direction mirrored = 0;
        if ((dir & Direction.UP) > 0)
        {
            mirrored |= Direction.DOWN;
        }
        if ((dir & Direction.DOWN) > 0)
        {
            mirrored |= Direction.UP;
        }
        if ((dir & Direction.LEFT) > 0)
        {
            mirrored |= Direction.RIGHT;
        }
        if ((dir & Direction.RIGHT) > 0)
        {
            mirrored |= Direction.LEFT;
        }
        return mirrored;
    }
    public static Vector2Int DirectionToVector(Direction dir)
    {
        Vector2Int vec = Vector2Int.zero;
        if ((dir & Direction.UP) > 0)
        {
            vec += Vector2Int.up;
        }
        if ((dir & Direction.DOWN) > 0)
        {
            vec += Vector2Int.down;
        }
        if ((dir & Direction.RIGHT) > 0)
        {
            vec += Vector2Int.right;
        }
        if ((dir & Direction.LEFT) > 0)
        {
            vec += Vector2Int.left;
        }
        return vec;
    }
    public static Direction VectorToDirection(Vector2Int vec)
    {
        Direction dir = 0;
        if (vec.y > 0)
        {
            dir |= Direction.UP;
        } else if (vec.y < 0)
        {
            dir |= Direction.DOWN;
        }
        if (vec.x > 0)
        {
            dir |= Direction.RIGHT;
        } else if (vec.x < 0)
        {
            dir |= Direction.LEFT;
        }
        return dir;
    }
    public static bool DirectionHasAny(Direction given, Direction target)
    {
        return (given & target) > 0;
    }
    public static bool DirectionHasAll(Direction given, Direction target)
    {
        return (given & target) == target;
    }

    [SerializeField] Collider2D detectionSquare;
    [SerializeField] public static Vector2Int default_size = new Vector2Int(200, 200);
    [SerializeField] public Vector2Int size = new Vector2Int(200, 200);

    public MapCell[] adjacentCells = new MapCell[4] { null, null, null, null };
    public CellMaster cellMaster = null;
    public PlayerController playerController = null;

    [SerializeField] public bool willDegenerate = false;
    [SerializeField] public float degenerationGrace = 0.05f;
    [SerializeField] public float degenerationRequest = 0f;

    /** Get the index of the passed direction. */
    public static int DirIndex(Direction dir)
    {
        return (int)Mathf.Log((int)dir, 2);
    }
    /** Get the direction of a given index. */
    public static Direction IndexDir(int index)
    {
        return (Direction)Mathf.Pow(2, index);
    }

    private void Awake()
    {
        if (detectionSquare == null)
        {
            detectionSquare = GetComponent<Collider2D>();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent<PlayerController>(out PlayerController player))
        {
            playerController = player;
            willDegenerate = false;
            GenerateNeighbors();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.TryGetComponent<PlayerController>(out PlayerController player))
        {
            playerController = null;
            QueueDegeneration();
        }
    }

    public void GenerateNeighbors()
    {
        cellMaster.GenerateCellNeighbors(this);
    }

    public void QueueDegeneration()
    {
        willDegenerate = true;
        degenerationRequest = Time.fixedTime;
    }

    public void DegenerateNeighbors()
    {
        cellMaster.DegenerateCellNeighbors(this);
    }

    public void PlaceCell(CellMaster cellMaster, Vector2Int loc, MapCell originCell, Direction generationDirection)
    {
        Direction indexDir = MirrorDirection(generationDirection);
        try
        {
            this.cellMaster = cellMaster;
            transform.position = new Vector3(loc.x, loc.y, 0);
            adjacentCells[DirIndex(indexDir)] = originCell;
        } catch (Exception e)
        {
            Debug.Log("Failed placing cell with origin to direction " + DirIndex(indexDir) + ":" + DirectionToVector(indexDir) + " at coord " + loc + "\nError: " + e.Message);
        }
    }

    void FixedUpdate()
    {
        if (willDegenerate)// && Time.fixedTime - degenerationRequest > degenerationGrace)
        {
            DegenerateNeighbors();
            willDegenerate = false;
            degenerationRequest = 0f;
        }
    }
}
