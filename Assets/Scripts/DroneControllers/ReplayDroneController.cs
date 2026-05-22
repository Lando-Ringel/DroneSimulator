using UnityEngine;
using DroneSimulator.EnemyControllers;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

namespace DroneSimulator.DroneControllers
{
    public class ReplayDroneController : ReplayableObjectController
    {
        [SerializeField] protected Transform m_PiecesHolder;
        protected Rigidbody[] m_Pieces;
        [SerializeField] protected GameObject m_explosionPrefab;
        [SerializeField] protected Animator m_Animator;
        [SerializeField] protected GameObject m_Grenade;
        void Start()
        {
            m_Pieces = new Rigidbody[m_PiecesHolder.childCount];
            for(int i = 0; i < m_PiecesHolder.childCount; i++)
            {
                m_Pieces[i] = m_PiecesHolder.GetChild(i).GetComponent<Rigidbody>();
                m_Pieces[i].isKinematic = true;
            }
        }
        public void Explode()
        {
            m_Animator.enabled=false;
            m_Grenade.SetActive(false);
            Instantiate(m_explosionPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject, 4);
            foreach(Rigidbody piece in m_Pieces)
            {
                piece.isKinematic = false;
                piece.transform.parent = null;

                piece.AddExplosionForce(
                    450f,
                    transform.position,
                    5f);
                Destroy(piece.gameObject, 4);
            }            
        }
        public override void MoveReplayableObject(ReplayableState replayableState)
        {
            transform.position = replayableState.Position;
            transform.rotation = replayableState.Rotation;
        }


    }


}