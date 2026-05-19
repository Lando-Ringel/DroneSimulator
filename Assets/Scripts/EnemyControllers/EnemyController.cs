using UnityEngine;
using DroneSimulator.AttackControllers;
using System;
using DroneSimulator.DroneControllers;

namespace DroneSimulator.EnemyControllers
{
    public abstract class EnemyController : ReplayableController
    {
        [SerializeField] private EnemyControllerType m_EnemyType;
        public EnemyControllerType EnemyType
        {
            get{ return m_EnemyType; }
        }
        public abstract void TakeDamage(AttackController attackController);
        public event Action<EnemyController> OnKilled;
        protected void RaiseKilled()
        {
            OnKilled?.Invoke(this);
        }
        public event Action<EnemyController> OnHit;
        protected void RaiseHit()
        {
            OnHit?.Invoke(this);
        }

    }
    public enum EnemyControllerType
    {
        Tank
    }
}