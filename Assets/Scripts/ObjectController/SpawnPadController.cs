using UnityEngine;
using DroneSimulator.AttackControllers;
using DroneSimulator.DroneControllers;

namespace DroneSimulator.ObjectControllers
{
    public  class SpawnPadController : ObjectController
    {
        private LayerMask m_PlayerLayerMask;
        [SerializeField] private Transform m_SpawnPoint;
        void Awake()
        {
            m_PlayerLayerMask = LayerMask.NameToLayer("Player");
        }
        public void OnCollisionEnter(Collision collision)
        {
            if(collision.gameObject.layer == m_PlayerLayerMask
                && collision.gameObject.TryGetComponent<PlayerDroneController>(out PlayerDroneController playerController))
            {
                playerController.LoadAmmo("LOAD AMMO");
            }
        }

        public Vector3 GetSpawnPointPosition()
        {
            return m_SpawnPoint.position;
        }

    }

}