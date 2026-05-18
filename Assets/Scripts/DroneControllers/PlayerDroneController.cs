using UnityEngine;
using DroneSimulator.EnemyControllers;
namespace DroneSimulator.DroneControllers
{
    public abstract class PlayerDroneController : MonoBehaviour
    {
        [SerializeField] private DroneControllerType m_DroneType;
        public DroneControllerType DroneType
        {
            get{ return m_DroneType; }
        }
        public abstract void EnemyHit(EnemyController enemyController);

        public abstract void EnemyKilled(EnemyController enemyController);

    }
    public enum DroneControllerType
    {
        Kamikaze
    }
}