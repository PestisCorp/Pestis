using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;

public class BenchmarkManager : MonoBehaviour
{
    public GameObject boidsPrefab;

    [SerializeField] private GameObject profilesUI;

    [SerializeField] private GameObject progressUI;

    [SerializeField] private TMP_Text progressUIText;

    private readonly List<RatBoids> _boidControllers = new();

    /// <summary>
    ///     Stores the time each of the last 240 frames took to render.
    /// </summary>
    private readonly float[] _fpsWindow = new float[240];

    private readonly float[] _fpsWindowPhysics = new float[240];

    /// <summary>
    ///     Current iteration of the benchmark.
    /// </summary>
    private int _currentIteration;

    private StreamWriter _dataWriter;

    private int _fpsWindowIndex;

    private int _fpsWindowPhysicsIndex;

    /// <summary>
    ///     How many hordes are currently in the benchmark.
    /// </summary>
    private float _hordeCount;

    /// <summary>
    ///     Calculated at start, used to determine where to spawn hordes by index.
    /// </summary>
    private List<Vector2> _hordeSpawnPositions;

    private bool _isRunning;

    /// <summary>
    ///     How many iterations the benchmark will run.
    /// </summary>
    private int _maxIterations;

    /// <summary>
    ///     Benchmark is stopped when this number of rats is reached.
    /// </summary>
    private int _maxRats;

    /// <summary>
    ///     Current number of rats in each horde.
    ///     Is a float instead of an int so that multiplicative growth can be used at low rat counts.
    /// </summary>
    private float _ratsPerHorde;

    /// <summary>
    ///     Called each timestep to update the number of hordes.
    /// </summary>
    private Func<float, float> _stepHordes;

    /// <summary>
    ///     Called each timestep to update the number of rats in each horde.
    /// </summary>
    private Func<float, float> _stepRatsPerHorde;

    /// <summary>
    ///     Current FPS, calculated over the last 5 seconds.
    /// </summary>
    private float _fps
    {
        get
        {
            float time = 0;
            var frames = 0;
            for (var i = _fpsWindowIndex;
                 i < _fpsWindow.Length && time < 5;
                 i = i == 0 ? _fpsWindow.Length - 1 : i - 1)
            {
                time += _fpsWindow[i];
                frames++;
            }

            return frames / time;
        }
    }

    /// <summary>
    ///     Current FPS for physics updates, calculated over the last 5 seconds.
    /// </summary>
    private float _fpsPhysics
    {
        get
        {
            float time = 0;
            var frames = 0;
            for (var i = _fpsWindowPhysicsIndex;
                 i < _fpsWindowPhysics.Length && time < 5;
                 i = i == 0 ? _fpsWindow.Length - 1 : i - 1)
            {
                time += _fpsWindowPhysics[i];
                frames++;
            }

            return frames / time;
        }
    }

    private void Start()
    {
        Debug.Log("Starting benchmark manager...");
        Debug.Log($"Supports Async Compute: {SystemInfo.supportsAsyncCompute}");
        _hordeSpawnPositions = CalculateHordeSpawnPositions();
        Debug.Log("Calculated horde spawn positions.");
    }

    private void Update()
    {
        // Update FPS window
        _fpsWindow[_fpsWindowIndex] = Time.deltaTime;
        _fpsWindowIndex = (_fpsWindowIndex + 1) % _fpsWindow.Length;
    }

    private void FixedUpdate()
    {
        if (!_isRunning) return;

        // Update FPS window
        _fpsWindowPhysics[_fpsWindowPhysicsIndex] = Time.deltaTime;
        _fpsWindowPhysicsIndex = (_fpsWindowPhysicsIndex + 1) % _fpsWindowPhysics.Length;

        // Update progress UI
        progressUIText.text =
            $"Iteration {_currentIteration + 1}/{_maxIterations}\nHordes: {_hordeCount}\nRats per horde: {_ratsPerHorde}\nTotal rats: {_ratsPerHorde * _boidControllers.Count}\nFPS: {_fps:F2}\nPhysics FPS: {_fpsPhysics:F2}\nGraphics:{SystemInfo.graphicsDeviceType}";

        _dataWriter.WriteLine(
            $"{_currentIteration},{_boidControllers.Count},{_ratsPerHorde},{_ratsPerHorde * _boidControllers.Count},{_fps:F2},{_fpsPhysics:F2}");

        if (_ratsPerHorde * _hordeCount >= _maxRats || _fps < 5)
            FinishIteration();

        _hordeCount = _stepHordes(_hordeCount);
        _ratsPerHorde = _stepRatsPerHorde(_ratsPerHorde);

        // Instantiate new boid controllers if needed
        if ((int)_hordeCount != _boidControllers.Count)
            while (_boidControllers.Count < (int)_hordeCount)
            {
                Debug.Log("Instantiating new boid controller.");
                var newController = Instantiate(boidsPrefab).GetComponent<RatBoids>();
                newController.TargetPos = _hordeSpawnPositions[_boidControllers.Count] * new Vector2(10, 10);
                _boidControllers.Add(newController);
            }

        foreach (var controller in _boidControllers) controller.AliveRats = (int)_ratsPerHorde;
    }

