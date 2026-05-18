using UnityEngine;
using DroneSimulator.AttackControllers;
using System;

namespace DroneSimulator.ObjectControllers
{
    public abstract class ObjectController : MonoBehaviour
    {
        [SerializeField] private ObjectControllerType m_ObjectType;
        public ObjectControllerType ObjectType
        {
            get{ return m_ObjectType; }
        }
    }
    public enum ObjectControllerType
    {
        SpawnPad
    }
}