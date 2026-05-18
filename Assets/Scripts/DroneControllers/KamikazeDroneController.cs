using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using DroneSimulator.UI;
using DroneSimulator.AttackControllers;
using DroneSimulator.EnemyControllers;

namespace DroneSimulator.DroneControllers
{
    public class KamikazeDroneController : PlayerDroneController
    {
        [Header("Visuals")]
        [SerializeField] private Transform m_Camera;
        [SerializeField] private float m_CameraTiltSpeed = 50f; // Degrees per second
        [SerializeField] private float m_MinTilt = -60f;
        [SerializeField] private float m_MaxTilt = 60f;    
        [SerializeField] private Transform[] m_Propellers;
        [SerializeField] private float m_MaxPropellerSpeed = 2000f;
        [Header("Flight Settings")]
        [SerializeField] private float m_ThrottleMultiplier = 20f;
        [SerializeField] private float m_MaxVerticalSpeed = 6.5f;
        [SerializeField] private float m_MinVerticalSpeed = -5f;
        [SerializeField] private float m_YawStrength = 5f;
        [SerializeField] private float m_PitchStrength = 10f;
        [SerializeField] private float m_RollStrength = 10f;
        [Header("Battery Settings")]
        [SerializeField] private float m_BaseDrainRate = 0.5f;
        [SerializeField] private float m_DrainEfficiency = 1.0f;
        private float m_MaxBattery = 100f;
        private float m_CurrentBattery = 100f;
        [Header("Smoothing")]
        [SerializeField] private float m_ThrottleLerpSpeed = 2f;
        [SerializeField] private float m_YawLerpSpeed = 4f;
        [Header("Grenade")]
        [SerializeField] private GameObject m_GrenadePrefab;
        [SerializeField] private Transform m_GrenadeHolder;
        [SerializeField] private Transform m_LeftClaw;
        [SerializeField] private Transform m_RightClaw;
        [SerializeField] private float m_ExplosionCollisionVelocity = 5f;
        [SerializeField] private int m_MaxAmmo = 3;
        private int m_CurrentAmmo;
        private GameObject m_GrenadeObj;
        private GrenadeController m_GrenadeController;
        [Header("Combat")]
        private LayerMask m_EnemyLayerMask;
        private int m_lifeCount;
        [Header("Canvas")]
        [SerializeField] private FPVCanvasUIOrganizer fpvCanvasUIOrganizer;
        private InputSystem_Actions m_inputSystem_Actions;
        private Rigidbody m_Rigidbody;
        private Vector3 m_StartPosition;
        private Quaternion m_StartRotation;
        // input storage
        private float m_ThrottleInput;
        private float m_YawInput;
        private float m_PitchInput;
        private float m_RollInput;
        private float m_CameraRotationX = 0f; // Track total rotation
        
        

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            m_StartPosition = transform.position;
            m_StartRotation = transform.rotation;
            m_EnemyLayerMask = LayerMask.NameToLayer("Enemy");
            m_inputSystem_Actions = new InputSystem_Actions();
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Rigidbody.angularDamping = 2f;
            m_Rigidbody.linearDamping = 1f; // helps buttress against falling too fast
            m_CurrentAmmo = m_MaxAmmo;
            ReloadGrenade();

        }
        void OnEnable()
        {
            m_inputSystem_Actions.KamikazeDrone.Enable();
            m_inputSystem_Actions.KamikazeDrone.DropAmmo.performed += DropAmmoPerformed;
            m_inputSystem_Actions.KamikazeDrone.Reset.performed += ResetDrone;
            m_inputSystem_Actions.KamikazeDrone.Reload.performed += CallReloadGrenade;
            foreach(EnemyController enemyController in FindObjectsByType<EnemyController>())
            {
                enemyController.OnKilled += EnemyKilled;
                enemyController.OnHit += EnemyHit;
            }
        }

        void OnDisable()
        {
            m_inputSystem_Actions.KamikazeDrone.Disable();
            m_inputSystem_Actions.KamikazeDrone.DropAmmo.performed -= DropAmmoPerformed;
            m_inputSystem_Actions.KamikazeDrone.Reset.performed -= ResetDrone;
            m_inputSystem_Actions.KamikazeDrone.Reload.performed -= CallReloadGrenade;
            foreach(EnemyController enemyController in FindObjectsByType<EnemyController>())
            {
                enemyController.OnKilled -= EnemyKilled;
                enemyController.OnHit -= EnemyHit;
            }

        }

