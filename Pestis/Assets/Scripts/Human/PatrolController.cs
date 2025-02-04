using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace Human
{
    public class PatrolController : NetworkBehaviour
    {
        [SerializeField] private GameObject humanPrefab;
        [SerializeField] private GameObject poi; // POI reference (van)
        [SerializeField] private float patrolRadius = 5.0f;

        private Transform _poiCenter;
        private List<GameObject> _spawnedHumans = new();

        [Networked]
        [OnChangedRender(nameof(OnHumanCountChanged))]

        public int HumanCount { get; private set; } = 6; // Networked human count

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
            Debug.Log($"[Network] humanCount changed to {HumanCount}");
            AdjustHumanCount();
        }

        private void AdjustHumanCount()
        {
            int difference = HumanCount - _spawnedHumans.Count;

            if (difference > 0)
            {
                // Spawn more humans
                for (int i = 0; i < difference; i++)
                {
                    Vector3 spawnPosition = _poiCenter.position + (Vector3)Random.insideUnitCircle * patrolRadius;
                    spawnPosition.z = 0;
                    GameObject human = Instantiate(humanPrefab, spawnPosition, Quaternion.identity);
                    _spawnedHumans.Add(human);

                    // Assign POI to HumanController
                    if (human.TryGetComponent<HumanController>(out HumanController humanScript))
                    {
                        humanScript.SetPOI(_poiCenter);
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

        //can be used when dynamic change of human count is needed, for example, buttons on the UI
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void UpdateHumanCountRpc(int newCount)
        {
            HumanCount = newCount; // Updates across all clients
        }
    }
}