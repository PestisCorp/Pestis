using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Fusion;
using Horde;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Combat
{
    public class CombatBoids : MonoBehaviour
    {
        private const float blockSize = 512f;
        private static readonly ProfilerMarker s_boundsMarker = new("RPCCombatBoids.GetBounds");
        private static readonly ProfilerMarker s_boundsHordeMarker = new("RPCCombatBoids.GetBoundsHorde");

        /// <summary>
        ///     How many boids to account for in initial memory allocations
        /// </summary>
        private static readonly int INITIAL_BOID_MEMORY_ALLOCATION = 2048;

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


        public Vector2 TargetPos;

        public bool paused;
        public bool local;

        public List<NetworkBehaviourId> containedHordes;

        public Bounds bounds;

        public readonly Dictionary<NetworkBehaviourId, Bounds> hordeBounds = new();

        // Horde controller -> local combat ID of horde
        private readonly Dictionary<NetworkBehaviourId, int> hordeIDs = new();
        private float[] _boundsArr;
        private ComputeBuffer _boundsBuffer;

        private bool _justAddedBoids;
        private Dictionary<NetworkBehaviourId, int> _previousNumBoids = new();


        private bool _started;
        private int blocks;

        private ComputeBuffer boidBuffer;
        private ComputeBuffer boidBufferOut;

        // One element per horde, element reflects how many boids of that horde to kill
        private ComputeBuffer boidsToKill;

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

        private float minSpeed;

        private Dictionary<NetworkBehaviourId, int> numBoids = new();
        private RenderParams rp;
        private RenderParams rpDead;
        public Dictionary<NetworkBehaviourId, int> totalDeathsPerHorde = new();
        private GraphicsBuffer trianglePositions;
        private Vector2[] triangleVerts;
        private float turnSpeed;

        private int updateBoidsKernel, generateBoidsKernel, updateBoundsKernel;

        private int updateGridKernel,
            clearGridKernel,
            prefixSumKernel,
            sumBlocksKernel,
            addSumsKernel,
            rearrangeBoidsKernel;

        private float xBound, yBound;

        public NetworkRunner Runner { set; private get; }

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
            xBound = 256;
            yBound = 256;
            turnSpeed = 0.8f;
            minSpeed = maxSpeed * 0.2f;

            // Create new instance of shaders to stop them sharing data!
            boidShader = Instantiate(boidShader);
            gridShader = Instantiate(gridShader);

            // Get kernel IDs
            updateBoidsKernel = boidShader.FindKernel("UpdateBoids");
            updateBoundsKernel = boidShader.FindKernel("UpdateBounds");
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

            deadBoidsCountBuffer = new ComputeBuffer(1, sizeof(uint));
            var counter = new uint[1];
            counter[0] = 0;
            deadBoidsCountBuffer.SetData(counter, 0, 0, 1);
            gridShader.SetBuffer(updateGridKernel, "deadBoidsCount", deadBoidsCountBuffer);

            boidsToKill = new ComputeBuffer(16, Marshal.SizeOf(typeof(int)));
            boidShader.SetBuffer(updateBoidsKernel, "boidsToKill", boidsToKill);

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

            boidShader.SetBuffer(updateBoidsKernel, "pois", GameManager.Instance.poiBuffer);
            boidShader.SetBuffer(updateBoidsKernel, "poiOffsets", GameManager.Instance.poiOffsetBuffer);
            boidShader.SetFloat("poiGridCellSize", GameManager.Instance.poiGridCellSize);
            boidShader.SetInt("poiGridDimX", GameManager.Instance.poiGridDimX);
            boidShader.SetInt("poiGridDimY", GameManager.Instance.poiGridDimY);

            _boundsArr = new float[CombatController.MaxParticipants * 4];
            _boundsBuffer = new ComputeBuffer(CombatController.MaxParticipants * 4, Marshal.SizeOf(typeof(uint)));

            for (var i = 0; i < CombatController.MaxParticipants; i++)
            {
                _boundsArr[i * 4 + 0] = 1024.0f;
                _boundsArr[i * 4 + 1] = -1024.0f;
                _boundsArr[i * 4 + 2] = -1024.0f;
                _boundsArr[i * 4 + 3] = 1024.0f;
            }

            _boundsBuffer.SetData(_boundsArr, 0, 0, CombatController.MaxParticipants * 4);
            boidShader.SetBuffer(updateBoundsKernel, "bounds", _boundsBuffer);
            boidShader.SetBuffer(updateBoundsKernel, "boidsIn", boidBuffer);

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
            gridDimX = Mathf.FloorToInt(xBound * 2 / gridCellSize) + 30;
            gridDimY = Mathf.FloorToInt(yBound * 2 / gridCellSize) + 30;
            gridTotalCells = gridDimX * gridDimY;


            gridBuffer = new ComputeBuffer(INITIAL_BOID_MEMORY_ALLOCATION, 8);
            gridOffsetBuffer = new ComputeBuffer(gridTotalCells, 4);
            gridOffsetBufferIn = new ComputeBuffer(gridTotalCells, 4);
            blocks = Mathf.CeilToInt(gridTotalCells / blockSize);
            gridSumsBuffer = new ComputeBuffer(blocks, 4);
            gridSumsBuffer2 = new ComputeBuffer(blocks, 4);

            gridShader.SetFloat("gridCellSize", gridCellSize);
            gridShader.SetInt("gridDimY", gridDimY);
            gridShader.SetInt("gridDimX", gridDimX);
            gridShader.SetInt("gridTotalCells", gridTotalCells);
            gridShader.SetInt("blocks", blocks);

            boidShader.SetFloat("gridCellSize", gridCellSize);
            boidShader.SetInt("gridDimY", gridDimY);
            boidShader.SetInt("gridDimX", gridDimX);

            boidShader.SetInt("player", -1);
            boidShader.SetInt("horde", -1);


            AttachBuffers();
            _started = true;
        }

        // Update is called once per frame
        private void Update()
        {
            if (numBoids.Values.Sum() == 0 || paused) return;

            _previousNumBoids = numBoids;

            var newNumBoids = containedHordes.ToDictionary(x => x, x =>
            {
                if (!Runner.TryFindBehaviour<HordeController>(x, out var horde))
                    throw new NullReferenceException("Couldn't find horde from combat");

                return (int)horde.AliveRats;
            });


            // Some boids have died
            if (newNumBoids.Values.Sum() < numBoids.Values.Sum())
                // Don't exceed dead boids buffer
                deadBoidsCount = Math.Min(deadBoidsCount + numBoids.Values.Sum() - newNumBoids.Values.Sum(),
                    deadBoids.count);

            if (boidBuffer.count < newNumBoids.Values.Sum()) ResizeBuffers(newNumBoids.Values.Sum() * 2);

            // Increase separation force the bigger the horde is.
            boidShader.SetFloat("separationFactor", separationFactor * (numBoids.Values.Sum() / 1000.0f));

            boidShader.SetFloat("deltaTime", Time.deltaTime);
            boidShader.SetFloats("targetPos", TargetPos.x,
                TargetPos.y);

            numBoids = newNumBoids;

            // Clear indices
            gridShader.Dispatch(clearGridKernel, blocks, 1, 1);

            gridShader.SetInt("numBoidsPrevious", _previousNumBoids.Values.Sum());
            // Grid shader needs to be one iteration behind, for correct rearranging.
            gridShader.SetInt("numBoids", numBoids.Values.Sum());

            // Populate grid
            gridShader.Dispatch(updateGridKernel, Mathf.CeilToInt(numBoids.Values.Sum() / blockSize), 1, 1);

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
            gridShader.Dispatch(rearrangeBoidsKernel, Mathf.CeilToInt(numBoids.Values.Sum() / blockSize), 1,
                1);

            // Compute boid behaviours

            var boidsToKillData = containedHordes
                .Select(horde => Math.Max(_previousNumBoids[horde] - numBoids[horde], 0)).ToArray();

            foreach (var horde in containedHordes)
                if (totalDeathsPerHorde.Keys.Contains(horde))
                    totalDeathsPerHorde[horde] += Math.Max(_previousNumBoids[horde] - numBoids[horde], 0);
                else
                    totalDeathsPerHorde[horde] = Math.Max(_previousNumBoids[horde] - numBoids[horde], 0);

            // If there are any boids to kill
            if (boidsToKillData.Any(x => x != 0))
                boidsToKill.SetData(boidsToKillData, 0, 0, boidsToKillData.Length);

            boidShader.Dispatch(updateBoidsKernel, containedHordes.Count,
                Mathf.CeilToInt(numBoids.Values.Sum() / blockSize), 1);

            // Actually draw the boids
            Graphics.RenderPrimitives(rp, MeshTopology.Quads, numBoids.Values.Sum() * 4);
            Graphics.RenderPrimitives(rpDead, MeshTopology.Quads, deadBoidsCount * 4);

            _justAddedBoids = false;
        }

        private void FixedUpdate()
        {
            if (!local || paused || !_started || numBoids.Values.Sum() == 0) return;

            _boundsBuffer.GetData(_boundsArr, 0, 0, CombatController.MaxParticipants * 4);
            for (var horde = 0; horde < numBoids.Count; horde++)
            {
                // Haven't got proper bounds yet
                if (_boundsArr[horde * 4] == 1024.0f) continue;

                // Extract this horde's bounds from bounds array
                var extents = new Vector2(_boundsArr[horde * 4 + 2] - _boundsArr[horde * 4 + 0],
                    _boundsArr[horde * 4 + 1] - _boundsArr[horde * 4 + 3]);
                var center = new Vector2(extents.x / 2.0f + _boundsArr[horde * 4 + 0],
                    extents.y / 2.0f + _boundsArr[horde * 4 + 3]);
                hordeBounds[containedHordes[horde]] = new Bounds(center, extents * 2);

                // Re-initialise bounds before next calculation
                _boundsArr[horde * 4 + 0] = 1024.0f;
                _boundsArr[horde * 4 + 1] = -1024.0f;
                _boundsArr[horde * 4 + 2] = -1024.0f;
                _boundsArr[horde * 4 + 3] = 1024.0f;
            }

            // Calculate bounds for the combat as a whole
            if (hordeBounds.Count != 0)
            {
                var foundValue = false;
                // For loop to avoid allocs
                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < containedHordes.Count; i++)
                {
                    if (!hordeBounds.ContainsKey(containedHordes[i])) continue;
                    if (foundValue)
                    {
                        bounds.Encapsulate(hordeBounds[containedHordes[i]]);
                    }
                    else
                    {
                        foundValue = true;
                        bounds = hordeBounds[containedHordes[i]];
                    }
                }
            }

            // Dispatch next bounds calculation
            _boundsBuffer.SetData(_boundsArr, 0, 0, CombatController.MaxParticipants * 4);
            boidShader.Dispatch(updateBoundsKernel, Mathf.CeilToInt(numBoids.Values.Sum() / blockSize), 1, 1);
        }

        private void OnDestroy()
        {
            _boundsBuffer.Release();
            boidBuffer.Release();
            boidBufferOut.Release();
            boidsToKill.Release();
            deadBoids.Release();
            deadBoidsCountBuffer.Release();
            gridBuffer.Release();
            gridOffsetBuffer.Release();
            gridOffsetBufferIn.Release();
            gridSumsBuffer.Release();
            gridSumsBuffer2.Release();
            trianglePositions.Release();
        }

        private void AttachBuffers()
        {
            boidShader.SetBuffer(updateBoidsKernel, "boidsIn", boidBufferOut);
            boidShader.SetBuffer(updateBoidsKernel, "boidsOut", boidBuffer);
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
            var newBuffer = new ComputeBuffer(newSize, Marshal.SizeOf(typeof(Boid)));
            var boids = new Boid[numBoids.Values.Sum()];
            boidBuffer.GetData(boids, 0, 0, numBoids.Values.Sum());
            newBuffer.SetData(boids);
            boidBuffer.Release();
            boidBuffer = newBuffer;

            newBuffer = new ComputeBuffer(newSize, Marshal.SizeOf(typeof(Boid)));
            boidBufferOut.GetData(boids, 0, 0, numBoids.Values.Sum());
            newBuffer.SetData(boids);
            boidBufferOut.Release();
            boidBufferOut = newBuffer;

            newBuffer = new ComputeBuffer(newSize, Marshal.SizeOf(typeof(Boid)), ComputeBufferType.Append);
            deadBoids.GetData(boids, 0, 0, deadBoidsCount);
            newBuffer.SetData(boids);
            deadBoids.Release();
            deadBoids = newBuffer;

            // Resize grid buffer
            var grid = new uint2[numBoids.Values.Sum()];
            newBuffer = new ComputeBuffer(newSize, 8);
            gridBuffer.GetData(grid, 0, 0, numBoids.Values.Sum());
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
        /// <returns></returns>
        public bool PosInHorde(Vector2 pos, float range)
        {
            var rangeSq = range * range;

            // Early return if outside bounds
            if (!bounds.Contains(pos)) return false;

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
        ///     Add new boids to myself (I am a combat boids controller)
        /// </summary>
        /// <param name="newBoidsBuffer">The compute buffer containing the boids to add</param>
        /// <param name="newBoidsCount">How many boids to add from the compute buffer</param>
        public void AddBoids(ComputeBuffer newBoidsBuffer, int newBoidsCount, HordeController boidsHorde)
        {
            Debug.Log($"COMBAT BOIDS: Adding boids {boidsHorde.Id.Object}");
            // Resize buffers if too small
            if (numBoids.Values.Sum() + newBoidsCount > boidBuffer.count)
                ResizeBuffers((numBoids.Values.Sum() + newBoidsCount) * 2);


            // Load boids into memory
            var newBoids = new Boid[newBoidsCount];
            newBoidsBuffer.GetData(newBoids, 0, 0, newBoidsCount);

            // Update horde number to use index in current combat
            for (var i = 0; i < newBoidsCount; i++) newBoids[i].horde = containedHordes.Count;

            hordeIDs[boidsHorde] = containedHordes.Count;

            // Append boids to buffer
            boidBuffer.SetData(newBoids, 0, numBoids.Values.Sum(), newBoidsCount);
            boidBufferOut.SetData(newBoids, 0, numBoids.Values.Sum(), newBoidsCount);

            _previousNumBoids.Add(boidsHorde, newBoidsCount);
            numBoids.Add(boidsHorde, newBoidsCount);
            containedHordes.Add(boidsHorde);

            var upTextureArray = new Texture2DArray(64, 64, containedHordes.Count, TextureFormat.ARGB32, false);
            var upRightTextureArray = new Texture2DArray(64, 64, containedHordes.Count, TextureFormat.ARGB32, false);
            var upLeftTextureArray = new Texture2DArray(64, 64, containedHordes.Count, TextureFormat.ARGB32, false);
            var leftTextureArray = new Texture2DArray(64, 64, containedHordes.Count, TextureFormat.ARGB32, false);
            var rightTextureArray = new Texture2DArray(64, 64, containedHordes.Count, TextureFormat.ARGB32, false);
            var downLeftTextureArray = new Texture2DArray(64, 64, containedHordes.Count, TextureFormat.ARGB32, false);
            var downRightTextureArray = new Texture2DArray(64, 64, containedHordes.Count, TextureFormat.ARGB32, false);
            var downTextureArray = new Texture2DArray(64, 64, containedHordes.Count, TextureFormat.ARGB32, false);
            for (var i = 0; i < containedHordes.Count; i++)
            {
                var id = hordeIDs[containedHordes[i]];

                if (!Runner.TryFindBehaviour<HordeController>(containedHordes[i], out var horde))
                    Debug.LogWarning($"Failed to find horde {containedHordes[i]} to get texture");

                var material = horde.Boids.GetMaterial();
                var upTex = material.GetTexture("_RatUp") as Texture2D;
                var upTexColours = upTex.GetPixels();
                upTextureArray.SetPixels(upTexColours, id);

                var rightTex = material.GetTexture("_RatRight") as Texture2D;
                var righTexColours = rightTex.GetPixels();
                rightTextureArray.SetPixels(righTexColours, id);


                var upRightTex = material.GetTexture("_RatUpRight") as Texture2D;
                var upRightTexColours = upRightTex.GetPixels();
                upRightTextureArray.SetPixels(upRightTexColours, id);

                var upLeftTex = material.GetTexture("_RatUpLeft") as Texture2D;
                var upLeftTexColours = upLeftTex.GetPixels();
                upLeftTextureArray.SetPixels(upLeftTexColours, id);

                var leftTex = material.GetTexture("_RatLeft") as Texture2D;
                var leftTexColours = leftTex.GetPixels();
                leftTextureArray.SetPixels(leftTexColours, id);

                var downTex = material.GetTexture("_RatDown") as Texture2D;
                var downTexColours = downTex.GetPixels();
                downTextureArray.SetPixels(downTexColours, id);

                var downRightTex = material.GetTexture("_RatDownRight") as Texture2D;
                var downRightTexColours = downRightTex.GetPixels();
                downRightTextureArray.SetPixels(downRightTexColours, id);

                var downLeftTex = material.GetTexture("_RatDownLeft") as Texture2D;
                var downLeftTexColours = downLeftTex.GetPixels();
                downLeftTextureArray.SetPixels(downLeftTexColours, id);
            }

            upTextureArray.Apply();
            upRightTextureArray.Apply();
            upLeftTextureArray.Apply();
            leftTextureArray.Apply();
            rightTextureArray.Apply();
            downLeftTextureArray.Apply();
            downRightTextureArray.Apply();
            downTextureArray.Apply();

            var newMat = new Material(boidMat);
            newMat.SetTexture("_RatUpArr", upTextureArray);
            newMat.SetTexture("_RatUpRightArr", upRightTextureArray);
            newMat.SetTexture("_RatUpLeftArr", upLeftTextureArray);
            newMat.SetTexture("_RatLeftArr", leftTextureArray);
            newMat.SetTexture("_RatRightArr", rightTextureArray);
            newMat.SetTexture("_RatDownLeftArr", downLeftTextureArray);
            newMat.SetTexture("_RatDownRightArr", downRightTextureArray);
            newMat.SetTexture("_RatDownArr", downTextureArray);

            rp.material = newMat;

            boidShader.SetInt("numBoids", numBoids.Values.Sum());
            _justAddedBoids = true;
        }

        /// <summary>
        ///     Called by the horde that wants its boids back from the combat boids.
        /// </summary>
        /// <param name="hordeBuffer">Buffer on the normal boids to put the combat boids into</param>
        /// <param name="hordeBufferOut">BufferOut on the normal boids to put the combat boids into</param>
        /// <param name="horde">The horde that is wanting its boids back</param>
        public void RetrieveBoids(ComputeBuffer hordeBuffer, ComputeBuffer hordeBufferOut, ComputeBuffer hordeCorpses,
            ComputeBuffer hordeCorpseCount, ref int hordeDeadBoidsCount, HordeController horde)
        {
            Debug.Log("COMBAT BOIDS: retrieving boids");
            // Transfer live boids
            var boids = new Boid[numBoids.Values.Sum()];
            boidBufferOut.GetData(boids, 0, 0, numBoids.Values.Sum());
            var combatBoids = new List<Boid>();
            var hordeBoids = new List<Boid>();

            var hordeID = hordeIDs[horde];
            foreach (var boid in boids)
                if (boid.horde == hordeID)
                    hordeBoids.Add(boid);
                else
                    combatBoids.Add(boid);

            Debug.Log($"COMBAT BOIDS: Giving horde back {hordeBoids.Count} and keeping {combatBoids.Count}");

            var hordeBoidsArr = hordeBoids.ToArray();
            hordeBuffer.SetData(hordeBoidsArr, 0, 0, hordeBoidsArr.Length);
            hordeBufferOut.SetData(hordeBoidsArr, 0, 0, hordeBoidsArr.Length);
            var combatBoidsArr = combatBoids.ToArray();
            boidBuffer.SetData(combatBoidsArr, 0, 0, combatBoidsArr.Length);
            boidBufferOut.SetData(combatBoidsArr, 0, 0, combatBoidsArr.Length);
            numBoids.Remove(horde);
            _previousNumBoids.Remove(horde);

            // Transfer corpses
            boids = new Boid[deadBoidsCount];
            deadBoids.GetData(boids, 0, 0, deadBoidsCount);
            combatBoids.Clear();
            hordeBoids.Clear();

            foreach (var boid in boids)
                if (boid.horde == hordeID)
                    hordeBoids.Add(boid);
                else
                    combatBoids.Add(boid);

            hordeBoidsArr = hordeBoids.ToArray();
            combatBoidsArr = combatBoids.ToArray();

            hordeCorpses.SetData(hordeBoidsArr, 0, 0, hordeBoidsArr.Length);
            deadBoids.SetData(combatBoidsArr, 0, 0, combatBoidsArr.Length);

            uint[] count = { Convert.ToUInt32(hordeBoidsArr.Length) };
            hordeCorpseCount.SetData(count, 0, 0, 1);
            hordeDeadBoidsCount = Convert.ToInt32(count[0]);
            count[0] = Convert.ToUInt32(combatBoidsArr.Length);
            deadBoidsCountBuffer.SetData(count, 0, 0, 1);
            deadBoidsCount = combatBoidsArr.Length;

            containedHordes.Remove(horde);

            boidShader.SetInt("numBoids", numBoids.Values.Sum());
            _justAddedBoids = true;
        }

        /// <summary>
        ///     Fully remove a horde's boids from this controller
        /// </summary>
        /// <param name="horde"></param>
        public void RemoveBoids(NetworkBehaviourId hordeBehaviour)
        {
            Debug.Log($"COMBAT BOIDS: Removing boids {hordeBehaviour}");

            // Already removed
            if (containedHordes.All(x => x != hordeBehaviour)) return;

            // Transfer live boids
            var boids = new Boid[numBoids.Values.Sum()];
            boidBufferOut.GetData(boids, 0, 0, numBoids.Values.Sum());

            var horde = containedHordes.Find(x => x == hordeBehaviour);

            var hordeID = hordeIDs[horde];

            var combatBoidsArr = boids.Where(boid => boid.horde != hordeID).ToArray();
            boidBuffer.SetData(combatBoidsArr, 0, 0, combatBoidsArr.Length);
            boidBufferOut.SetData(combatBoidsArr, 0, 0, combatBoidsArr.Length);
            numBoids.Remove(horde);
            _previousNumBoids.Remove(horde);

            containedHordes.Remove(horde);

            boidShader.SetInt("numBoids", numBoids.Values.Sum());
            _justAddedBoids = true;
        }
    }
}