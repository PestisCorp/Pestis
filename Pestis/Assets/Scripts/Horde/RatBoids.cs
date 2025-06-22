using System;
using System.Linq;
using System.Runtime.InteropServices;
using Combat;
using Horde;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

public struct Boid
{
    public float2 pos;
    public float2 vel;
    public int player;

    public int horde;

    /// <summary>
    ///     0 for alive, 1 for dead
    /// </summary>
    public int dead;
}

public struct BoidPoi
{
    public float2 Pos;
    public float RadiusSq;

    public BoidPoi(float2 pos, float radiusSq)
    {
        Pos = pos;
        RadiusSq = radiusSq;
    }
}

public class RatBoids : MonoBehaviour
{
    private const float blockSize = 512f;

    /// <summary>
    ///     How many boids to account for in initial memory allocations
    /// </summary>
    private static readonly int INITIAL_BOID_MEMORY_ALLOCATION = 2048;

    private static readonly int RatLeft = Shader.PropertyToID("_RatLeft");
    private static readonly int RatUp = Shader.PropertyToID("_RatUp");
    private static readonly int RatUpLeft = Shader.PropertyToID("_RatUpLeft");
    private static readonly int RatDownLeft = Shader.PropertyToID("_RatDownLeft");
    private static readonly int RatDown = Shader.PropertyToID("_RatDown");
    private static readonly int RatRight = Shader.PropertyToID("_RatRight");
    private static readonly int RatUpRight = Shader.PropertyToID("_RatUpRight");
    private static readonly int RatDownRight = Shader.PropertyToID("_RatDownRight");
    private static readonly int ReceivedBoidsThisFrame = Shader.PropertyToID("receivedBoidsThisFrame");

    [Header("Settings")] [SerializeField] private float maxSpeed = 2;

    [SerializeField] private float edgeMargin = .5f;
    [SerializeField] private float visualRange = 50.0f;
    [SerializeField] private float minDistance = 0.4f;
    [SerializeField] private float cohesionFactor = 3;
    [SerializeField] private float separationFactor = 100;
    [SerializeField] private float alignmentFactor = 0.1f;
    [SerializeField] private float targetFactor = 50;

    [SerializeField] private ComputeShader boidShader;
    [SerializeField] private ComputeShader gridShader;
    [SerializeField] private Material boidMat;
    [SerializeField] private Material deadBoidMat;

    public bool combat;

    /// <summary>
    ///     Set by HordeController, pulled from when sim updates. Inside this script you should only read from `numBoids`
    /// </summary>
    public int AliveRats;

    public Vector2 TargetPos;

    public bool paused;

    public int numBoids;

    /// <summary>
    ///     If we set the boid buffer this frame
    /// </summary>
    private int _boidAddCooldown;

    private float[] _boundsArr;
    private ComputeBuffer _boundsBuffer;
    private ComputeBuffer _gridBoundsBuffer;

    private bool _started;

    private float _timeSinceVelUpdate;

    private bool _waitingForBounds;
    private int blocks;

    private ComputeBuffer boidBuffer;
    private ComputeBuffer boidBufferOut;

    private int corpseKernel;
    private ComputeBuffer deadBoids;
    private int deadBoidsCount;

    private ComputeBuffer deadBoidsCountBuffer;

    // Index is boid ID, x value is position flattened to 1D array, y value is grid cell offset
    private ComputeBuffer gridBuffer;
    private float gridCellSize;
    private int gridDimY, gridDimX, gridTotalCells;
    private ComputeBuffer gridOffsetBuffer;
    private ComputeBuffer gridOffsetBufferIn;
    private ComputeBuffer gridSumsBuffer;
    private ComputeBuffer gridSumsBuffer2;

    private HordeController hordeController;

    private float minSpeed;

    private int previousNumBoids;
    private RenderParams rp;
    private RenderParams rpDead;

    /// <summary>
    ///     Used for storing boids when receiving boids from GPU without allocating memory
    /// </summary>
    private Boid[] tempBoidsArr;

    private GraphicsBuffer trianglePositions;
    private Vector2[] triangleVerts;
    private float turnSpeed;

