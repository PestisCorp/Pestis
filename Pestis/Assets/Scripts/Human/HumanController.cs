using UnityEngine;

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
        public Sprite deadSprite;


        [SerializeField] private float patrolRadius = 2f;
        [SerializeField] private float patrolSpeed = 1.0f;
        [SerializeField] private float rotationSpeed = 360.0f; // Rotation speed (degrees per second)
        [SerializeField] private float targetTolerance = 0.5f; // Distance before choosing a new target
        private Transform poiCenter; // Center of patrol area (Van)

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Vector2 targetPosition;
        private bool isDead;

        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            rb.mass = Random.Range(0.8f, 1.2f);
            PickNewTarget();
        }

        private void FixedUpdate()
        {
            if (isDead) return;
            if (poiCenter == null) return;

            // Check if the human has reached the patrol target
            if (Vector2.Distance(transform.position, targetPosition) < targetTolerance ||
                rb.linearVelocity.magnitude < 1.0f)
                PickNewTarget();

            // Move towards the target
            MoveTowardsTarget();

            // Update sprite direction
            UpdateSpriteDirection();
        }

        public void Die()
        {
            if (isDead) return;
            isDead = true;

            // Switch to dead sprite
            if (deadSprite != null)
            {
                spriteRenderer.sprite = deadSprite;
            }

            // Disable further movement / collisions
            if (TryGetComponent<Rigidbody2D>(out Rigidbody2D body))
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0;
                body.isKinematic = true;
                body.simulated = false; // So it no longer interacts with physics
            }

            StartCoroutine(RemoveAfterDelay(5f)); // Wait 5 seconds
        }

        private System.Collections.IEnumerator RemoveAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }


        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject == poiCenter.gameObject) // If the human collides with the POI, pick a new target
                PickNewTarget();
        }

        public void SetRadius(float newRadius)
        {
            patrolRadius = newRadius;
        }

        public float GetRadius()
        {
            return patrolRadius;
        }

        public void SetPOI(Transform poi)
        {
            poiCenter = poi;
            PickNewTarget();
        }

        private void PickNewTarget()
        {
            // Select a new patrol point around the POI
            var randomOffset = Random.insideUnitCircle * patrolRadius;
            targetPosition = (Vector2)poiCenter.position + randomOffset;
        }

        public void UpdatePatrolRadius(float newRadius)
        {
            patrolRadius = newRadius;
            PickNewTarget(); // Immediately update to new radius
        }

        /// <param name="force">The force to apply to the human</param>
        /// <returns>New velocity</returns>
        private Vector2 _addForce(Vector2 force)
        {
            rb.AddForce(force);
            rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, patrolSpeed);
            return rb.linearVelocity;
        }


        private void MoveTowardsTarget()
        {
            // Get direction vector
            var direction = (targetPosition - (Vector2)transform.position).normalized;

            // Get desired rotation from desired direction
            var targetRotation = Quaternion.LookRotation(Vector3.forward, direction);

            // If the human is facing exactly away from the target, Unity might fumble the calculation
            if (Quaternion.Inverse(targetRotation) == transform.rotation)
            {
                var degrees = targetRotation.eulerAngles.z;
                targetRotation = Quaternion.Euler(0, 0, degrees + 90); // Offset rotation to fix issue
            }

            // Lerp to desired rotation
            var newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360 * Time.deltaTime);
            // Apply rotation to current direction
            Vector2 headingIn = newRotation * Vector2.up;
            // Push rat in new direction and get current direction
            var currentDirection = _addForce(headingIn.normalized);
            // Turn rat to face current direction
            var currentRotation = Quaternion.LookRotation(Vector3.forward, currentDirection);
            transform.rotation = currentRotation;
        }

        private void UpdateSpriteDirection()
        {
            if (isDead) return;
            var angle = Vector2.SignedAngle(transform.up, Vector2.up);
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