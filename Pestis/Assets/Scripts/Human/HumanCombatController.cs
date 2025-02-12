using UnityEngine;
using Fusion;
using POI;

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

        private bool isUnderAttack = false;
        private Horde.HordeController attackingHorde;

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

        private void AttackHorde()
        {
            if (attackingHorde == null) return;

            attackingHorde.DealDamageRpc(attackDamage);
            Debug.Log($"[Human] Attacking Horde {attackingHorde.Object.Id} for {attackDamage} damage!");
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