        void Update()
        {
            Vector2 throttleYaw = m_inputSystem_Actions.KamikazeDrone.ThrottleYaw.ReadValue<Vector2>();
            Vector2 pitchRoll = m_inputSystem_Actions.KamikazeDrone.PitchRoll.ReadValue<Vector2>();
            m_PitchInput = pitchRoll.y;
            m_RollInput = pitchRoll.x;
            m_ThrottleInput = Mathf.MoveTowards(m_ThrottleInput, throttleYaw.y, Time.deltaTime * m_ThrottleLerpSpeed);
            m_YawInput = Mathf.MoveTowards(m_YawInput, throttleYaw.x, Time.deltaTime * m_YawLerpSpeed);
            HandleCameraRotation();
            RotatePropellers();
            Vector3 horizontalVector = new Vector3(m_Rigidbody.linearVelocity.x, 0, m_Rigidbody.linearVelocity.z);
            float groundSpeed = horizontalVector.magnitude;
            CalculateBattery();
            fpvCanvasUIOrganizer.UpdateDroneHudText(GetAltitude(), m_Rigidbody.linearVelocity.y, groundSpeed, -m_CameraRotationX, m_CurrentBattery, m_CurrentAmmo, m_MaxAmmo, m_lifeCount);
        }

        void FixedUpdate()
        {
            ApplyFlightPhysics();
            LimitVelocity();
        }
        
        // Add this to KamikazeDroneController.cs
        private void OnCollisionEnter(Collision collision)
        {
            // If we are carrying a grenade and hit something hard enough
            if (m_GrenadeObj != null && collision.relativeVelocity.magnitude > m_ExplosionCollisionVelocity)
            {
                // 1. Trigger the grenade explosion at the point of impact
                m_GrenadeController.Explode(collision.contacts[0].point);                
                if(collision.gameObject.layer == m_EnemyLayerMask && collision.gameObject.TryGetComponent<EnemyController>(out EnemyController enemyController))
                {
                    enemyController.TakeDamage(m_GrenadeController);
                }
                // 2. Clear references so we don't trigger it twice
                m_GrenadeObj = null;
                m_GrenadeController = null;

                // 3. Handle the "Death" of the drone (e.g., reset to start)
                // We pass a default context because the original ResetDrone expects one
                ResetDrone(default);
            }
        }
        private void EnemyHit(EnemyController enemyController)
        {
            string text = $"{enemyController.EnemyType.ToString()} Hit!";
            fpvCanvasUIOrganizer.UpdateDroneAlertText(text, Color.green);
        }

        private void EnemyKilled(EnemyController enemyController)
        {
            string text = $"{enemyController.EnemyType.ToString()} is Dead!";
            fpvCanvasUIOrganizer.UpdateDroneAlertText(text, Color.green);
        }
        // 2. The Reset Function
        public void ResetDrone(InputAction.CallbackContext context)
        {
            m_lifeCount++;

            // Stop all movement immediately
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;

            // Reset Transform
            transform.position = m_StartPosition;
            transform.rotation = m_StartRotation;
            m_CameraRotationX = 0f;

            // Reset Inputs and Battery
            m_ThrottleInput = 0;
            m_YawInput = 0;
            m_CurrentBattery = m_MaxBattery;
            m_CurrentAmmo = m_MaxAmmo;
            // Reset Ammo (Grenade)
            ReloadGrenade();
        }

        private void CallReloadGrenade(InputAction.CallbackContext context)
        {
            ReloadGrenade();
        }

        private void ReloadGrenade()
        {
            // If a grenade already exists (attached or dropped), destroy it to avoid clutter
            if (m_GrenadeObj != null || m_CurrentAmmo == 0) 
            {
                return;
            }
            
            // Spawn a fresh grenade
            m_GrenadeObj = Instantiate(m_GrenadePrefab);
            m_GrenadeController = m_GrenadeObj.GetComponent<GrenadeController>();

            // Reset Drone Mass (add back the grenade weight)
            m_Rigidbody.mass = 1f; // Set this to your drone's base mass
            m_Rigidbody.mass += m_GrenadeController.Mass;

            // Attach to holder
            m_GrenadeObj.transform.parent = m_GrenadeHolder;
            m_GrenadeObj.transform.localPosition = Vector3.zero;
            m_GrenadeObj.transform.localEulerAngles = new Vector3(90, 0, 0);

            // Reset Claws to closed position
            m_LeftClaw.localRotation = Quaternion.Euler(Vector3.zero);
            m_RightClaw.localRotation = Quaternion.Euler(Vector3.zero);
        }    

        public float GetAltitude()
        {
            RaycastHit hit;
            // Cast a ray straight down
            if (Physics.Raycast(transform.position, Vector3.down, out hit))
            {
                // hit.distance is the value in meters
                return hit.distance; 
            }
            return transform.position.y; // Fallback to world height
        }

