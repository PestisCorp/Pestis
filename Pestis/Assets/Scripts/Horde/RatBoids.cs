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
    private readonly int burstLimit = 1 << 15;

    private readonly int cpuLimit = 1 << 12;
    private readonly int gpuLimit = 1 << 25;
    private readonly int jobLimit = 1 << 18;
    private int blocks;

    private ComputeBuffer boidBuffer;
    private ComputeBuffer boidBufferOut;

    private NativeArray<Boid> boids;
    private NativeArray<Boid> boidsTemp;

    // Index is particle ID, x value is position flattened to 1D array, y value is grid cell offset
    private NativeArray<int2> grid;
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

    [Header("Performance")] private int numBoids => horde.AliveRats;

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
        generateBoidsKernel = boidShader.FindKernel("GenerateBoids");
        updateGridKernel = gridShader.FindKernel("UpdateGrid");
        clearGridKernel = gridShader.FindKernel("ClearGrid");
        prefixSumKernel = gridShader.FindKernel("PrefixSum");
        sumBlocksKernel = gridShader.FindKernel("SumBlocks");
        addSumsKernel = gridShader.FindKernel("AddSums");
        rearrangeBoidsKernel = gridShader.FindKernel("RearrangeBoids");

        // Setup compute buffer
        boidBuffer = new ComputeBuffer(1024, 16);
        boidBufferOut = new ComputeBuffer(1024, 16);
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

        // Don't generate grid on CPU if over CPU limit
        if (numBoids <= jobLimit)
        {
            grid = new NativeArray<int2>(numBoids, Allocator.Persistent);
            gridOffsets = new NativeArray<int>(gridTotalCells, Allocator.Persistent);
        }

        gridBuffer = new ComputeBuffer(1024, 8);
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

        if (grid.IsCreated)
        {
            grid.Dispose();
            gridOffsets.Dispose();
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
}