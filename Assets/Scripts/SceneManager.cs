using UnityEngine;

namespace DroneSimulator
{
    public class SceneManager : MonoBehaviour
    {
        public static SceneManager Instance {get; private set;}

        private void Awake()
        {
            if(Instance !=null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } 
    }
}