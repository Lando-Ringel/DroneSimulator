using UnityEngine;
using DroneSimulator.AttackControllers;

namespace DroneSimulator.ObjectControllers
{
    public  class SpawnPadController : ObjectController
    {
        [SerializeField] private Transform m_SpawnPoint;
        public Vector3 GetSpawnPointPosition()
        {
            return m_SpawnPoint.position;
        }
        
    }

}