using DroneSimulator.EnemyControllers;
using UnityEngine;

namespace DroneSimulator.AttackControllers
{
    public class GrenadeController : AttackController
    {
        [SerializeField] private GameObject m_explosionPrefab;
        [SerializeField] private float m_velocityThreshold = 3f;
        private LayerMask m_EnemyLayerMask;
        private bool m_HasExploded;
        private float m_mass = 2f;
        public float Mass
        {
            get { return m_mass; }
        }
        void Awake()
        {
            m_EnemyLayerMask = LayerMask.NameToLayer("Enemy");
        }

        // Inside GrenadeController.cs
        public void Explode(Vector3 position)
        {
            if (m_HasExploded) return;
            m_HasExploded = true;
                
            // Offset the explosion slightly so it doesn't clip into the ground
            Vector3 explosionPosition = position + new Vector3(0, 0.1f, 0);
            Instantiate(m_explosionPrefab, explosionPosition, Quaternion.identity);
            
            Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // This handles the explosion logic when the grenade is ALREADY dropped
            if (GetComponent<Rigidbody>() != null && !m_HasExploded)
            {
                if (collision.relativeVelocity.magnitude > m_velocityThreshold)
                {
                    Explode(collision.contacts[0].point);
                    if(collision.gameObject.layer == m_EnemyLayerMask && collision.gameObject.TryGetComponent<EnemyController>(out EnemyController enemyController))
                    {
                        enemyController.TakeDamage(this, transform.position);
                    }
                }
            }
        }
    }
}