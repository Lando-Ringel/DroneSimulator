using UnityEngine;

namespace DroneSimulator.DroneControllers
{
    public class PlayerDroneController : MonoBehaviour
    {
        [SerializeField] private DroneControllerType m_DroneType;
        public DroneControllerType DroneType
        {
            get{ return m_DroneType; }
        }
    }
    public enum DroneControllerType
    {
        Kamikaze
    }
}