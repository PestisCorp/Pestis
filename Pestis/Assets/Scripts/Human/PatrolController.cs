using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace Human
{
    public class PatrolController : NetworkBehaviour
    {
        [SerializeField] private GameObject humanPrefab;
        [SerializeField] private GameObject poi; // POI reference (van)

        private Transform _poiCenter;
        private List<GameObject> _spawnedHumans = new();

        [Networked]
        [OnChangedRender(nameof(OnHumanCountChanged))]
        public int HumanCount { get; private set; } = 6; // Networked human count

        [Networked]
        [OnChangedRender(nameof(OnPatrolRadiusChanged))]
        public float PatrolRadius { get; private set; } = 5.0f; // Networked patrol radius

        public override void Spawned()
        {
            base.Spawned();

            if (poi != null)
            {
                _poiCenter = poi.transform;
            }
            else
            {
                return;
            }

            AdjustHumanCount(); // Ensure correct initial human count
        }

        private void OnHumanCountChanged()
        {
            AdjustHumanCount();
        }

        private void OnPatrolRadiusChanged()
        {
            AdjustPatrolRadius();
        }

        private void AdjustPatrolRadius()
        {
            // Update the patrol radius for all humans
            foreach (var human in _spawnedHumans)
            {
                if (human.TryGetComponent<HumanController>(out HumanController humanScript))
                {
                    humanScript.UpdatePatrolRadius(PatrolRadius);
                }
            }
        }

        private void AdjustHumanCount()
        {
            int difference = HumanCount - _spawnedHumans.Count;

            if (difference > 0)
            {
                // Spawn more humans
                for (int i = 0; i < difference; i++)
                {
                    Vector3 spawnPosition = _poiCenter.position + (Vector3)Random.insideUnitCircle * PatrolRadius;
                    spawnPosition.z = 0;
                    GameObject human = Instantiate(humanPrefab, spawnPosition, Quaternion.identity);
                    _spawnedHumans.Add(human);

                    // Assign POI to HumanController
                    if (human.TryGetComponent<HumanController>(out HumanController humanScript))
                    {
                        humanScript.SetPOI(_poiCenter);
                        humanScript.UpdatePatrolRadius(PatrolRadius);
                    }
                }
            }
            else if (difference < 0)
            {
                // Remove excess humans
                for (int i = 0; i < -difference; i++)
                {
                    if (_spawnedHumans.Count > 0)
                    {
                        GameObject toRemove = _spawnedHumans[_spawnedHumans.Count - 1];
                        _spawnedHumans.RemoveAt(_spawnedHumans.Count - 1);
                        Destroy(toRemove);
                    }
                }
            }
        }

        // Can be used when dynamically changing human count is needed (e.g., UI buttons)
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void UpdateHumanCountRpc(int newCount)
        {
            if (newCount < HumanCount)
            {
                // If newCount is 0, that means we killed everyone.
                if (newCount == 0)
                {
                    // For each human in the list
                    foreach (var humanGO in _spawnedHumans)
                    {
                        if (humanGO.TryGetComponent(out HumanController hc))
                        {
                            hc.Die();
                        }
                    }

                    // We'll let them remove themselves in 5s
                    _spawnedHumans.Clear();
                }
            }

            HumanCount = newCount;
        }
    }
}