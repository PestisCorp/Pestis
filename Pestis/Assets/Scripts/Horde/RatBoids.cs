using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Horde;
using MoreLinq.Extensions;
using Unity.Mathematics;
using UnityEngine;

internal struct Boid
{
    public float2 pos;
    public float2 vel;
    public int player;

    public int horde;

    // 0 for dead, 1 for alive
    public byte dead;
}

public class RatBoids : MonoBehaviour
{
    private const float blockSize = 512f;

    [Header("Settings")] [SerializeField] private float maxSpeed = 2;

    [SerializeField] private float edgeMargin = .5f;
    [SerializeField] private float visualRange = 5.0f;
    [SerializeField] private float minDistance = 0.4f;
    [SerializeField] private float cohesionFactor = 3;
    [SerializeField] private float separationFactor = 100;
    [SerializeField] private float alignmentFactor = 0.1f;
    [SerializeField] private float targetFactor = 50;

    [SerializeField] private ComputeShader boidShader;
    [SerializeField] private ComputeShader gridShader;
    [SerializeField] private Material boidMat;
    [SerializeField] private Material deadBoidMat;


    /// <summary>
    ///     Set by HordeController, pulled from when sim updates. Inside this script you should only read from `numBoids`
    /// </summary>
    public int AliveRats;

    public Vector2 TargetPos;

    public bool paused;

    public List<HordeController> containedHordes;

    private bool _started;
    private int blocks;

    private ComputeBuffer boidBuffer;
    private ComputeBuffer boidBufferOut;
    private ComputeBuffer deadBoids;
    private int deadBoidsCount;

    // Index is boid ID, x value is position flattened to 1D array, y value is grid cell offset
    private ComputeBuffer gridBuffer;
    private float gridCellSize;
    private int gridDimY, gridDimX, gridTotalCells;
    private ComputeBuffer gridOffsetBuffer;
    private ComputeBuffer gridOffsetBufferIn;
    private ComputeBuffer gridSumsBuffer;
    private ComputeBuffer gridSumsBuffer2;

    private float minSpeed;

    [Header("Performance")] private int numBoids;
    private int previousNumBoids;
    private RenderParams rp;
    private RenderParams rpDead;
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
    public void Start()
    {
        if (_started) return;
        _started = true;
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

        // Setup compute buffer
        boidBuffer = new ComputeBuffer(128, Marshal.SizeOf(typeof(Boid)));
        boidBufferOut = new ComputeBuffer(128, Marshal.SizeOf(typeof(Boid)));
        deadBoids = new ComputeBuffer(128, Marshal.SizeOf(typeof(Boid)), ComputeBufferType.Append);

        boidShader.SetBuffer(updateBoidsKernel, "boidsIn", boidBufferOut);
        boidShader.SetBuffer(updateBoidsKernel, "boidsOut", boidBuffer);
        boidShader.SetBuffer(updateBoidsKernel, "deadBoids", deadBoids);

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

        // Set render params
        rp = new RenderParams(new Material(boidMat));
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("boids", boidBuffer);
        rp.worldBounds = new Bounds(Vector3.zero, Vector3.one * 3000);
        trianglePositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, 8);
        trianglePositions.SetData(triangleVerts);
        rp.matProps.SetBuffer("_Positions", trianglePositions);

        rpDead = new RenderParams(new Material(deadBoidMat));
        rpDead.matProps = new MaterialPropertyBlock();
        rpDead.matProps.SetBuffer("boids", deadBoids);
        rpDead.worldBounds = new Bounds(Vector3.zero, Vector3.one * 3000);
        rpDead.matProps.SetBuffer("_Positions", trianglePositions);

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
        gridShader.SetBuffer(updateGridKernel, "deadBoids", deadBoids);
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

