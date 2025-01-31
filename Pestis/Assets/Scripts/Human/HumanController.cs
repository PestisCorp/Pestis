using UnityEngine;
using System.Collections.Generic;

namespace Human
{
    public class HumanPatrolController : MonoBehaviour
    {
        public GameObject humanPrefab;

        //number of human patrolling
        public int humanCount = 6;

        //radius of human patrol
        public float patrolRadius = 5.0f;

        //speed of human patrol
        public float patrolSpeed = 1.0f;
        private List<GameObject> _spawnedHumans = new();

        private Vector2 _poiCenter;

        //point of interest
        public POI poi = null; //point of interest which can be added later


        private void Start()
        {
            if (poi != null)
            {
                _poiCenter = new Vector2(poi.transform.position.x, poi.transform.position.y);
            }
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
                //randomly spawn human in the area specified
                Vector2 spawnPosition = _poiCenter + Random.insideUnitCircle * patrolRadius;
                GameObject human = Instantiate(humanPrefab, new Vector3(spawnPosition.x, spawnPosition.y, 0),
                    Quaternion.identity);
                _spawnedHumans.Add(human);
            }
        }

        private void PatrolHumans()
        {
            //humans orbiting around
            for (int i = 0; i < _spawnedHumans.Count; i++)
            {
                GameObject human = _spawnedHumans[i];
                //time-based dynamic angle update, also ensures that humans face different directions
                float angle = Time.time * patrolSpeed + i * (2 * Mathf.PI / humanCount);
                //calculate new position based on angle and radius
                Vector2 newPosition = _poiCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * patrolRadius;
                human.transform.position = newPosition;
            }
        }
    }
}