    private int updateBoidsKernel,
        generateBoidsKernel,
        updateBoundsKernel,
        updatePositionsKernel,
        updateGridBoundsKernel;

    private int updateGridKernel,
        clearGridKernel,
        prefixSumKernel,
        sumBlocksKernel,
        addSumsKernel,
        rearrangeBoidsKernel;

    private float xBound, yBound;

    // If horde controller is null then the game is running in Benchmark mode, so we should simulate these boids!
    public bool Local => hordeController.IsUnityNull() || hordeController.HasStateAuthority;

    public Bounds? Bounds { private set; get; }

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
        hordeController = GetComponentInParent<HordeController>();
        _started = true;
        xBound = 256;
        yBound = 256;
        turnSpeed = 1.5f;
        minSpeed = maxSpeed * 0.2f;

        // Create new instance of shaders to stop them sharing data!
        boidShader = Instantiate(boidShader);
        gridShader = Instantiate(gridShader);

        // Get kernel IDs
        updateBoidsKernel = boidShader.FindKernel("UpdateBoidsVelocity");
        updatePositionsKernel = boidShader.FindKernel("UpdateBoidsPositions");
        updateBoundsKernel = boidShader.FindKernel("UpdateBounds");
        updateGridBoundsKernel = boidShader.FindKernel("UpdateGridBounds");
        updateGridKernel = gridShader.FindKernel("UpdateGrid");
        clearGridKernel = gridShader.FindKernel("ClearGrid");
        prefixSumKernel = gridShader.FindKernel("PrefixSum");
        sumBlocksKernel = gridShader.FindKernel("SumBlocks");
        addSumsKernel = gridShader.FindKernel("AddSums");
        rearrangeBoidsKernel = gridShader.FindKernel("RearrangeBoids");

        // Setup compute buffer
        boidBuffer = new ComputeBuffer(INITIAL_BOID_MEMORY_ALLOCATION, Marshal.SizeOf(typeof(Boid)));
        boidBufferOut = new ComputeBuffer(INITIAL_BOID_MEMORY_ALLOCATION, Marshal.SizeOf(typeof(Boid)));
        deadBoids = new ComputeBuffer(INITIAL_BOID_MEMORY_ALLOCATION, Marshal.SizeOf(typeof(Boid)));