        var horde = gameObject.GetComponentInParent<HordeController>();
        if (horde)
        {
            boidShader.SetInt("player", unchecked((int)horde.Player.Id.Object.Raw));
            boidShader.SetInt("horde", unchecked((int)horde.Id.Object.Raw));
        }
        else
        {
            boidShader.SetInt("player", -1);
            boidShader.SetInt("horde", -1);
        }
    }

    // Update is called once per frame
    private void Update()
    {
        if (AliveRats == 0 || paused) return;

        previousNumBoids = numBoids;
        var newNumBoids = AliveRats;

        // Some boids have died
        if (newNumBoids < numBoids)
        {
            deadBoidsCount += numBoids - newNumBoids;

            var deadboids = new Boid[deadBoidsCount];
            deadBoids.GetData(deadboids, 0, 0, deadBoidsCount);

            var boids = new Boid[numBoids + deadBoidsCount];
            boidBuffer.GetData(boids, 0, 0, numBoids + deadBoidsCount);

            Debug.Log("Got dead boids");
        }
        else
        {
            var deadboids = new Boid[deadBoidsCount];
            deadBoids.GetData(deadboids, 0, 0, deadBoidsCount);

            var boids = new Boid[numBoids + deadBoidsCount];
            boidBuffer.GetData(boids, 0, 0, numBoids + deadBoidsCount);

            Debug.Log("Got dead boids");
        }

        if (boidBuffer.count < newNumBoids) ResizeBuffers(newNumBoids * 2);

        // Increase separation force the bigger the horde is.
        boidShader.SetFloat("separationFactor", separationFactor * (numBoids / 1000.0f));

        boidShader.SetFloat("deltaTime", Time.deltaTime);
        boidShader.SetFloats("targetPos", TargetPos.x,
            TargetPos.y);

        boidShader.SetInt("numBoids", newNumBoids);
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

        // Rearrange boids
        gridShader.Dispatch(rearrangeBoidsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);

        // Compute boid behaviours
        boidShader.Dispatch(updateBoidsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);

        boidShader.SetInt("numBoidsPrevious", previousNumBoids);
        // Grid shader needs to be one iteration behind, for correct rearranging.
        gridShader.SetInt("numBoids", numBoids);
        gridShader.SetInt("numBoidsPrevious", previousNumBoids);


        // Actually draw the boids
        Graphics.RenderPrimitives(rp, MeshTopology.Quads, numBoids * 4);
        Graphics.RenderPrimitives(rpDead, MeshTopology.Quads, deadBoidsCount * 4);
    }

    private void OnDestroy()
    {
        boidBuffer.Release();
        boidBufferOut.Release();
        gridBuffer.Release();
        gridOffsetBuffer.Release();
        gridOffsetBufferIn.Release();
        gridSumsBuffer.Release();
        gridSumsBuffer2.Release();
        trianglePositions.Release();
    }

    private void ResizeBuffers(int newSize)
    {
        if (newSize < boidBuffer.count) throw new Exception("Tried to shrink buffers!");
        Debug.Log($"Resizing boid buffers to {newSize}");
        var newBuffer = new ComputeBuffer(newSize, Marshal.SizeOf(typeof(Boid)));
        var boids = new Boid[numBoids];
        boidBuffer.GetData(boids, 0, 0, numBoids);
        newBuffer.SetData(boids);
        boidBuffer.Release();
        boidBuffer = newBuffer;

        newBuffer = new ComputeBuffer(newSize, Marshal.SizeOf(typeof(Boid)));
        boidBufferOut.GetData(boids, 0, 0, numBoids);
        newBuffer.SetData(boids);
        boidBufferOut.Release();
        boidBufferOut = newBuffer;

        newBuffer = new ComputeBuffer(newSize, Marshal.SizeOf(typeof(Boid)), ComputeBufferType.Append);
        deadBoids.GetData(boids, 0, 0, deadBoidsCount);
        newBuffer.SetData(boids);
        deadBoids.Release();
        deadBoids = newBuffer;

        // Resize grid buffer
        var grid = new uint2[numBoids];
        newBuffer = new ComputeBuffer(newSize, 8);
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
        if (numBoids == 0) return new Bounds();

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

    /// <summary>
    ///     Add new boids to myself (I am a combat boids controller)
    /// </summary>
    /// <param name="newBoidsBuffer">The compute buffer containing the boids to add</param>
    /// <param name="newBoidsCount">How many boids to add from the compute buffer</param>
    public void AddBoids(ComputeBuffer newBoidsBuffer, int newBoidsCount, HordeController boidsHorde)
    {
        Debug.Log("COMBAT BOIDS: Adding boids");
        // Resize buffers if too small
        if (numBoids + newBoidsCount > boidBuffer.count) ResizeBuffers((numBoids + newBoidsCount) * 2);

        // Load boids into memory
        var newBoids = new Boid[newBoidsCount];
        newBoidsBuffer.GetData(newBoids, 0, 0, newBoidsCount);

        // Send boids to buffer
        boidBuffer.SetData(newBoids, 0, numBoids, newBoidsCount);
        boidBufferOut.SetData(newBoids, 0, numBoids, newBoidsCount);
        AliveRats += newBoidsCount;
        numBoids += newBoidsCount;
        // So it doesn't override their current positions/other data
        boidShader.SetInt("numBoidsPrevious", numBoids);

        containedHordes.Add(boidsHorde);
    }

    /// <summary>
    ///     Called by the horde that wants its boids back from the combat boids.
    /// </summary>
    /// <param name="hordeBuffer">Buffer on the normal boids to put the combat boids into</param>
    /// <param name="hordeBufferOut">BufferOut on the normal boids to put the combat boids into</param>
    /// <param name="horde">The horde that is wanting its boids back</param>
    public void RemoveBoids(ComputeBuffer hordeBuffer, ComputeBuffer hordeBufferOut, HordeController horde)
    {
        var boids = new Boid[numBoids];
        var combatBoids = new List<Boid>();
        var hordeBoids = new List<Boid>();

        var hordeID = unchecked((int)horde.Object.Id.Raw);
        foreach (var boid in boids)
            if (boid.horde == hordeID)
                hordeBoids.Add(boid);
            else
                combatBoids.Add(boid);

        var hordeBoidsArr = hordeBoids.ToArray();
        hordeBuffer.SetData(hordeBoids, 0, 0, hordeBoidsArr.Length);
        hordeBufferOut.SetData(hordeBoids, 0, 0, hordeBoidsArr.Length);
        var combatBoidsArr = combatBoids.ToArray();
        boidBuffer.SetData(combatBoids, 0, 0, combatBoidsArr.Length);
        boidBufferOut.SetData(combatBoids, 0, 0, combatBoidsArr.Length);
    }

    /// <summary>
    ///     Get my boids back from a combat controller.
    /// </summary>
    /// <param name="combat">Combat controller that is currently controlling my boids</param>
    /// <param name="myHorde">The horde that owns me</param>
    public void GetBoidsBack(CombatController combat, HordeController myHorde)
    {
        combat.boids.RemoveBoids(boidBuffer, boidBufferOut, myHorde);
        paused = false;
    }

    /// <summary>
    ///     Transfer control of my boids over to some combat boids controller.
    /// </summary>
    /// <param name="combatBoids">The combat boid controller</param>
    /// <param name="myHorde">The horde which owns me</param>
    public void JoinCombat(RatBoids combatBoids, HordeController myHorde)
    {
        Debug.Log("BOIDS: Joining to combat");
        paused = true;
        combatBoids.AddBoids(boidBufferOut, numBoids, myHorde);
        combatBoids.TargetPos = TargetPos;
    }
}