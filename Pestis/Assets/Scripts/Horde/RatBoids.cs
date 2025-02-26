using System.Linq;
using System.Runtime.InteropServices;
using Horde;
using MoreLinq.Extensions;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

internal struct Boid
{
    public float2 pos;
    public float2 vel;
}

public class RatBoids : MonoBehaviour
{
    private const float blockSize = 512f;

    [Header("Settings")] [SerializeField] private float maxSpeed = 2;

    [SerializeField] private float edgeMargin = .5f;
    [SerializeField] private float visualRange = .5f;
    [SerializeField] private float minDistance = 0.15f;
    [SerializeField] private float cohesionFactor = 2;
    [SerializeField] private float separationFactor = 1;
    [SerializeField] private float alignmentFactor = 5;
    [SerializeField] private float targetFactor = 0.5f;

    [SerializeField] private ComputeShader boidShader;
    [SerializeField] private ComputeShader gridShader;
    [SerializeField] private Material boidMat;

    private ComputeBuffer _boidPlayersBuffer;
    private ComputeBuffer _boidPlayersBufferOut;
    private bool _swapPlayerBuffer;
    private int blocks;

    private ComputeBuffer boidBuffer;
    private ComputeBuffer boidBufferOut;

    private NativeArray<Boid> boids;
    private NativeArray<Boid> boidsTemp;

    // Index is boid ID, x value is position flattened to 1D array, y value is grid cell offset
    private ComputeBuffer gridBuffer;
    private float gridCellSize;
    private int gridDimY, gridDimX, gridTotalCells;
    private ComputeBuffer gridOffsetBuffer;
    private ComputeBuffer gridOffsetBufferIn;
    private ComputeBuffer gridSumsBuffer;
    private ComputeBuffer gridSumsBuffer2;

    private HordeController horde;

    private float minSpeed;

    [Header("Performance")] private int numBoids = 5;
    private int previousNumBoids;
    private RenderParams rp;
    private GraphicsBuffer trianglePositions;
    private Vector2[] triangleVerts;
    private float turnSpeed;

    private int updateBoidsKernel, generateBoidsKernel;

    private int updateGridKernel,
        clearGridKernel,
        prefixSumKernel,
        sumBlocksKernel,
        addSumsKernel,
        rearrangeBoidsKernel;

    private float xBound, yBound;

    private float visualRangeSq => visualRange * visualRange;
    private float minDistanceSq => minDistance * minDistance;

    private void Awake()
    {
        triangleVerts = getTriangleVerts();
    }