        Assert.AreEqual(Marshal.SizeOf(typeof(float)), Marshal.SizeOf(typeof(uint)),
            "uint and float have different byte sizes, bounds calc WILL break");
        _boundsBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(float)));
        _gridBoundsBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(uint)));
        _boundsArr = new float[4];

        _boundsArr[0] = 1024.0f;
        _boundsArr[1] = -1024.0f;
        _boundsArr[2] = -1024.0f;
        _boundsArr[3] = 1024.0f;
        _boundsBuffer.SetData(_boundsArr, 0, 0, 4);

        boidShader.SetBuffer(updateBoundsKernel, "bounds", _boundsBuffer);
        boidShader.SetBuffer(updateBoundsKernel, "boidsIn", boidBuffer);

        deadBoidsCountBuffer = new ComputeBuffer(1, sizeof(uint));
        var counter = new uint[1];
        counter[0] = 0;
        deadBoidsCountBuffer.SetData(counter, 0, 0, 1);
        gridShader.SetBuffer(updateGridKernel, "deadBoidsCount", deadBoidsCountBuffer);

        boidShader.SetInt("numBoids", numBoids);
        boidShader.SetBool("combatRats", combat);
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

        if (GameManager.Instance)
        {
            boidShader.SetBuffer(updateBoidsKernel, "pois", GameManager.Instance.poiBuffer);
            boidShader.SetBuffer(updateBoidsKernel, "poiOffsets", GameManager.Instance.poiOffsetBuffer);
            boidShader.SetFloat("poiGridCellSize", GameManager.Instance.poiGridCellSize);
            boidShader.SetInt("poiGridDimX", GameManager.Instance.poiGridDimX);
            boidShader.SetInt("poiGridDimY", GameManager.Instance.poiGridDimY);
        }
        else
        {
            boidShader.SetBuffer(updateBoidsKernel, "pois", new ComputeBuffer(1, sizeof(uint)));
            boidShader.SetBuffer(updateBoidsKernel, "poiOffsets", new ComputeBuffer(1, sizeof(uint)));
            boidShader.SetFloat("poiGridCellSize", 5);
            boidShader.SetInt("poiGridDimX", 128);
            boidShader.SetInt("poiGridDimY", 128);
        }


        // Set render params
        rp = new RenderParams(new Material(boidMat));
        rp.matProps = new MaterialPropertyBlock();
        rp.worldBounds = new Bounds(Vector3.zero, Vector3.one * 3000);
        trianglePositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, 8);
        trianglePositions.SetData(triangleVerts);

        rpDead = new RenderParams(new Material(deadBoidMat));
        rpDead.matProps = new MaterialPropertyBlock();
        rpDead.worldBounds = new Bounds(Vector3.zero, Vector3.one * 3000);

        // Spatial grid setup
        gridCellSize = visualRange;
        gridDimX = Mathf.FloorToInt(xBound * 2 / gridCellSize) + 31;
        gridDimY = Mathf.FloorToInt(yBound * 2 / gridCellSize) + 31;
        gridTotalCells = gridDimX * gridDimY;


        gridBuffer = new ComputeBuffer(INITIAL_BOID_MEMORY_ALLOCATION, 8);
        gridOffsetBuffer = new ComputeBuffer(gridTotalCells, 4);
        gridOffsetBufferIn = new ComputeBuffer(gridTotalCells, 4);
        blocks = Mathf.CeilToInt(gridTotalCells / blockSize);
        gridSumsBuffer = new ComputeBuffer(blocks, 4);
        gridSumsBuffer2 = new ComputeBuffer(blocks, 4);
        gridShader.SetInt("numBoids", numBoids);
        gridShader.SetInt("numBoidsPrevious", 0);


        boidShader.SetBuffer(updateGridBoundsKernel, "gridBounds", _gridBoundsBuffer);
        boidShader.SetBuffer(updateBoundsKernel, "gridBounds", _gridBoundsBuffer);
        boidShader.SetBuffer(updateGridBoundsKernel, "gridOffsetBuffer", gridOffsetBuffer);


        gridShader.SetFloat("gridCellSize", gridCellSize);
        gridShader.SetInt("gridDimY", gridDimY);
        gridShader.SetInt("gridDimX", gridDimX);
        gridShader.SetInt("gridTotalCells", gridTotalCells);
        gridShader.SetInt("blocks", blocks);

        boidShader.SetFloat("gridCellSize", gridCellSize);
        boidShader.SetInt("gridDimY", gridDimY);
        boidShader.SetInt("gridDimX", gridDimX);

        var horde = gameObject.GetComponentInParent<HordeController>();
        if (horde)
        {
            boidShader.SetInt("player", unchecked((int)horde.player.Id.Object.Raw));
            boidShader.SetInt("horde", unchecked((int)horde.Id.Object.Raw));
        }
        else
        {
            boidShader.SetInt("player", -1);
            boidShader.SetInt("horde", -1);
        }

        tempBoidsArr = new Boid[INITIAL_BOID_MEMORY_ALLOCATION];

        AttachBuffers();
    }

    // Update is called once per frame
    private void Update()
    {
        if (AliveRats == 0 || paused) return;

        _timeSinceVelUpdate += Time.deltaTime;

        boidShader.SetFloat("positionDeltaTime", Time.deltaTime);
        // If this boids sim is not selected for full update this frame due to performance reason, update only positions and not velocity and then return.
        // Check only performed if not in Benchmark mode.
        if (!hordeController.IsUnityNull() && hordeController.Id.Object.Raw % GameManager.Instance.recoverPerfLevel !=
            GameManager.Instance.currentPerfBucket && numBoids != 0)
        {
            // Clear indices
            gridShader.Dispatch(clearGridKernel, blocks, 1, 1);

            // Populate grid
            gridShader.Dispatch(updateGridKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);

            // Generate Offsets (Prefix Sum)
            // Offsets in each block
            gridShader.Dispatch(prefixSumKernel, blocks, 1, 1);

            // Offsets for sums of blocks
            var swapInner = false;
            for (var d = 1; d < blocks; d *= 2)
            {
                gridShader.SetBuffer(sumBlocksKernel, "gridSumsBufferIn", swapInner ? gridSumsBuffer : gridSumsBuffer2);
                gridShader.SetBuffer(sumBlocksKernel, "gridSumsBuffer", swapInner ? gridSumsBuffer2 : gridSumsBuffer);
                gridShader.SetInt("d", d);
                gridShader.Dispatch(sumBlocksKernel, Mathf.CeilToInt(blocks / blockSize), 1, 1);
                swapInner = !swapInner;
            }

            // Apply offsets of sums to each block
            gridShader.SetBuffer(addSumsKernel, "gridSumsBufferIn", swapInner ? gridSumsBuffer : gridSumsBuffer2);
            gridShader.Dispatch(addSumsKernel, blocks, 1, 1);

            // Rearrange boids
            gridShader.Dispatch(rearrangeBoidsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);

            boidShader.Dispatch(updatePositionsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);
            // Actually draw the boids
            Graphics.RenderPrimitives(rp, MeshTopology.Quads, numBoids * 4);
            Graphics.RenderPrimitives(rpDead, MeshTopology.Quads, deadBoidsCount * 4);
            return;
        }

        previousNumBoids = numBoids;
        var newNumBoids = AliveRats;

        // Some boids have died
        if (newNumBoids < numBoids && combat)
            // Don't exceed dead boids buffer
            deadBoidsCount = Math.Min(deadBoidsCount + numBoids - newNumBoids, deadBoids.count);

        if (boidBuffer.count < newNumBoids) ResizeBuffers(newNumBoids * 2);

        // Increase separation force the bigger the horde is.
        boidShader.SetFloat("separationFactor", separationFactor);

        boidShader.SetFloat("velocityDeltaTime", _timeSinceVelUpdate);
        _timeSinceVelUpdate = 0;
        // If I don't add something to it, the first time the shader accesses it in the shader it is NaN !?
        boidShader.SetFloats("targetPos", TargetPos.x, TargetPos.y);
        boidShader.SetFloat("targetPosX", TargetPos.x + 0.01f);
        boidShader.SetFloat("targetPosY", TargetPos.y + 0.01f);

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

        boidShader.SetInt("numBoidsPrevious", previousNumBoids);

        // 0,0 case is overriden in the shader to use an approximate center
        if (!Bounds.HasValue)
            boidShader.SetFloats("spawnPoint", 0, 0);
        else
            boidShader.SetFloats("spawnPoint", Bounds.Value.center.x + 0.01f, Bounds.Value.center.y + 0.01f);

        // Compute boid behaviours
        boidShader.Dispatch(updateBoidsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);

        // Grid shader needs to be one iteration behind, for correct rearranging.
        gridShader.SetInt("numBoids", numBoids);
        gridShader.SetInt("numBoidsPrevious", previousNumBoids);


        // Actually draw the boids
        Graphics.RenderPrimitives(rp, MeshTopology.Quads, numBoids * 4);
        Graphics.RenderPrimitives(rpDead, MeshTopology.Quads, deadBoidsCount * 4);

        switch (_boidAddCooldown)
        {
            case > 1:
                _boidAddCooldown--;
                break;
            case 1:
                _boidAddCooldown--;
                boidShader.SetBool(ReceivedBoidsThisFrame, false);
                break;
        }
    }

    private void FixedUpdate()
    {
        // If the horde isn't local, or there are performance issues, don't calculate bounds.
        if (!Local || (!hordeController.IsUnityNull() &&
                       hordeController.Id.Object.Raw % Math.Max(1, GameManager.Instance.recoverPerfLevel / 8) !=
                       GameManager.Instance.currentPerfBucket %
                       Math.Max(1, GameManager.Instance.recoverPerfLevel / 8))) return;

        if (numBoids == 0 || paused || !_started)
        {
            _boundsArr[0] = 1024.0f;
            _boundsArr[1] = -1024.0f;
            _boundsArr[2] = -1024.0f;
            _boundsArr[3] = 1024.0f;
            _boundsBuffer.SetData(_boundsArr, 0, 0, 4);
            return;
        }

        _boundsBuffer.GetData(_boundsArr, 0, 0, 4);

        if (_boundsArr[0] == 1024.0f)
        {
            Bounds = null;
            boidShader.Dispatch(updateGridBoundsKernel, 5, 5, 1);
            boidShader.Dispatch(updateBoundsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);
            return;
        }

        var size = new Vector2(_boundsArr[2] - _boundsArr[0], _boundsArr[1] - _boundsArr[3]);
        var center = new Vector2(size.x / 2.0f + _boundsArr[0], size.y / 2.0f + _boundsArr[3]);
        Bounds = new Bounds(center, size * 2);
        _boundsArr[0] = 1024.0f;
        _boundsArr[1] = -1024.0f;
        _boundsArr[2] = -1024.0f;
        _boundsArr[3] = 1024.0f;
        _boundsBuffer.SetData(_boundsArr, 0, 0, 4);
        boidShader.Dispatch(updateGridBoundsKernel, 5, 5, 1);
        boidShader.Dispatch(updateBoundsKernel, Mathf.CeilToInt(numBoids / blockSize), 1, 1);
    }

    private void OnDestroy()
    {
        _boundsBuffer.Release();
        boidBuffer.Release();
        boidBufferOut.Release();
        deadBoids.Release();
        deadBoidsCountBuffer.Release();
        gridBuffer.Release();
        gridOffsetBuffer.Release();
        gridOffsetBufferIn.Release();
        gridSumsBuffer.Release();
        gridSumsBuffer2.Release();
        trianglePositions.Release();
    }

    private void MarkBoidsChanged()
    {
        _boidAddCooldown = 5;
        boidShader.SetBool(ReceivedBoidsThisFrame, true);
    }

    private void AttachBuffers()
    {
        boidShader.SetBuffer(updateBoidsKernel, "boidsIn", boidBufferOut);
        boidShader.SetBuffer(updateBoidsKernel, "boidsOut", boidBuffer);
        boidShader.SetBuffer(updatePositionsKernel, "boidsIn", boidBufferOut);
        boidShader.SetBuffer(updatePositionsKernel, "boidsOut", boidBuffer);
        boidShader.SetBuffer(updateBoidsKernel, "deadBoids", deadBoids);
        rp.matProps.SetBuffer("boids", boidBuffer);
        rp.matProps.SetBuffer("_Positions", trianglePositions);
        rpDead.matProps.SetBuffer("boids", deadBoids);
        rpDead.matProps.SetBuffer("_Positions", trianglePositions);
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
        boidShader.SetBuffer(updateBoidsKernel, "gridOffsetBuffer", gridOffsetBuffer);
    }

    private void ResizeBuffers(int newSize)
    {
        if (newSize < boidBuffer.count) throw new Exception("Tried to shrink buffers!");
        Debug.Log($"Resizing boid buffers to {newSize}");

        tempBoidsArr = new Boid[newSize];

        var newBuffer = new ComputeBuffer(newSize, Marshal.SizeOf(typeof(Boid)));
        boidBuffer.GetData(tempBoidsArr, 0, 0, numBoids);
        newBuffer.SetData(tempBoidsArr);
        boidBuffer.Release();
        boidBuffer = newBuffer;

        newBuffer = new ComputeBuffer(newSize, Marshal.SizeOf(typeof(Boid)));
        boidBufferOut.GetData(tempBoidsArr, 0, 0, numBoids);
        newBuffer.SetData(tempBoidsArr);
        boidBufferOut.Release();
        boidBufferOut = newBuffer;

        newBuffer = new ComputeBuffer(newSize, Marshal.SizeOf(typeof(Boid)), ComputeBufferType.Append);
        deadBoids.GetData(tempBoidsArr, 0, 0, deadBoidsCount);
        newBuffer.SetData(tempBoidsArr);
        deadBoids.Release();
        deadBoids = newBuffer;

        // Resize grid buffer
        var grid = new uint2[numBoids];
        newBuffer = new ComputeBuffer(newSize, 8);
        gridBuffer.GetData(grid, 0, 0, numBoids);
        newBuffer.SetData(grid);
        gridBuffer.Release();
        gridBuffer = newBuffer;

        AttachBuffers();
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
    /// <param name="hordeBounds">The bounds of the horde these boids belong to</param>
    /// <returns></returns>
    public bool PosInHorde(Vector2 pos, float range, Bounds hordeBounds)
    {
        var rangeSq = range * range;

        // Expand bounds to account for slight mismatches between machine states
        var biggerBounds = new Bounds(hordeBounds.center, hordeBounds.extents);
        biggerBounds.Expand(5.0f);

        // Early return if outside bounds
        if (!biggerBounds.Contains(pos)) return false;

        var gridXY = getGridLocation(pos);
        var gridID = getGridID(gridXY);

        // Get grid offset of this cell, and next cell
        var gridOffsets = new uint[2];
        gridOffsetBuffer.GetData(gridOffsets, 0, gridID - 1, 2);

        // If grid offsets are identical then there are no boids in the grid cell where we clicked
        if (gridOffsets[0] == gridOffsets[1]) return false;

        var boids = new Boid[gridOffsets[1] - gridOffsets[0]];
        boidBufferOut.GetData(boids, 0, Convert.ToInt32(gridOffsets[0]),
            Convert.ToInt32(gridOffsets[1] - gridOffsets[0]));

        return boids.Any(boid => (new Vector2(boid.pos.x, boid.pos.y) - pos).sqrMagnitude < rangeSq);
    }


    /// <summary>
    ///     Get my boids back from a combat controller.
    /// </summary>
    /// <param name="combat">Combat controller that is currently controlling my boids</param>
    /// <param name="myHorde">The horde that owns me</param>
    public void GetBoidsBack(CombatController combat, HordeController myHorde)
    {
        combat.boids.RetrieveBoids(boidBuffer, boidBufferOut, deadBoids, deadBoidsCountBuffer, ref deadBoidsCount,
            myHorde);
        paused = false;
    }

    /// <summary>
    ///     Transfer control of my boids over to some combat boids controller.
    /// </summary>
    /// <param name="combatBoids">The combat boid controller</param>
    /// <param name="myHorde">The horde which owns me</param>
    public void JoinCombat(CombatBoids combatBoids, HordeController myHorde)
    {
        Debug.Log("BOIDS: Joining to combat");
        paused = true;
        // Delete old corpses
        uint[] count = { 0 };
        deadBoidsCountBuffer.SetData(count, 0, 0, 1);
        combatBoids.AddBoids(boidBufferOut, numBoids, myHorde);
        combatBoids.TargetPos = TargetPos;
    }

    /// <summary>
    ///     Need to flip the up-left, left, and down-left sprites to their right equivalents.
    /// </summary>
    /// <param name="original">The sprite to be flipped</param>
    /// <returns>The flipped original sprite</returns>
    private static Sprite FlipSprite(Sprite original)
    {
        var originalTex = original.texture;
        var flippedTex = new Texture2D(originalTex.width, originalTex.height, originalTex.format, false)
        {
            filterMode = originalTex.filterMode,
            wrapMode = originalTex.wrapMode
        };
        for (var y = 0; y < originalTex.height; y++)
        for (var x = 0; x < originalTex.width; x++)
            flippedTex.SetPixel(originalTex.width - x - 1, y, originalTex.GetPixel(x, y));


        flippedTex.Apply();

        flippedTex.anisoLevel = originalTex.anisoLevel;

        var flippedSprite = Sprite.Create(
            flippedTex,
            original.rect,
            new Vector2(0.5f, 0.5f),
            original.pixelsPerUnit
        );

        return flippedSprite;
    }

    /// <summary>
    ///     Set a new material for these boids
    /// </summary>
    public void SetBoidsMat()
    {
        var spriteID = Random.Range(0, 599);
        var upSprite = Resources.Load<Sprite>("Rats/Top/rat_Top_" + spriteID);
        var upLeftSprite = Resources.Load<Sprite>("Rats/UpLeft/rat_UpLeft_" + spriteID);
        var upRightSprite = FlipSprite(upLeftSprite);
        var leftSprite = Resources.Load<Sprite>("Rats/Left/rat_Left_" + spriteID);
        var rightSprite = FlipSprite(leftSprite);
        var downLeftSprite = Resources.Load<Sprite>("Rats/DownLeft/rat_DownLeft_" + spriteID);
        var downRightSprite = FlipSprite(downLeftSprite);
        var downSprite = Resources.Load<Sprite>("Rats/Down/rat_Down_" + spriteID);

        boidMat = new Material(boidMat);

        boidMat.SetTexture(RatUp, upSprite.texture);
        boidMat.SetTexture(RatUpLeft, upLeftSprite.texture);
        boidMat.SetTexture(RatLeft, leftSprite.texture);
        boidMat.SetTexture(RatDownLeft, downLeftSprite.texture);
        boidMat.SetTexture(RatDown, downSprite.texture);
        boidMat.SetTexture(RatRight, rightSprite.texture);
        boidMat.SetTexture(RatUpRight, upRightSprite.texture);
        boidMat.SetTexture(RatDownRight, downRightSprite.texture);
    }

    public Material GetMaterial()
    {
        return boidMat;
    }

    public Sprite GetSpriteFromMat()
    {
        var tex = boidMat.GetTexture(RatLeft) as Texture2D;
        return Sprite.Create(tex, new Rect(0, 0, tex!.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    ///     Set my internal boids to some specific boids, used for transferring boids from one to another when splitting horde.
    /// </summary>
    /// <param name="newBoids"></param>
    public void SetBoids(Boid[] newBoids)
    {
        if (newBoids.Length >= boidBuffer.count) ResizeBuffers(newBoids.Length * 2);
        boidBuffer.SetData(newBoids, 0, 0, newBoids.Length);
        boidBufferOut.SetData(newBoids, 0, 0, newBoids.Length);
        numBoids = newBoids.Length;
        previousNumBoids = newBoids.Length;
        MarkBoidsChanged();
    }

    /// <summary>
    ///     Send some of our boids over to another boids sim
    /// </summary>
    /// <param name="numBoidsFromAuthority">
    ///     The numBoidsCount on the State Authority when it called the RPC, to avoid race
    ///     conditions
    /// </param>
    /// <param name="boidsToMove">How many boids to send to the other sim</param>
    /// <param name="otherBoids">The other boid sim to send boids to</param>
    public void SplitBoids(int numBoidsFromAuthority, int boidsToMove, RatBoids otherBoids)
    {
        otherBoids.Start();
        var boids = new Boid[boidsToMove];
        boidBuffer.GetData(boids, 0, numBoidsFromAuthority - boidsToMove, boidsToMove);
        numBoids = numBoidsFromAuthority - boidsToMove;
        previousNumBoids = numBoidsFromAuthority - boidsToMove;
        otherBoids.SetBoids(boids);
        MarkBoidsChanged();
    }

    public void CreateApparition(RatBoids otherBoids, int boidsToClone)
    {
        otherBoids.Start();
        var boids = new Boid[boidsToClone];
        boidBuffer.GetData(boids, 0, 0, boidsToClone);
        otherBoids.SetBoids(boids);
    }

    public void TeleportHorde(Vector3 newHordeCenter, Bounds hordeBounds)
    {
        boidBuffer.GetData(tempBoidsArr, 0, 0, numBoids);
        for (var i = 0; i < numBoids; i++)
        {
            var offset = (Vector2)newHordeCenter - (Vector2)hordeBounds.center;
            tempBoidsArr[i].pos.x += offset.x;
            tempBoidsArr[i].pos.y += offset.y;
        }

        boidBuffer.SetData(tempBoidsArr, 0, 0, numBoids);
        boidBufferOut.SetData(tempBoidsArr, 0, 0, numBoids);
    }
}