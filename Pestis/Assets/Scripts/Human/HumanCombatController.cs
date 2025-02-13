using UnityEngine;
using Fusion;
using POI;
using Horde;
using Players;

namespace Human
{
    public class HumanCombatController : NetworkBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private GameObject poi; // POI reference (van)
        [Networked] private float currentHealth { get; set; }

        [SerializeField] private float attackDamage = 5f; // Damage per attack
        [SerializeField] private float attackCooldown = 1.5f;
        private float lastAttackTime;

        private bool isUnderAttack;
        private HordeController attackingHorde;

        public const int MAX_PARTICIPANTS = 6;

        /// <summary>
        ///     Stores the involved players and a list of their HordeControllers (as NetworkBehaviourId so must be converted before
        ///     use)
        /// </summary>
        [Networked]
        [Capacity(MAX_PARTICIPANTS)]
        public NetworkDictionary<Player, CombatParticipant> Participators { get; }

        private void Start()
        {
            currentHealth = maxHealth;
        }

        private void Update()
        {
            if (isUnderAttack && Time.time - lastAttackTime > attackCooldown)
            {
                AttackHorde();
                lastAttackTime = Time.time;
            }
        }

        public void TakeDamage(float damage, Horde.HordeController attacker)
        {
            if (!isUnderAttack)
            {
                isUnderAttack = true;
                attackingHorde = attacker;
            }

            currentHealth -= damage;
            Debug.Log($"[Human] Took {damage} damage! Remaining Health: {currentHealth}");

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        //human finding nearest rat horde
        public HordeController GetNearestEnemy()
        {
            Vector2 myCenter = poi.transform.position;

            HordeController bestTarget = null;
            var closestDistance = Mathf.Infinity;

            foreach (var kvp in Participators)
            {
                foreach (var hordeID in kvp.Value.Hordes)
                {
                    Runner.TryFindBehaviour(hordeID, out HordeController horde);
                    var dist = ((Vector2)horde.GetBounds().center - myCenter).sqrMagnitude;

                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        bestTarget = horde;
                    }
                }
            }

            return bestTarget;
        }

        private void AttackHorde()
        {
            if (attackingHorde == null) return;

            attackingHorde.DealDamageRpc(attackDamage);
            attackingHorde.EventAttackedHumanRpc(this);
            Debug.Log($"[Human] Attacking Horde {attackingHorde.Object.Id} for {attackDamage} damage!");
        }

        public void AttackHuman(PatrolController patrol)
        {
            patrol.EventAttackedHumanRpc(this);
        }


        private void Die()
        {
            Debug.Log($"[Human] {gameObject.name} has died!");

            // Find the HumanController component
            HumanController baseHuman = GetComponent<HumanController>();

            // Remove human from POI before destroying
            if (poi != null && baseHuman != null)
            {
                poi.GetComponent<POIController>().RemoveHumans(baseHuman);
            }

            Destroy(gameObject); // Remove human from scene
        }

        public float GetAttackDamage()
        {
            return attackDamage;
        }
    }
}