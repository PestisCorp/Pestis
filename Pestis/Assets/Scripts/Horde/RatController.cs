using System.Collections.Generic;
using UnityEngine;

namespace Horde
{
    /// <summary>
    ///     Component attached to each rat so that clicking a Rat selects the horde it belongs to
    /// </summary>
    public class RatController : MonoBehaviour
    {
        public Sprite DirectionUp;
        public Sprite DirectionUpLeft;
        public Sprite DirectionUpRight;
        public Sprite DirectionLeft;
        public Sprite DirectionRight;
        public Sprite DirectionDownLeft;
        public Sprite DirectionDown;
        public Sprite DirectionDownRight;
        public Sprite Corpse;

        /// <summary>
        ///     If > 0, decrement each physics tick until reaches 0 then destroy self.
        /// </summary>
        public float deathCountdown = -1;

        public Vector2 targetPoint;

        private byte _currentIntraHordeTarget;

        /// <summary>
        ///     Cycles 0,1,2,3 if true, or 3,2,1,0 if false
        /// </summary>
        private bool _cycleIntraHordeTargetForwards = true;

        private HordeController _hordeController;

        private Rigidbody2D _rigidbody;

        private SpriteRenderer _spriteRenderer;

        public void Start()
        {
            _cycleIntraHordeTargetForwards = Random.Range(0.0f, 1.0f) > 0.5f;
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.mass = Random.Range(0.8f, 1.2f);
        }

        private void Update()
        {
            if (deathCountdown != -1) return;

            // Update sprite rotation
            var angle = Vector2.SignedAngle(transform.up, Vector2.up);

            // Normalise to clockwise
            if (angle < 0) angle += 360f;

            if (angle < 22.5f)
                _spriteRenderer.sprite = DirectionUp;
            else if (angle < 67.5)
                _spriteRenderer.sprite = DirectionUpRight;
            else if (angle < 112.5)
                _spriteRenderer.sprite = DirectionRight;
            else if (angle < 157.5)
                _spriteRenderer.sprite = DirectionDownRight;
            else if (angle < 202.5)
                _spriteRenderer.sprite = DirectionDown;
            else if (angle < 247.5)
                _spriteRenderer.sprite = DirectionDownLeft;
            else if (angle < 292.5)
                _spriteRenderer.sprite = DirectionLeft;
            else
                _spriteRenderer.sprite = DirectionUpLeft;
            _spriteRenderer.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, angle));
        }

        private void FixedUpdate()
        {
            if (deathCountdown != -1)
            {
                _spriteRenderer.transform.localRotation = Quaternion.Euler(Vector3.zero);
                transform.rotation = Quaternion.Euler(Vector3.zero);
                _rigidbody.freezeRotation = true;
                _rigidbody.linearDamping = 15;
                if (_hordeController.GetComponent<EvolutionManager>().GetEvolutionaryState().AcquiredEffects.Contains("unlock_necrosis"))
                {
                    Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, 20f);
                    Dictionary<HordeController, int> affectedHordes = new Dictionary<HordeController, int>();
                    foreach (var col in hitColliders)
                    {
                        HordeController affectedEnemy = col.GetComponentInParent<HordeController>();
                        if (affectedEnemy && (affectedEnemy.GetHashCode() != _hordeController.GetHashCode()) && (affectedHordes.ContainsKey(affectedEnemy)))
                        {
                            affectedHordes[affectedEnemy] += 1;
                        }
                    }
                    foreach (var horde in affectedHordes)
                    {
                        horde.Key.DealDamageRpc(horde.Value * 0.1f);
                    }
                }
                deathCountdown -= Time.deltaTime;
                if (deathCountdown <= 0) Destroy(gameObject);
                return;
            }


            if (_hordeController.HordeBeingDamaged)
            {
                targetPoint = _hordeController.HordeBeingDamaged.ClosestRat(transform.position).transform.position;
            }
            else
            {
                if (((Vector2)transform.position - _hordeController.intraHordeTargets[_currentIntraHordeTarget])
                    .magnitude < _hordeController.targetTolerance)
                {
                    if (_cycleIntraHordeTargetForwards)
                    {
                        if (_currentIntraHordeTarget == 3)
                            _currentIntraHordeTarget = 0;
                        else
                            _currentIntraHordeTarget++;
                    }
                    else
                    {
                        if (_currentIntraHordeTarget == 0)
                            _currentIntraHordeTarget = 3;
                        else
                            _currentIntraHordeTarget--;
                    }
                }

                targetPoint = _hordeController.intraHordeTargets[_currentIntraHordeTarget];
            }

            // Get desired direction
            var direction = targetPoint - (Vector2)transform.position;
            // Get desired rotation from desired direction
            var targetRotation = Quaternion.LookRotation(Vector3.forward, direction);

            // If the rat is facing exactly away from target, then Unity fumbles the calculation, so just offset the target
            // https://discussions.unity.com/t/longest-distance-rotation-problem-from-175-to-175-via-0-using-quaternion-rotatetowards-stops-at-5-why/103817
            if (Quaternion.Inverse(targetRotation) == transform.rotation)
            {
                var degrees = targetRotation.eulerAngles.z;
                targetRotation = Quaternion.Euler(0, 0, degrees + 90);
            }

            // Lerp to desired rotation
            var newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 1440 * Time.deltaTime);
            // Apply rotation to current direction
            Vector2 headingIn = newRotation * Vector2.up;
            // Push rat in new direction and get current direction
            var currentDirection = _addForce(headingIn.normalized);
            // Turn rat to face current direction
            var currentRotation = Quaternion.LookRotation(Vector3.forward, currentDirection);
            transform.rotation = currentRotation;
        }

        public void SetHordeController(HordeController controller)
        {
            _hordeController = controller;
        }

        public void SetColor(Color color)
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _spriteRenderer.color = color;
        }

        public HordeController GetHordeController()
        {
            return _hordeController;
        }

        /// <param name="force">The force to apply to the rat</param>
        /// <returns>New velocity</returns>
        private Vector2 _addForce(Vector2 force)
        {
            _rigidbody.AddForce(force);
            _rigidbody.linearVelocity = Vector2.ClampMagnitude(_rigidbody.linearVelocity, 5f);
            return _rigidbody.linearVelocity;
        }


        /// <summary>
        ///     Kill rat and leave corpse
        /// </summary>
        public void Kill()
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _spriteRenderer.sprite = Corpse;
            // Stay dead for 60 seconds, then disappear.
            deathCountdown = 60.0f;
        }

        /// <summary>
        ///     Kill Rat and leave no corpse
        /// </summary>
        public void KillInstant()
        {
            Destroy(gameObject);
        }
    }
}