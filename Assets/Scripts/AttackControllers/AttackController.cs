using UnityEngine;

namespace DroneSimulator.AttackControllers
{
    public class AttackController : MonoBehaviour
    {
        [SerializeField] private AttackControllerType m_AttackType;
        public AttackControllerType AttackType
        {
            get{ return m_AttackType; }
        }

        [SerializeField] private float m_AttackDamage;
        public float AttackDamage
        {
            get{ return m_AttackDamage; }
        }


    }
    public enum AttackControllerType
    {
        Grenade
    }
}