    // Start is called before the first frame update
    private void Start()
    {
        horde = GetComponentInParent<HordeController>();

        xBound = Camera.main.orthographicSize * Camera.main.aspect - edgeMargin;
        yBound = Camera.main.orthographicSize - edgeMargin;
        turnSpeed = 0.04f;
        minSpeed = maxSpeed * 0.75f;

        // Create new instance of shaders to stop them sharing data!
        boidShader = Instantiate(boidShader);
        gridShader = Instantiate(gridShader);

        // Get kernel IDs
        updateBoidsKernel = boidShader.FindKernel("UpdateBoids");
        updateGridKernel = gridShader.FindKernel("UpdateGrid");
        clearGridKernel = gridShader.FindKernel("ClearGrid");
        prefixSumKernel = gridShader.FindKernel("PrefixSum");
        sumBlocksKernel = gridShader.FindKernel("SumBlocks");
        addSumsKernel = gridShader.FindKernel("AddSums");
        rearrangeBoidsKernel = gridShader.FindKernel("RearrangeBoids");
        generateBoidsKernel = boidShader.FindKernel("GenerateBoids");

        // Setup compute buffer
        boidBuffer = new ComputeBuffer(128, Marshal.SizeOf(typeof(Boid)));
        boidBufferOut = new ComputeBuffer(128, Marshal.SizeOf(typeof(Boid)));
        _boidPlayersBuffer = new ComputeBuffer(128, Marshal.SizeOf(typeof(int)));
        _boidPlayersBufferOut = new ComputeBuffer(128, Marshal.SizeOf(typeof(int)));

        boidShader.SetBuffer(updateBoidsKernel, "boidsIn", boidBufferOut);
        boidShader.SetBuffer(updateBoidsKernel, "boidsOut", boidBuffer);

        boidShader.SetInt("numBoids", numBoids);
        boidShader.SetFloat("maxSpeed", maxSpeed);
        boidShader.SetFloat("minSpeed", minSpeed);
        boidShader.SetFloat("edgeMargin", edgeMargin);
        boidShader.SetFloat("visualRangeSq", visualRangeSq);
        boidShader.SetFloat("minDistanceSq", minDistanceSq);
        boidShader.SetFloat("turnSpeed", turnSpeed);
        boidShader.SetFloat("xBound", xBound);
        boidShader.SetFloat("yBound", yBound);
        boidShader.SetFloat("cohesionFactor", cohesionFactor);
        boidShader.SetFloat("separationFactor", separationFactor);
        boidShader.SetFloat("alignmentFactor", alignmentFactor);
        boidShader.SetFloat("targetFactor", targetFactor);


        boidShader.SetBuffer(generateBoidsKernel, "boidsOut", boidBuffer);
        boidShader.SetInt("randSeed", Random.Range(0, int.MaxValue));
        boidShader.Dispatch(generateBoidsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);

        // Set render params
        rp = new RenderParams(new Material(boidMat));
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("boids", boidBuffer);
        rp.worldBounds = new Bounds(Vector3.zero, Vector3.one * 3000);
        trianglePositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, 8);
        trianglePositions.SetData(triangleVerts);
        rp.matProps.SetBuffer("_Positions", trianglePositions);

        // Spatial grid setup
        gridCellSize = visualRange;
        gridDimX = Mathf.FloorToInt(xBound * 2 / gridCellSize) + 30;
        gridDimY = Mathf.FloorToInt(yBound * 2 / gridCellSize) + 30;
        gridTotalCells = gridDimX * gridDimY;


        gridBuffer = new ComputeBuffer(128, 8);
        gridOffsetBuffer = new ComputeBuffer(gridTotalCells, 4);
        gridOffsetBufferIn = new ComputeBuffer(gridTotalCells, 4);
        blocks = Mathf.CeilToInt(gridTotalCells / blockSize);
        gridSumsBuffer = new ComputeBuffer(blocks, 4);
        gridSumsBuffer2 = new ComputeBuffer(blocks, 4);
        gridShader.SetInt("numBoids", numBoids);
        gridShader.SetInt("numBoidsPrevious", 0);
        gridShader.SetBuffer(updateGridKernel, "boids", boidBuffer);
        gridShader.SetBuffer(updateGridKernel, "gridBuffer", gridBuffer);
        gridShader.SetBuffer(updateGridKernel, "gridOffsetBuffer", gridOffsetBufferIn);
        gridShader.SetBuffer(updateGridKernel, "gridSumsBuffer", gridSumsBuffer);

        gridShader.SetBuffer(clearGridKernel, "gridOffsetBuffer", gridOffsetBufferIn);

        gridShader.SetBuffer(prefixSumKernel, "gridOffsetBuffer", gridOffsetBuffer);
        gridShader.SetBuffer(prefixSumKernel, "gridOffsetBufferIn", gridOffsetBufferIn);
        gridShader.SetBuffer(prefixSumKernel, "gridSumsBuffer", gridSumsBuffer2);

        gridShader.SetBuffer(addSumsKernel, "gridOffsetBuffer", gridOffsetBuffer);

        gridShader.SetBuffer(rearrangeBoidsKernel, "gridBuffer", gridBuffer);
        gridShader.SetBuffer(rearrangeBoidsKernel, "gridOffsetBuffer", gridOffsetBuffer);
        gridShader.SetBuffer(rearrangeBoidsKernel, "boids", boidBuffer);
        gridShader.SetBuffer(rearrangeBoidsKernel, "boidsOut", boidBufferOut);

