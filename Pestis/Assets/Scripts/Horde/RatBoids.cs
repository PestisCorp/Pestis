using Horde;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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

    [Header("Performance")] [SerializeField]
    private int numBoids = 500;

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
    private readonly Modes mode = Modes.Gpu;
    private int blocks;

    private ComputeBuffer boidBuffer;
    private ComputeBuffer boidBufferOut;
    private BoidBehavioursJob boidJob;

    private NativeArray<Boid> boids;
    private NativeArray<Boid> boidsTemp;
    private ClearGridJob clearGridJob;
    private GenerateGridOffsetsJob generateGridOffsetsJob;

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
    private RearrangeBoidsJob rearrangeBoidsJob;
    private RenderParams rp;
    private GraphicsBuffer trianglePositions;
    private Vector2[] triangleVerts;
    private float turnSpeed;

    private int updateBoidsKernel, generateBoidsKernel;
    private UpdateGridJob updateGridJob;

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
        generateBoidsKernel = boidShader.FindKernel("GenerateBoids");
        updateGridKernel = gridShader.FindKernel("UpdateGrid");
        clearGridKernel = gridShader.FindKernel("ClearGrid");
        prefixSumKernel = gridShader.FindKernel("PrefixSum");
        sumBlocksKernel = gridShader.FindKernel("SumBlocks");
        addSumsKernel = gridShader.FindKernel("AddSums");
        rearrangeBoidsKernel = gridShader.FindKernel("RearrangeBoids");

        // Setup compute buffer
        boidBuffer = new ComputeBuffer(numBoids, 16);
        boidBufferOut = new ComputeBuffer(numBoids, 16);
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

        // Generate boids on GPU if over CPU limit
        if (numBoids <= jobLimit)
        {
            // Populate initial boids
            boids = new NativeArray<Boid>(numBoids, Allocator.Persistent);
            boidsTemp = new NativeArray<Boid>(numBoids, Allocator.Persistent);
            for (var i = 0; i < numBoids; i++)
            {
                var pos = new float2(Random.Range(-xBound, xBound), Random.Range(-yBound, yBound));
                var vel = new float2(Random.Range(-maxSpeed, maxSpeed), Random.Range(-maxSpeed, maxSpeed));
                var boid = new Boid();
                boid.pos = pos;
                boid.vel = vel;
                boids[i] = boid;
            }

            boidBuffer.SetData(boids);
        }
        else
        {
            boidShader.SetBuffer(generateBoidsKernel, "boidsOut", boidBuffer);
            boidShader.SetInt("randSeed", Random.Range(0, int.MaxValue));
            boidShader.Dispatch(generateBoidsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);
        }

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

        gridBuffer = new ComputeBuffer(numBoids, 8);
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

        // Job variables setup
        boidJob.gridCellSize = gridCellSize;
        boidJob.gridDimX = gridDimX;
        boidJob.gridDimY = gridDimY;
        boidJob.numBoids = numBoids;
        boidJob.visualRangeSq = visualRangeSq;
        boidJob.minDistanceSq = minDistanceSq;
        boidJob.xBound = xBound;
        boidJob.yBound = yBound;
        boidJob.cohesionFactor = cohesionFactor;
        boidJob.alignmentFactor = alignmentFactor;
        boidJob.separationFactor = separationFactor;
        boidJob.maxSpeed = maxSpeed;
        boidJob.minSpeed = minSpeed;
        boidJob.turnSpeed = turnSpeed;
        boidJob.inBoids = boidsTemp;
        boidJob.outBoids = boids;
        boidJob.gridOffsets = gridOffsets;

        clearGridJob.gridOffsets = gridOffsets;

        updateGridJob.numBoids = numBoids;
        updateGridJob.gridCellSize = gridCellSize;
        updateGridJob.gridDimY = gridDimY;
        updateGridJob.gridDimX = gridDimX;
        updateGridJob.boids = boids;
        updateGridJob.grid = grid;
        updateGridJob.gridOffsets = gridOffsets;

        generateGridOffsetsJob.gridTotalCells = gridTotalCells;
        generateGridOffsetsJob.gridOffsets = gridOffsets;

        rearrangeBoidsJob.numBoids = numBoids;
        rearrangeBoidsJob.grid = grid;
        rearrangeBoidsJob.gridOffsets = gridOffsets;
        rearrangeBoidsJob.inBoids = boids;
        rearrangeBoidsJob.outBoids = boidsTemp;
    }

    // Update is called once per frame
    private void Update()
    {
        if (mode == Modes.Gpu)
        {
            boidShader.SetFloat("deltaTime", Time.deltaTime);
            boidShader.SetFloats("targetPos", horde.targetLocation.transform.position.x,
                horde.targetLocation.transform.position.y);

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
        }
        else // CPU
        {
            // Using Burst or Jobs (multicore)
            if (mode == Modes.Burst || mode == Modes.Jobs)
            {
                // Clear grid counts/offsets
                clearGridJob.Run(gridTotalCells);

                // Update grid
                updateGridJob.Run();

                // Generate grid offsets
                generateGridOffsetsJob.Run();

                // Rearrange boids
                rearrangeBoidsJob.Run();

                // Update boids
                boidJob.deltaTime = Time.deltaTime;

                // Burst compiled (Single core)
                if (mode == Modes.Burst)
                {
                    boidJob.Run(numBoids);
                }
                // Burst Jobs (Multicore)
                else
                {
                    var boidJobHandle = boidJob.Schedule(numBoids, 32);
                    boidJobHandle.Complete();
                }
            }
            else // basic cpu
            {
                // Spatial grid
                ClearGrid();
                UpdateGrid();
                GenerateGridOffsets();
                RearrangeBoids();

                for (var i = 0; i < numBoids; i++)
                {
                    var boid = boidsTemp[i];
                    MergedBehaviours(ref boid);
                    LimitSpeed(ref boid);
                    KeepInBounds(ref boid);

                    // Update boid position
                    boid.pos += boid.vel * Time.deltaTime;
                    boids[i] = boid;
                }
            }

            // Send data to gpu buffer
            boidBuffer.SetData(boids);
        }

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

    private void MergedBehaviours(ref Boid boid)
    {
        var center = float2.zero;
        var close = float2.zero;
        var avgVel = float2.zero;
        var neighbours = 0;

        var gridXY = getGridLocation(boid);
        var gridCell = getGridIDbyLoc(gridXY);

        for (var y = gridCell - gridDimX; y <= gridCell + gridDimX; y += gridDimX)
        {
            var start = gridOffsets[y - 2];
            var end = gridOffsets[y + 1];
            for (var i = start; i < end; i++)
            {
                var other = boidsTemp[i];
                var diff = boid.pos - other.pos;
                var distanceSq = math.dot(diff, diff);
                if (distanceSq > 0 && distanceSq < visualRangeSq)
                {
                    if (distanceSq < minDistanceSq) close += diff / distanceSq;
                    center += other.pos;
                    avgVel += other.vel;
                    neighbours++;
                }
            }
        }

        if (neighbours > 0)
        {
            center /= neighbours;
            avgVel /= neighbours;

            boid.vel += (center - boid.pos) * (cohesionFactor * Time.deltaTime);
            boid.vel += (avgVel - boid.vel) * (alignmentFactor * Time.deltaTime);
        }

        boid.vel += close * (separationFactor * Time.deltaTime);
    }

    private void LimitSpeed(ref Boid boid)
    {
        var speed = math.length(boid.vel);
        var clampedSpeed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        boid.vel *= clampedSpeed / speed;
    }

    // Keep boids on screen
    private void KeepInBounds(ref Boid boid)
    {
        if (Mathf.Abs(boid.pos.x) > xBound) boid.vel.x -= Mathf.Sign(boid.pos.x) * Time.deltaTime * turnSpeed;
        if (Mathf.Abs(boid.pos.y) > yBound) boid.vel.y -= Mathf.Sign(boid.pos.y) * Time.deltaTime * turnSpeed;
    }

    private int getGridID(Boid boid)
    {
        var gridX = Mathf.FloorToInt(boid.pos.x / gridCellSize + gridDimX / 2);
        var gridY = Mathf.FloorToInt(boid.pos.y / gridCellSize + gridDimY / 2);
        return gridDimX * gridY + gridX;
    }

    private int getGridIDbyLoc(int2 cell)
    {
        return gridDimX * cell.y + cell.x;
    }

    private int2 getGridLocation(Boid boid)
    {
        var gridX = Mathf.FloorToInt(boid.pos.x / gridCellSize + gridDimX / 2);
        var gridY = Mathf.FloorToInt(boid.pos.y / gridCellSize + gridDimY / 2);
        return new int2(gridX, gridY);
    }

    private void ClearGrid()
    {
        for (var i = 0; i < gridTotalCells; i++) gridOffsets[i] = 0;
    }

    private void UpdateGrid()
    {
        for (var i = 0; i < numBoids; i++)
        {
            var id = getGridID(boids[i]);
            var boidGrid = grid[i];
            boidGrid.x = id;
            boidGrid.y = gridOffsets[id];
            grid[i] = boidGrid;
            gridOffsets[id]++;
        }
    }

    private void GenerateGridOffsets()
    {
        for (var i = 1; i < gridTotalCells; i++) gridOffsets[i] += gridOffsets[i - 1];
    }

    private void RearrangeBoids()
    {
        for (var i = 0; i < numBoids; i++)
        {
            var gridID = grid[i].x;
            var cellOffset = grid[i].y;
            var index = gridOffsets[gridID] - 1 - cellOffset;
            boidsTemp[index] = boids[i];
        }
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

    private enum Modes
    {
        Cpu,
        Burst,
        Jobs,
        Gpu
    }

    // Jobs
    [BurstCompile]
    private struct ClearGridJob : IJobParallelFor
    {
        public NativeArray<int> gridOffsets;

        public void Execute(int i)
        {
            gridOffsets[i] = 0;
        }
    }

    [BurstCompile]
    private struct UpdateGridJob : IJob
    {
        public NativeArray<int2> grid;
        public NativeArray<int> gridOffsets;
        [ReadOnly] public NativeArray<Boid> boids;
        public int numBoids;
        public float gridCellSize;
        public int gridDimY;
        public int gridDimX;

        private int jobGetGridID(Boid boid)
        {
            var gridX = Mathf.FloorToInt(boid.pos.x / gridCellSize + gridDimX / 2);
            var gridY = Mathf.FloorToInt(boid.pos.y / gridCellSize + gridDimY / 2);
            return gridDimX * gridY + gridX;
        }

        public void Execute()
        {
            for (var i = 0; i < numBoids; i++)
            {
                var id = jobGetGridID(boids[i]);
                var boidGrid = grid[i];
                boidGrid.x = id;
                boidGrid.y = gridOffsets[id];
                grid[i] = boidGrid;
                gridOffsets[id]++;
            }
        }
    }

    [BurstCompile]
    private struct GenerateGridOffsetsJob : IJob
    {
        public int gridTotalCells;
        public NativeArray<int> gridOffsets;

        public void Execute()
        {
            for (var i = 1; i < gridTotalCells; i++) gridOffsets[i] += gridOffsets[i - 1];
        }
    }

    [BurstCompile]
    private struct RearrangeBoidsJob : IJob
    {
        [ReadOnly] public NativeArray<int2> grid;
        [ReadOnly] public NativeArray<int> gridOffsets;
        [ReadOnly] public NativeArray<Boid> inBoids;
        public NativeArray<Boid> outBoids;
        public int numBoids;

        public void Execute()
        {
            for (var i = 0; i < numBoids; i++)
            {
                var gridID = grid[i].x;
                var cellOffset = grid[i].y;
                var index = gridOffsets[gridID] - 1 - cellOffset;
                outBoids[index] = inBoids[i];
            }
        }
    }

    [BurstCompile]
    private struct BoidBehavioursJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> gridOffsets;
        [ReadOnly] public NativeArray<Boid> inBoids;
        public NativeArray<Boid> outBoids;
        public float deltaTime;
        public int numBoids;
        public float visualRangeSq;
        public float minDistanceSq;
        public float cohesionFactor;
        public float alignmentFactor;
        public float separationFactor;
        public float maxSpeed;
        public float minSpeed;
        public float turnSpeed;
        public float xBound;
        public float yBound;
        public float gridCellSize;
        public int gridDimY;
        public int gridDimX;

        private void jobMergedBehaviours(ref Boid boid)
        {
            var center = float2.zero;
            var close = float2.zero;
            var avgVel = float2.zero;
            var neighbours = 0;

            var gridXY = jobGetGridLocation(boid);
            var gridCell = gridDimX * gridXY.y + gridXY.x;

            for (var y = gridCell - gridDimX; y <= gridCell + gridDimX; y += gridDimX)
            {
                var start = gridOffsets[y - 2];
                var end = gridOffsets[y + 1];
                for (var i = start; i < end; i++)
                {
                    var other = inBoids[i];
                    var diff = boid.pos - other.pos;
                    var distanceSq = math.dot(diff, diff);
                    if (distanceSq > 0 && distanceSq < visualRangeSq)
                    {
                        if (distanceSq < minDistanceSq) close += diff / distanceSq;
                        center += other.pos;
                        avgVel += other.vel;
                        neighbours++;
                    }
                }
            }

            if (neighbours > 0)
            {
                center /= neighbours;
                avgVel /= neighbours;

                boid.vel += (center - boid.pos) * (cohesionFactor * deltaTime);
                boid.vel += (avgVel - boid.vel) * (alignmentFactor * deltaTime);
            }

            boid.vel += close * (separationFactor * deltaTime);
        }

        private void jobLimitSpeed(ref Boid boid)
        {
            var speed = math.length(boid.vel);
            var clampedSpeed = Mathf.Clamp(speed, minSpeed, maxSpeed);
            boid.vel *= clampedSpeed / speed;
        }

        private void jobKeepInBounds(ref Boid boid)
        {
            if (Mathf.Abs(boid.pos.x) > xBound) boid.vel.x -= Mathf.Sign(boid.pos.x) * deltaTime * turnSpeed;
            if (Mathf.Abs(boid.pos.y) > yBound) boid.vel.y -= Mathf.Sign(boid.pos.y) * deltaTime * turnSpeed;
        }

        private int2 jobGetGridLocation(Boid boid)
        {
            var gridY = Mathf.FloorToInt(boid.pos.y / gridCellSize + gridDimY / 2);
            var gridX = Mathf.FloorToInt(boid.pos.x / gridCellSize + gridDimX / 2);
            return new int2(gridX, gridY);
        }

        public void Execute(int index)
        {
            var boid = inBoids[index];

            jobMergedBehaviours(ref boid);
            jobLimitSpeed(ref boid);
            jobKeepInBounds(ref boid);

            boid.pos += boid.vel * deltaTime;
            outBoids[index] = boid;
        }
    }
}