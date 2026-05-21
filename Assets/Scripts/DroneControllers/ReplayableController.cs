using UnityEngine;
using DroneSimulator.EnemyControllers;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DroneSimulator.SceneManagers;

namespace DroneSimulator.DroneControllers
{
    public abstract class ReplayableController : MonoBehaviour
    {
        [Header("Instant Replay")]
        protected bool m_IsPaused;
        protected bool m_IsDead;        
        protected Queue<ReplayableState> m_ReplayBuffer = new Queue<ReplayableState>();
        public Queue<ReplayableState> ReplayBuffer
        {
            get{return m_ReplayBuffer;}
        }
        protected const int MAX_REPLAY_FRAMES = 350;
        [SerializeField] protected GameObject m_ReplayModelPrefab;
        public GameObject m_ReplayModelObject {get; protected set; }
        public GameObject ReplayModelPrefab
        {
            get{ return m_ReplayModelPrefab; }
        }
        [SerializeField] protected GameObject m_Model3D;

        protected void RecordState()
        {
            m_ReplayBuffer.Enqueue(new ReplayableState {
                Position = transform.position,
                Rotation = transform.rotation,
                TimeStamp = Time.time
            });

            if (m_ReplayBuffer.Count > MAX_REPLAY_FRAMES)
                m_ReplayBuffer.Dequeue();
        }
        public virtual void PauseController()
        {
            m_IsPaused=!m_IsPaused;
        }

    }

    [System.Serializable]
    public struct ReplayableState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float TimeStamp;
    }        
}