using Horde;
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
    private NativeArray<int> gridOffsets;
    private ComputeBuffer gridSumsBuffer;
    private ComputeBuffer gridSumsBuffer2;

    private HordeController horde;

    private float minSpeed;

    [Header("Performance")] private int numBoids = 5;
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
        boidBuffer = new ComputeBuffer(128, 16);
        boidBufferOut = new ComputeBuffer(128, 16);
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
        rp = new RenderParams(boidMat);
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
        var newNumBoids = horde.AliveRats;
        if (boidBuffer.count < newNumBoids)
        {
            Debug.Log("Resizing boid buffers");
            var newBuffer = new ComputeBuffer(newNumBoids * 2, 16);
            var boids = new Boid[numBoids];
            boidBuffer.GetData(boids, 0, 0, numBoids);
            newBuffer.SetData(boids);
            boidBuffer.Release();
            boidBuffer = newBuffer;

            newBuffer = new ComputeBuffer(newNumBoids * 2, 16);
            boidBufferOut.GetData(boids, 0, 0, numBoids);
            newBuffer.SetData(boids);
            boidBufferOut.Release();
            boidBufferOut = newBuffer;


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

        numBoids = newNumBoids;

        boidShader.SetFloat("deltaTime", Time.deltaTime);
        boidShader.SetFloats("targetPos", horde.targetLocation.transform.position.x,
            horde.targetLocation.transform.position.y);

        boidShader.SetInt("numBoids", horde.AliveRats);

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
    ///     Check whether a given position is within the horde.
    /// </summary>
    /// <param name="pos">Position to check</param>
    /// <param name="range">Range which pos must be within from a boid center to be in the horde</param>
    /// <returns></returns>
    public bool PosInHorde(Vector2 pos, float range)
    {
        var rangeSq = range * range;

        var boids = new Boid[2];

        // Boids are sorted in position order
        boidBuffer.GetData(boids, 0, 0, 1);
        boidBuffer.GetData(boids, 1, numBoids - 1, 1);
        Vector2 topLeft = boids[0].pos;
        Vector2 bottomRight = boids[1].pos;

        // Early return if pos outside bounding box
        if (pos.x < topLeft.x - range) return false;
        if (pos.x > bottomRight.x + range) return false;
        if (pos.y < bottomRight.y - range) return false;
        if (pos.y > topLeft.y + range) return false;

        var grid = new float2[gridDimX * gridDimY];
        gridOffsetBuffer.GetData(grid, 0, 0, gridDimX * gridDimY);

        boids = new Boid[numBoids];
        boidBuffer.GetData(boids, 0, 0, numBoids);

        return true;
    }

    public Bounds GetBounds()
    {
        var boids = new Boid[2];
        // Boids are sorted in position order
        boidBuffer.GetData(boids, 0, 0, 1);
        boidBuffer.GetData(boids, 1, numBoids - 1, 1);
        Vector2 topLeft = boids[0].pos;
        Vector2 bottomRight = boids[1].pos;

        var center = topLeft + (bottomRight - topLeft) / 2.0f;
        var size = bottomRight - topLeft;

        return new Bounds(center, size);
    }
}