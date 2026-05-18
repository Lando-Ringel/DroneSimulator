using UnityEngine;
using DroneSimulator.AttackControllers;

namespace DroneSimulator.EnemyControllers
{
    public abstract class EnemyController : MonoBehaviour
    {
        [SerializeField] private EnemyControllerType m_EnemyType;
        public EnemyControllerType EnemyType
        {
            get{ return m_EnemyType; }
        }
        public  abstract void TakeDamage(AttackController attackController);
        
    }
    public enum EnemyControllerType
    {
        Tank
    }
}