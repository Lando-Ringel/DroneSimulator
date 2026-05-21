using UnityEngine;
using DroneSimulator.EnemyControllers;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

namespace DroneSimulator.DroneControllers
{
    public abstract class PlayerDroneController : ReplayableController
    {
        protected bool m_IsInitialized;
        protected Rigidbody m_Rigidbody;        
        protected InputSystem_Actions m_inputSystem_Actions;
        [SerializeField] protected FlightMode m_FlightMode;
        [SerializeField] protected DroneControllerType m_DroneType;
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
        public abstract void ResetDrone();
        public event Action<PlayerDroneController, EnemyController> OnKilled;
        protected void RaiseKilled(EnemyController enemyController)
        {
            m_Model3D.SetActive(false);
            m_Rigidbody.isKinematic = true;
            OnKilled?.Invoke(this, enemyController);
        }
        public abstract void InitializePlayerController(InputSystem_Actions inputSystem_Actions);
    }
    public enum DroneControllerType
    {
        Kamikaze
    }
    public enum FlightMode
    {
        Acro,
        Angle
    }    

}