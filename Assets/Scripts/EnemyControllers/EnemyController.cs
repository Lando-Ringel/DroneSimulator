using UnityEngine;
using DroneSimulator.AttackControllers;
using System;
using DroneSimulator.DroneControllers;
using System.Collections.Generic;

namespace DroneSimulator.EnemyControllers
{
    public abstract class EnemyController : ReplayableController
    {
        [SerializeField] private EnemyControllerType m_EnemyType;
        public EnemyControllerType EnemyType
        {
            get{ return m_EnemyType; }
        }
        public abstract void TakeDamage(AttackController attackController, Vector3 attackPosition);
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
        public virtual Queue<ReplayableState> SetUpReplayModel()
        {
            if(m_ReplayModelObject==null)
            {
                m_ReplayModelObject = Instantiate(ReplayModelPrefab, m_ReplayBuffer.Peek().Position, m_ReplayBuffer.Peek().Rotation);
            }
            else
            {
                m_ReplayModelObject.SetActive(true);
            }
            m_Model3D.SetActive(false);
            return new Queue<ReplayableState>(m_ReplayBuffer);            
        }

        public virtual void RemoveReplayModel()
        {
            m_ReplayModelObject.SetActive(false);
            m_Model3D.SetActive(true);
        }

    }
    public enum EnemyControllerType
    {
        Tank
    }
}