using UnityEngine;
using System.Collections.Generic;
using Fusion;
using POI;

namespace Human
{
    public class HumanPatrolController : NetworkBehaviour
    {
        [SerializeField] private GameObject humanPrefab;

        //number of human patrolling
        [SerializeField] private int humanCount = 6;

        //radius of human patrol
        [SerializeField] private float patrolRadius = 5.0f;

        //speed of human patrol
        [SerializeField] private float patrolSpeed = 1.0f;
        private List<GameObject> _spawnedHumans = new();
        private List<Vector3> _targetPositions = new();

        private Transform _poiCenter;

        //reference to point of interest
        [SerializeField] private POIController poi;


        public override void Spawned()
        {
            base.Spawned();

            if (poi != null)
            {
                _poiCenter = poi.transform;
            }
            else
            {
                Debug.LogWarning("No poi center");
                return;
            }

            SpawnHumans();
        }

        private void Update()
        {
            //update human patrol
            PatrolHumans();
        }

        private void SpawnHumans()
        {
            for (int i = 0; i < humanCount; i++)
            {
                Vector3 spawnPosition = GetRandomPatrolPosition();
                GameObject human = Instantiate(humanPrefab, spawnPosition, Quaternion.identity);
                _spawnedHumans.Add(human);
                _targetPositions.Add(spawnPosition);
            }
        }

        private Vector3 GetRandomPatrolPosition()
        {
            if (_poiCenter == null)
            {
                return Vector3.zero;
            }

            Vector3 randomPatrolPosition = _poiCenter.position + Random.insideUnitSphere * patrolRadius;
            //y = 0 as 2D
            randomPatrolPosition.y = 0;
            return randomPatrolPosition;
        }

        private void PatrolHumans()
        {
            for (int i = 0; i < _spawnedHumans.Count; i++)
            {
                GameObject human = _spawnedHumans[i];

                //Move towards target position
                human.transform.position = Vector3.Lerp(_poiCenter.position, human.transform.position,
                    patrolSpeed * Time.deltaTime);

                //if close to the target position, move towards a new position
                if (Vector3.Distance(human.transform.position, _targetPositions[i]) < 0.5f)
                {
                    _targetPositions[i] = GetRandomPatrolPosition();
                }
            }
        }
    }
}