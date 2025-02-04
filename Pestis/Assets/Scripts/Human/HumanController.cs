using UnityEngine;
using Fusion;

namespace Human
{
    public class HumanController : MonoBehaviour
    {
        public Sprite DirectionUp;
        public Sprite DirectionUpLeft;
        public Sprite DirectionUpRight;
        public Sprite DirectionLeft;
        public Sprite DirectionRight;
        public Sprite DirectionDownLeft;
        public Sprite DirectionDown;
        public Sprite DirectionDownRight;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Transform poiCenter; // Center of patrol area (Van)
        private Vector2 targetPosition;


        [SerializeField] private float patrolRadius = 5.0f;
        [SerializeField] private float patrolSpeed = 1.0f;
        [SerializeField] private float rotationSpeed = 360.0f; // Rotation speed (degrees per second)
        [SerializeField] private float targetTolerance = 0.5f; // Distance before choosing a new target

        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            rb.mass = Random.Range(0.8f, 1.2f);
            PickNewTarget();
        }

        public void SetPOI(Transform poi)
        {
            poiCenter = poi;
            PickNewTarget();
        }

        private void FixedUpdate()
        {
            if (poiCenter == null) return;

            // Check if the human has reached the patrol target
            if (Vector2.Distance(transform.position, targetPosition) < targetTolerance)
            {
                PickNewTarget();
            }

            // Move towards the target
            MoveTowardsTarget();

            // Update sprite direction
            UpdateSpriteDirection();
        }

        private void PickNewTarget()
        {
            // Select a new patrol point around the POI
            Vector2 randomOffset = Random.insideUnitCircle * patrolRadius;
            targetPosition = (Vector2)poiCenter.position + randomOffset;
        }

        private void MoveTowardsTarget()
        {
            // Get direction vector
            Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;

            // Rotate smoothly towards movement direction
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float angle = Mathf.LerpAngle(transform.eulerAngles.z, targetAngle, rotationSpeed * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Euler(0, 0, angle);

            // Apply movement
            rb.linearVelocity = direction * patrolSpeed;
        }

        private void UpdateSpriteDirection()
        {
            float angle = Vector2.SignedAngle(transform.up, Vector2.up);
            if (angle < 0) angle += 360f; // Normalize angle to 0-360 degrees

            if (angle < 22.5f)
                spriteRenderer.sprite = DirectionUp;
            else if (angle < 67.5)
                spriteRenderer.sprite = DirectionUpRight;
            else if (angle < 112.5)
                spriteRenderer.sprite = DirectionRight;
            else if (angle < 157.5)
                spriteRenderer.sprite = DirectionDownRight;
            else if (angle < 202.5)
                spriteRenderer.sprite = DirectionDown;
            else if (angle < 247.5)
                spriteRenderer.sprite = DirectionDownLeft;
            else if (angle < 292.5)
                spriteRenderer.sprite = DirectionLeft;
            else
                spriteRenderer.sprite = DirectionUpLeft;

            spriteRenderer.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, angle));
        }
    }
}