        gridShader.SetFloat("gridCellSize", gridCellSize);
        gridShader.SetInt("gridDimY", gridDimY);
        gridShader.SetInt("gridDimX", gridDimX);
        gridShader.SetInt("gridTotalCells", gridTotalCells);
        gridShader.SetInt("blocks", blocks);

        boidShader.SetBuffer(updateBoidsKernel, "gridOffsetBuffer", gridOffsetBuffer);
        boidShader.SetFloat("gridCellSize", gridCellSize);
        boidShader.SetInt("gridDimY", gridDimY);
        boidShader.SetInt("gridDimX", gridDimX);
    }

    // Update is called once per frame
    private void Update()
    {
        previousNumBoids = numBoids;
        var newNumBoids = horde.AliveRats;
        if (boidBuffer.count < newNumBoids)
        {
            Debug.Log($"Resizing boid buffers to {newNumBoids * 2}");
            var newBuffer = new ComputeBuffer(newNumBoids * 2, Marshal.SizeOf(typeof(Boid)));
            var boids = new Boid[numBoids];
            boidBuffer.GetData(boids, 0, 0, numBoids);
            newBuffer.SetData(boids);
            boidBuffer.Release();
            boidBuffer = newBuffer;

            newBuffer = new ComputeBuffer(newNumBoids * 2, Marshal.SizeOf(typeof(Boid)));
            boidBufferOut.GetData(boids, 0, 0, numBoids);
            newBuffer.SetData(boids);
            boidBufferOut.Release();
            boidBufferOut = newBuffer;

            newBuffer = new ComputeBuffer(newNumBoids * 2, Marshal.SizeOf(typeof(int)));
            var players = new int[numBoids];
            _boidPlayersBuffer.GetData(players, 0, 0, numBoids);
            newBuffer.SetData(players);
            _boidPlayersBuffer.Release();
            _boidPlayersBuffer = newBuffer;

            newBuffer = new ComputeBuffer(newNumBoids * 2, Marshal.SizeOf(typeof(int)));
            players = new int[numBoids];
            _boidPlayersBufferOut.GetData(players, 0, 0, numBoids);
            newBuffer.SetData(players);
            _boidPlayersBufferOut.Release();
            _boidPlayersBufferOut = newBuffer;

            // Resize grid buffer
            var grid = new uint2[numBoids];
            newBuffer = new ComputeBuffer(newNumBoids * 2, 8);
            gridBuffer.GetData(grid, 0, 0, numBoids);
            newBuffer.SetData(grid);
            gridBuffer.Release();
            gridBuffer = newBuffer;

            boidShader.SetBuffer(updateBoidsKernel, "boidsIn", boidBufferOut);
            boidShader.SetBuffer(updateBoidsKernel, "boidsOut", boidBuffer);

            gridShader.SetBuffer(updateGridKernel, "boids", boidBuffer);
            gridShader.SetBuffer(updateGridKernel, "gridBuffer", gridBuffer);

            gridShader.SetBuffer(rearrangeBoidsKernel, "gridBuffer", gridBuffer);
            gridShader.SetBuffer(rearrangeBoidsKernel, "boids", boidBuffer);
            gridShader.SetBuffer(rearrangeBoidsKernel, "boidsOut", boidBufferOut);

            rp.matProps.SetBuffer("boids", boidBuffer);
        }

        // Increase separation force the bigger the horde is.
        boidShader.SetFloat("separationFactor", separationFactor * (numBoids / 1000.0f));

        boidShader.SetFloat("deltaTime", Time.deltaTime);
        boidShader.SetFloats("targetPos", horde.targetLocation.transform.position.x,
            horde.targetLocation.transform.position.y);

        boidShader.SetInt("numBoids", horde.AliveRats);
        numBoids = newNumBoids;

        // Clear indices
        gridShader.Dispatch(clearGridKernel, blocks, 1, 1);

        // Populate grid
        gridShader.Dispatch(updateGridKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);

        // Generate Offsets (Prefix Sum)
        // Offsets in each block
        gridShader.Dispatch(prefixSumKernel, blocks, 1, 1);

        // Offsets for sums of blocks
        var swap = false;
        for (var d = 1; d < blocks; d *= 2)
        {
            gridShader.SetBuffer(sumBlocksKernel, "gridSumsBufferIn", swap ? gridSumsBuffer : gridSumsBuffer2);
            gridShader.SetBuffer(sumBlocksKernel, "gridSumsBuffer", swap ? gridSumsBuffer2 : gridSumsBuffer);
            gridShader.SetInt("d", d);
            gridShader.Dispatch(sumBlocksKernel, Mathf.CeilToInt(blocks / blockSize), 1, 1);
            swap = !swap;
        }

        // Apply offsets of sums to each block
        gridShader.SetBuffer(addSumsKernel, "gridSumsBufferIn", swap ? gridSumsBuffer : gridSumsBuffer2);
        gridShader.Dispatch(addSumsKernel, blocks, 1, 1);


        if (_swapPlayerBuffer)
        {
            gridShader.SetBuffer(rearrangeBoidsKernel, "boidPlayersIn", _boidPlayersBufferOut);
            gridShader.SetBuffer(rearrangeBoidsKernel, "boidPlayersOut", _boidPlayersBuffer);
            boidShader.SetBuffer(updateBoidsKernel, "boidPlayers", _boidPlayersBuffer);
        }
        else
        {
            gridShader.SetBuffer(rearrangeBoidsKernel, "boidPlayersIn", _boidPlayersBuffer);
            gridShader.SetBuffer(rearrangeBoidsKernel, "boidPlayersOut", _boidPlayersBufferOut);
            boidShader.SetBuffer(updateBoidsKernel, "boidPlayers", _boidPlayersBufferOut);
        }

        _swapPlayerBuffer = !_swapPlayerBuffer;

        // Rearrange boids
        gridShader.Dispatch(rearrangeBoidsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);

        // Compute boid behaviours
        boidShader.Dispatch(updateBoidsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);


        boidShader.SetInt("numBoidsPrevious", numBoids);
        // Grid shader needs to be one iteration behind, for correct rearranging.
        gridShader.SetInt("numBoids", horde.AliveRats);

        // Actually draw the boids
        Graphics.RenderPrimitives(rp, MeshTopology.Quads, numBoids * 4);
    }

    private void OnDestroy()
    {
        if (boids.IsCreated)
        {
            boids.Dispose();
            boidsTemp.Dispose();
        }

        boidBuffer.Release();
        boidBufferOut.Release();
        gridBuffer.Release();
        gridOffsetBuffer.Release();
        gridOffsetBufferIn.Release();
        gridSumsBuffer.Release();
        gridSumsBuffer2.Release();
        trianglePositions.Release();
    }

    private Vector2[] getTriangleVerts()
    {
        return new[]
        {
            new Vector2(-0.5f, -0.5f),
            new Vector2(-0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, -0.5f)
        };
    }

    /// <summary>
    ///     Get grid coordinates of a world point
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    private int2 getGridLocation(Vector2 pos)
    {
        var x = Mathf.FloorToInt(pos.x / gridCellSize + gridDimX / 2);
        var y = Mathf.FloorToInt(pos.y / gridCellSize + gridDimY / 2);
        return new int2(x, y);
    }

    /// Get Grid Index from 2D grid coordinate
    private int getGridID(int2 pos)
    {
        return gridDimX * pos.y + pos.x;
    }


    /// <summary>
    ///     Check whether a given position is within the horde.
    /// </summary>
    /// <param name="pos">Position to check</param>
    /// <param name="range">Range which pos must be within from a boid center to be in the horde</param>
    /// <returns></returns>
    public bool PosInHorde(Vector2 pos, float range)
    {
        var rangeSq = range * range;

        var boids = new Boid[2];

        // Boids are sorted in position order from bottom left to top right
        boidBufferOut.GetData(boids, 0, 0, 1);
        boidBufferOut.GetData(boids, 1, previousNumBoids - 1, 1);
        Vector2 bottomLeft = boids[0].pos;
        Vector2 topRight = boids[1].pos;

        // Early return if pos outside bounding box
        if (pos.x < bottomLeft.x - range) return false;
        if (pos.x > topRight.x + range) return false;
        if (pos.y < bottomLeft.y - range) return false;
        if (pos.y > topRight.y + range) return false;

        var gridXY = getGridLocation(pos);
        var gridID = getGridID(gridXY);

        // Get grid offset of this cell, and next cell
        var gridOffsets = new int[2];
        gridOffsetBuffer.GetData(gridOffsets, 0, gridID - 1, 2);

        // If grid offsets are identical then there are no boids in the grid cell where we clicked
        if (gridOffsets[0] == gridOffsets[1]) return false;

        boids = new Boid[gridOffsets[1] - gridOffsets[0]];
        boidBufferOut.GetData(boids, 0, gridOffsets[0], gridOffsets[1] - gridOffsets[0]);

        return boids.Any(boid => (new Vector2(boid.pos.x, boid.pos.y) - pos).sqrMagnitude < rangeSq);
    }


    /// <summary>
    ///     EXPENSIVE, should only be used by HordeController to update bounds each frame, otherwise you should get the bounds
    ///     from the HordeController.
    /// </summary>
    /// <returns>Bounds encapsulating all rats in the horde</returns>
    public Bounds GetBounds()
    {
        var offsets = new int[gridDimX * gridDimY];
        gridOffsetBuffer.GetData(offsets, 0, 0, gridDimX * gridDimY);

        var bottomLeft = Vector2.positiveInfinity;

        // Find first non-empty row from bottom
        for (var y = 0; y < gridDimY; y++)
        {
            var rowBoids = offsets[(y + 1) * gridDimX - 1] - offsets[y * gridDimX];
            if (rowBoids == 0) continue;

            var boids = new Boid[rowBoids];
            boidBufferOut.GetData(boids, 0, offsets[y * gridDimX], rowBoids);

            bottomLeft.y = boids.Minima(boid => boid.pos.y).First().pos.y;
            break;
        }

        // Find first non-empty column from left
        for (var x = 0; x < gridDimX; x++)
        {
            var empty = true;

            for (var y = 0; y < gridDimY; y++)
            {
                var cellBoids = offsets[y * gridDimX + x] - (y == 0 ? 0 : offsets[y * gridDimX + x - 1]);
                if (cellBoids == 0) continue;

                empty = false;

                var boids = new Boid[cellBoids];
                boidBufferOut.GetData(boids, 0, y == 0 ? 0 : offsets[y * gridDimX + x - 1], cellBoids);

                bottomLeft.x = Mathf.Min(bottomLeft.x, boids.Minima(boid => boid.pos.x).First().pos.x);
            }

            // Break once we've processed the first non-empty row
            if (!empty) break;
        }

        var topRight = Vector2.negativeInfinity;

        // Find first non-empty row from top
        for (var y = gridDimY - 1; y >= 0; y--)
        {
            var rowBoids = offsets[(y + 1) * gridDimX - 1] - offsets[y * gridDimX];
            if (rowBoids == 0) continue;

            var boids = new Boid[rowBoids];
            boidBufferOut.GetData(boids, 0, offsets[y * gridDimX], rowBoids);

            topRight.y = boids.Maxima(boid => boid.pos.y).First().pos.y;
            break;
        }

        // Find first non-empty column from right
        for (var x = gridDimX - 1; x >= 0; x--)
        {
            var empty = true;

            for (var y = 0; y < gridDimY; y++)
            {
                var cellBoids = offsets[y * gridDimX + x] - (y == 0 ? 0 : offsets[y * gridDimX + x - 1]);
                if (cellBoids == 0) continue;

                empty = false;

                var boids = new Boid[cellBoids];
                boidBufferOut.GetData(boids, 0, y == 0 ? 0 : offsets[y * gridDimX + x - 1], cellBoids);

                topRight.x = Mathf.Max(topRight.x, boids.Maxima(boid => boid.pos.x).First().pos.x);
            }

            // Break once we've processed the first non-empty row
            if (!empty) break;
        }

        var center = bottomLeft + (topRight - bottomLeft) / 2.0f;
        var size = topRight - bottomLeft;

        return new Bounds(center, size);
    }
}