        private void DropAmmoPerformed(InputAction.CallbackContext context)
        {
            // Check if we actually have a grenade to drop
            if (m_GrenadeObj == null) return;

            // 1. Unparent the grenade so it stays in the world as the drone flies away
            m_GrenadeObj.transform.SetParent(null);

            // 2. Add a Rigidbody to make it fall
            // We check if it already has one just in case to prevent errors
            if (!m_GrenadeObj.GetComponent<Rigidbody>())
            {
                Rigidbody rb = m_GrenadeObj.AddComponent<Rigidbody>();
                
                // Optional: Inherit the drone's current velocity so the grenade 
                // doesn't just drop straight down like a rock while moving fast.
                rb.linearVelocity = m_Rigidbody.linearVelocity;
            }

            // 3. Add a Collider so it hits things
            if (!m_GrenadeObj.GetComponent<Collider>())
            {
                // Using SphereCollider as a default for grenades, 
                // but you could use BoxCollider or MeshCollider
                m_GrenadeObj.AddComponent<BoxCollider>();
            }
            m_Rigidbody.mass -= m_GrenadeController.Mass;
            
            StartCoroutine(AnimateClawsOpen());
            m_CurrentAmmo--;
            // 4. Clear the reference so we don't try to drop it again
            m_GrenadeObj = null;
            m_GrenadeController = null;
        }

        private IEnumerator AnimateClawsOpen()
        {
            float duration = .2f;
            float elapsed = 0f;

            // Store starting local Euler angles
            Vector3 leftStartEuler = m_LeftClaw.localEulerAngles;
            Vector3 rightStartEuler = m_RightClaw.localEulerAngles;

            // Calculate target Euler angles
            Vector3 leftTargetEuler = leftStartEuler + new Vector3(0, 0, -45f);
            Vector3 rightTargetEuler = rightStartEuler + new Vector3(0, 0, 45f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;

                // Use Lerp for the angles
                m_LeftClaw.localRotation = Quaternion.Euler(Vector3.Lerp(leftStartEuler, leftTargetEuler, percent));
                m_RightClaw.localRotation = Quaternion.Euler(Vector3.Lerp(rightStartEuler, rightTargetEuler, percent));

                yield return null;
            }

            m_LeftClaw.localRotation = Quaternion.Euler(leftTargetEuler);
            m_RightClaw.localRotation = Quaternion.Euler(rightTargetEuler);
        }
        // Update is called once per frame

        private void HandleCameraRotation()
        {
            float m_CameraInput = m_inputSystem_Actions.KamikazeDrone.CameraTilt.ReadValue<float>();
            m_CameraRotationX += m_CameraInput * m_CameraTiltSpeed * Time.deltaTime;
            m_CameraRotationX = Mathf.Clamp(m_CameraRotationX, m_MinTilt, m_MaxTilt);
            m_Camera.localEulerAngles = new Vector3(m_CameraRotationX, 0, 0);

        }

        private void CalculateBattery()
        {
            if (m_CurrentBattery <= 0)
            {
                m_CurrentBattery = 0;
                // Optional: Kill motors here if you want a "dead stick" landing
                return;
            }

            // Base drain + Throttle intensity
            float workLoad = Mathf.Abs(m_ThrottleInput);
            
            // Add rotational strain (Yaw, Pitch, Roll)
            workLoad += (Mathf.Abs(m_YawInput) + Mathf.Abs(m_PitchInput) + Mathf.Abs(m_RollInput)) * 0.2f;

            // Factor in Mass: Heavier drones require more energy to move the same amount
            // We use mass as a direct multiplier to the workload
            float massFactor = m_Rigidbody.mass;

            float totalDrain = (m_BaseDrainRate + (workLoad * massFactor)) * m_DrainEfficiency;

            m_CurrentBattery -= totalDrain * Time.deltaTime;
        }

        private void ApplyFlightPhysics()
        {
            float extraForce = 0f;
            if(m_ThrottleInput > .01f)
            {
                // The floor: Counteract gravity so the drone feels weightless while throttled.
                extraForce = Physics.gravity.magnitude;
            }

            float totalThrottle = (m_ThrottleInput * m_ThrottleMultiplier) + extraForce;
            m_Rigidbody.AddRelativeForce(Vector3.up * totalThrottle, ForceMode.Acceleration);

            Vector3 torque = new Vector3(
                -m_PitchInput * m_PitchStrength, 
                m_YawInput * m_YawStrength,
                -m_RollInput*m_RollStrength
            );
            m_Rigidbody.AddRelativeTorque(torque, ForceMode.Acceleration);
        }

        private void LimitVelocity()
        {
            // The Ceiling: Clamp the velocity so it doesn't accelerate forever
            Vector3 vel = m_Rigidbody.linearVelocity;

            // Limit the upward/downward speed specifically
            vel.y = Mathf.Clamp(vel.y, m_MinVerticalSpeed, m_MaxVerticalSpeed);

            m_Rigidbody.linearVelocity = vel;
        }    
        private void RotatePropellers()
        {
            // Ensure the speed is always positive so they don't spin backward
            float currentSpinSpeed = (Mathf.Abs(m_ThrottleInput) * m_MaxPropellerSpeed) + 200f;

            for (int i = 0; i < m_Propellers.Length; i++)
            {
                Transform prop = m_Propellers[i];
                float direction = (i % 2 == 0) ? 1f : -1f;

                // Change Vector3.up to whichever axis points 'out' of the motor in your model
                prop.Rotate(Vector3.up * currentSpinSpeed * direction * Time.deltaTime, Space.Self);
            }
        }
    }
}