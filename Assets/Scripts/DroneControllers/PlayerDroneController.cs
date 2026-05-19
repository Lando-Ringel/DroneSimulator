using UnityEngine;
using DroneSimulator.EnemyControllers;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

namespace DroneSimulator.DroneControllers
{
    public abstract class PlayerDroneController : ReplayableController
    {
        [SerializeField] private DroneControllerType m_DroneType;
        public DroneControllerType DroneType
        {
            get{ return m_DroneType; }
        }
        [SerializeField] protected Transform m_Camera;
        [SerializeField] protected GameObject m_AttackPrefab;
        public GameObject AttackPrefab
        {
            get{ return m_AttackPrefab; }
        }
        public abstract void EnemyHit(EnemyController enemyController);
        public abstract void EnemyKilled(EnemyController enemyController);
        public abstract void LoadAmmo(string alertText = "");
        public abstract void ResetDrone(InputAction.CallbackContext context);
        public event Action<PlayerDroneController, EnemyController> OnKilled;
        protected void RaiseKilled(EnemyController enemyController)
        {
            OnKilled?.Invoke(this, enemyController);
        }
    }
    public enum DroneControllerType
    {
        Kamikaze
    }

}