    private void OnDestroy()
    {
        if (_dataWriter != null)
        {
            _dataWriter.Close();
            _dataWriter.Dispose();
        }
    }

    /// <summary>
    ///     Credit to https://stackoverflow.com/a/3706260/7732024
    /// </summary>
    private static List<Vector2> CalculateHordeSpawnPositions()
    {
        var positions = new List<Vector2>();

        // (di, dj) is a vector - direction in which we move right now
        var di = 1;
        var dj = 0;
        // length of current segment
        var segmentLength = 1;


        // current position (i, j) and how much of current segment we passed
        var i = 0;
        var j = 0;
        var segmentPassed = 0;
        for (var k = 0; k < 20_000; ++k)
        {
            // make a step, add 'direction' vector (di, dj) to current position (i, j)
            i += di;
            j += dj;
            ++segmentPassed;

            positions.Add(new Vector2(i, j));

            if (segmentPassed == segmentLength)
            {
                // done with current segment
                segmentPassed = 0;

                // 'rotate' directions
                var buffer = di;
                di = -dj;
                dj = buffer;

                // increase segment length if necessary
                if (dj == 0) ++segmentLength;
            }
        }

        return positions;
    }

    private void FinishIteration()
    {
        _currentIteration++;
        Debug.Log($"Iteration {_currentIteration}/{_maxIterations} complete.");

        if (_currentIteration >= _maxIterations)
        {
            Debug.Log("Benchmark complete.");
            _isRunning = false;
            _dataWriter.Close();
        }

        // Reset for the next iteration
        ResetForNextIteration();
    }

    /// <summary>
    ///     Start the benchmark with given parameters.
    /// </summary>
    /// <param name="profileName">Name of the benchmark profile, just for organisational purposes.</param>
    /// <param name="stepRatsPerHorde">Function called each timestep to update the number of rats per horde.</param>
    /// <param name="stepHordes">Function called each timestep to update the number of hordes.</param>
    /// <param name="maxRats">When the total number of rats reaches this value, the benchmark will stop.</param>
    /// <param name="maxIterations">How many iterations of the benchmark to perform.</param>
    private void StartProfile(string profileName, Func<float, float> stepRatsPerHorde, Func<float, float> stepHordes,
        int maxRats, int maxIterations)
    {
        _stepRatsPerHorde = stepRatsPerHorde;
        _stepHordes = stepHordes;
        _maxRats = maxRats;
        _maxIterations = maxIterations;

        Debug.Log($"Starting benchmark profile: {profileName}");
        Debug.Log($"Max rats: {_maxRats}, Max iterations: {_maxIterations}");

        _isRunning = true;

        ResetForNextIteration();
        profilesUI.SetActive(false);
        progressUI.SetActive(true);

        _dataWriter =
            new StreamWriter($"{profileName}-{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}.csv"
                , false);
        _dataWriter.WriteLine("Iteration,Hordes,RatsPerHorde,TotalRats,FPS,PhysicsFPS");
    }

    /// <summary>
    ///     Wipe per-iteration state and prepare for the next iteration of the benchmark.
    /// </summary>
    private void ResetForNextIteration()
    {
        // Clear existing boid controllers
        foreach (var controller in _boidControllers) Destroy(controller.gameObject);
        _boidControllers.Clear();
        _ratsPerHorde = 1;
        _hordeCount = 1;
        Array.Clear(_fpsWindow, 0, _fpsWindow.Length);
        Array.Clear(_fpsWindowPhysics, 0, _fpsWindowPhysics.Length);
        _fpsWindowIndex = 0;
        _fpsWindowPhysicsIndex = 0;
    }

    public void Start0_01horde_1rat()
    {
        StartProfile("0.005 Horde, 500 Rat", x => x + 50, x => 10, 500000, 5);
    }
}