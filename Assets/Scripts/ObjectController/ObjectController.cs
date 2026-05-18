using UnityEngine;
using DroneSimulator.AttackControllers;

namespace DroneSimulator.ObjectControllers
{
    public abstract class ObjectController : MonoBehaviour
    {
        [SerializeField] private ObjectControllerType m_EnemyType;
        public ObjectControllerType EnemyType
        {
            get{ return m_EnemyType; }
        }
        public  abstract void TakeDamage(AttackController attackController);
        
    }
    public enum ObjectControllerType
    {
        Restock
    }
}