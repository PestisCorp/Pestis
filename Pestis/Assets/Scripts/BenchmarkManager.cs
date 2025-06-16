using System;
using System.Collections.Generic;
using UnityEngine;

public class BenchmarkManager : MonoBehaviour
{
    public GameObject boidsPrefab;

    private readonly List<RatBoids> _boidControllers = new();

    /// <summary>
    ///     Current iteration of the benchmark.
    /// </summary>
    private int _currentIteration;

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

    private void Start()
    {
        _hordeSpawnPositions = CalculateHordeSpawnPositions();
    }

    private void FixedUpdate()
    {
        if (!_isRunning) return;

        if (_ratsPerHorde * _hordeCount >= _maxRats)
            FinishIteration();

        _hordeCount = _stepHordes(_hordeCount);
        _ratsPerHorde = _stepRatsPerHorde(_ratsPerHorde);

        // Instantiate new boid controllers if needed
        if ((int)_hordeCount != _boidControllers.Count)
            while (_boidControllers.Count < (int)_hordeCount)
            {
                var newController = Instantiate(boidsPrefab).GetComponent<RatBoids>();
                newController.TargetPos = _hordeSpawnPositions[_boidControllers.Count] * new Vector2(10, 10);
                _boidControllers.Add(newController);
            }

        foreach (var controller in _boidControllers) controller.AliveRats = (int)_ratsPerHorde;
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

        // TODO - save results, etc.

        if (_currentIteration >= _maxIterations)
        {
            Debug.Log("Benchmark complete.");
            _isRunning = false;
            return;
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
    public void StartProfile(string profileName, Func<float, float> stepRatsPerHorde, Func<float, float> stepHordes,
        int maxRats, int maxIterations)
    {
        _stepRatsPerHorde = stepRatsPerHorde;
        _stepHordes = stepHordes;
        _maxRats = maxRats;
        _maxIterations = maxIterations;

        Debug.Log($"Starting benchmark profile: {profileName}");
        Debug.Log($"Max rats: {_maxRats}, Max iterations: {_maxIterations}");

        ResetForNextIteration();
    }

    /// <summary>
    ///     Wipe per-iteration state and prepare for the next iteration of the benchmark.
    /// </summary>
    private void ResetForNextIteration()
    {
        // Clear existing boid controllers
        foreach (var controller in _boidControllers) Destroy(controller.gameObject);
        _boidControllers.Clear();
        _currentIteration = 0;
        _ratsPerHorde = 1;
        _hordeCount = 0;
    }
}