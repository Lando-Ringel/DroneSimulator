using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using DroneSimulator.UI;
using DroneSimulator.AttackControllers;
using DroneSimulator.EnemyControllers;
using DroneSimulator.SceneManagers;
using System.Collections.Generic;

namespace DroneSimulator.DroneControllers
{
    public class KamikazeDroneController : PlayerDroneController
    {
        [Header("Visuals")]
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
        [SerializeField] private float m_AcroAngularDampening = 2f;
        [SerializeField] private float m_AcroLinearDampening = 1f;
        [Header("Angle Settings")]
        [SerializeField] private float m_MaxAngleTilt = 35f;
        [SerializeField] private float m_AngleResponsiveness = 5f;
        [SerializeField] private float m_SelfLevelStrength = 3f;
        [SerializeField] private float m_MoveForce = 5f;
        [SerializeField] private float m_AngleAngularDampening = 8f;
        [SerializeField] private float m_AngleLinearDampening = 2f;
        [SerializeField] private float m_AngleThrottleMult = 1.45f;
        [SerializeField] private float m_AngleYawStrengthMult = 2.1f;

        [Header("Battery Settings")]
        [SerializeField] private float m_BaseDrainRate = 0.5f;
        [SerializeField] private float m_DrainEfficiency = 1.0f;
        private float m_MaxBattery = 100f;
        private float m_CurrentBattery = 100f;
        [Header("Smoothing")]
        [SerializeField] private float m_ThrottleLerpSpeed = 2f;
        [SerializeField] private float m_YawLerpSpeed = 4f;
        [Header("Grenade")]
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
        [SerializeField] private FPVCanvasUIOrganizer m_FPVCanvasUIOrganizer;
        private Vector3 m_StartPosition;
        private Quaternion m_StartRotation;
        // input storage
        private float m_ThrottleInput;
        private float m_YawInput;
        private float m_PitchInput;
        private float m_RollInput;
        private float m_CameraRotationX = 0f; // Track total rotation
        private InputAction currentInputAction;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            m_StartPosition = transform.position;
            m_StartRotation = transform.rotation;
            m_EnemyLayerMask = LayerMask.NameToLayer("Enemy");

            m_Rigidbody = GetComponent<Rigidbody>();
            // m_Rigidbody.angularDamping = 2f;
            // m_Rigidbody.linearDamping = 1f; // helps buttress against falling too fast
            
            LoadAmmo();
        }

        public override void InitializePlayerController(InputSystem_Actions inputSystem_Actions)
        {
            m_IsInitialized = true;
            m_inputSystem_Actions = inputSystem_Actions;
            m_inputSystem_Actions.DroneInput.Enable();
            m_Rigidbody.isKinematic = false;
            m_inputSystem_Actions.DroneInput.DropAmmo.performed += DropAmmoPerformed;
            m_inputSystem_Actions.DroneInput.Reset.performed += CallResetDrone;
            m_inputSystem_Actions.DroneInput.Reload.performed += CallReloadGrenade;
            m_inputSystem_Actions.DroneInput.ToggleFlightMode.performed += ToggleFlightMode;
            SetFlightMode(FlightMode.Angle);
        }



        void Update()
        {
            if(m_IsDead || m_IsPaused)
                return;
            Vector2 throttleYaw = m_inputSystem_Actions.DroneInput.ThrottleYaw.ReadValue<Vector2>();
            GetFlightMode();

            // pitchRoll = m_inputSystem_Actions.DroneInput.PitchRoll.ReadValue<Vector2>();
            m_ThrottleInput = Mathf.MoveTowards(m_ThrottleInput, throttleYaw.y, Time.deltaTime * m_ThrottleLerpSpeed);
            m_YawInput = Mathf.MoveTowards(m_YawInput, throttleYaw.x, Time.deltaTime * m_YawLerpSpeed);
            HandleCameraRotation();
            
            Vector3 horizontalVector = new Vector3(m_Rigidbody.linearVelocity.x, 0, m_Rigidbody.linearVelocity.z);
            float groundSpeed = horizontalVector.magnitude;
            CalculateBattery();
            m_FPVCanvasUIOrganizer.UpdateDroneHudText(GetAltitude(), m_Rigidbody.linearVelocity.y, groundSpeed, -m_CameraRotationX, m_CurrentBattery, m_CurrentAmmo, m_MaxAmmo, m_lifeCount);
            // Record current state
            RecordState();
        }

        void FixedUpdate()
        {
            if(m_IsDead || m_IsPaused)
                return;

            switch(m_FlightMode)
            {
                case FlightMode.Acro:
                    ApplyAcroPhysics();
                    RotateAcroPropellers();
                    break;

                case FlightMode.Angle:
                    ApplyAnglePhysics();
                    RotateAnglePropellers();
                    break;
            }            
            LimitVelocity();
        }
        
        // Add this to KamikazeDroneController.cs
        private void OnCollisionEnter(Collision collision)
        {
            if (!m_IsPaused && collision.relativeVelocity.magnitude > m_ExplosionCollisionVelocity)
            {
                // 1. Trigger the grenade explosion at the point of impact
                if(collision.gameObject.layer == m_EnemyLayerMask && collision.gameObject.TryGetComponent<EnemyController>(out EnemyController enemyController)
                && !enemyController.m_IsDead)
                {
                    RaiseKilled(enemyController);
                }
                else
                {
                    RaiseKilled(null);
                }
            }
        }
        public override void PauseController()
        {
            if(m_IsPaused)
            {
                m_IsPaused = false;
                m_inputSystem_Actions.DroneInput.Enable();
            }
            else
            {
                m_IsPaused = true;
                m_inputSystem_Actions.DroneInput.Disable();
                
            }
        }
        public override void EnemyHit(EnemyController enemyController)
        {
            string text = $"{enemyController.EnemyType.ToString()} Hit!";
            m_FPVCanvasUIOrganizer.UpdateDroneAlertText(text, Color.green);
        }

        public override void EnemyKilled(EnemyController enemyController)
        {
            string text = $"{enemyController.EnemyType.ToString()} is Dead!";
            m_FPVCanvasUIOrganizer.UpdateDroneAlertText(text, Color.green);
        }
        private void CallResetDrone(InputAction.CallbackContext context)
        {
            ResetDrone();
        }

        // 2. The Reset Function
        public override void ResetDrone()
        {
            m_lifeCount++;
            // Stop all movement immediately
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            m_Rigidbody.isKinematic = false;
            // Reset Transform
            transform.position = m_StartPosition;
            transform.rotation = m_StartRotation;
            m_CameraRotationX = 0f;
            m_Model3D.SetActive(true);
            // Reset Inputs and Battery
            LoadAmmo();
        }
        private void ToggleFlightMode(InputAction.CallbackContext context)
        {
            if(m_FlightMode == FlightMode.Acro)
            {
                SetFlightMode(FlightMode.Angle);
            }
            else
            {
                SetFlightMode(FlightMode.Acro);
            }
        }        
        private void SetFlightMode(FlightMode newMode)
        {
            m_FlightMode = newMode;
            switch(m_FlightMode)
            {
                case FlightMode.Acro:

                    m_Rigidbody.angularDamping = m_AcroAngularDampening;
                    m_Rigidbody.linearDamping = m_AcroLinearDampening;        
                    m_FPVCanvasUIOrganizer.UpdateDroneAlertText(
                        "ACRO MODE",
                        Color.red
                    );
                    break;

                case FlightMode.Angle:

                    m_Rigidbody.angularDamping = m_AngleAngularDampening;
                    m_Rigidbody.linearDamping = m_AngleLinearDampening;                    
                    m_FPVCanvasUIOrganizer.UpdateDroneAlertText(
                        "ANGLE MODE",
                        Color.cyan
                    );

                break;
            }
        }
        private Vector2 GetFlightMode()
        {
            Vector2 pitchRoll = Vector2.zero;
            switch(m_FlightMode)
            {
                case FlightMode.Acro:
                    pitchRoll = m_inputSystem_Actions.DroneInput.PitchRollMouse.ReadValue<Vector2>();
                    m_PitchInput = pitchRoll.y;
                    m_RollInput = pitchRoll.x;

                    break;

                case FlightMode.Angle:
                    pitchRoll = m_inputSystem_Actions.DroneInput.PitchRollKeyboard.ReadValue<Vector2>();
                    m_PitchInput = pitchRoll.y;
                    m_RollInput = pitchRoll.x;
                    break;
            }    
            return pitchRoll;
        }        


        public override void LoadAmmo(string alertText = "")
        {
            m_ThrottleInput = 0;
            m_YawInput = 0;
            m_CurrentBattery = m_MaxBattery;
            if(m_CurrentAmmo != m_MaxAmmo)
            {
                m_CurrentAmmo = m_MaxAmmo;
                ReloadGrenade();
                m_FPVCanvasUIOrganizer.UpdateDroneAlertText(alertText, Color.green);
            }
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
            m_GrenadeObj = Instantiate(m_AttackPrefab);
            m_GrenadeController = m_GrenadeObj.GetComponent<GrenadeController>();

            // Reset Drone Mass (add back the grenade weight)
            m_Rigidbody.mass = 1f; // Set this to your drone's base mass
            m_Rigidbody.mass += m_GrenadeController.Mass;

            // Attach to holder
            m_GrenadeObj.transform.parent = m_GrenadeHolder;
            m_GrenadeObj.transform.localPosition = Vector3.zero;
            m_GrenadeObj.transform.localEulerAngles = new Vector3(0, 0, 0);

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
            float m_CameraInput = m_inputSystem_Actions.DroneInput.CameraTilt.ReadValue<float>();
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

        private void ApplyAcroPhysics()
        {
            float extraForce = 0f;
            if(m_ThrottleInput > .01f)
            {
                extraForce = Physics.gravity.magnitude;
            }
            float totalThrottle = (m_ThrottleInput * m_ThrottleMultiplier) + extraForce;
            m_Rigidbody.AddRelativeForce(
                Vector3.up * totalThrottle,
                ForceMode.Acceleration
            );
            Vector3 torque = new Vector3(
                -m_PitchInput * m_PitchStrength,
                m_YawInput * m_YawStrength,
                -m_RollInput * m_RollStrength
            );
            m_Rigidbody.AddRelativeTorque(
                torque,
                ForceMode.Acceleration
            );
        }

        private void ApplyAnglePhysics()
        {
            // HOVER
            float hoverForce = Physics.gravity.magnitude + (m_ThrottleInput * m_ThrottleMultiplier * m_AngleThrottleMult);
            m_Rigidbody.AddForce(
                Vector3.up * hoverForce,
                ForceMode.Acceleration
            );
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 moveDirection =
                (forward * m_PitchInput) +
                (right * m_RollInput);
            m_Rigidbody.AddForce(
                moveDirection * m_MoveForce,
                ForceMode.Acceleration
            );
            // TARGET VISUAL TILT
            float targetPitch = Mathf.Clamp(m_PitchInput, -1, 1) * m_MaxAngleTilt;
            float targetRoll = -Mathf.Clamp(m_RollInput, -1, 1) * m_MaxAngleTilt;
            Vector3 currentRotation = transform.localEulerAngles;
            float currentPitch = NormalizeAngle(currentRotation.x);
            float currentRoll = NormalizeAngle(currentRotation.z);
            float pitchError = targetPitch - currentPitch;
            float rollError = targetRoll - currentRoll;
            Vector3 torque = new Vector3(
                pitchError * m_AngleResponsiveness,
                m_YawInput * m_YawStrength*m_AngleYawStrengthMult,
                rollError * m_AngleResponsiveness
            );
            m_Rigidbody.AddRelativeTorque(
                torque,
                ForceMode.Acceleration
            );
        }

        private float NormalizeAngle(float angle)
        {
            if(angle > 180f)
                angle -= 360f;

            return angle;
        }

        private void LimitVelocity()
        {
            // The Ceiling: Clamp the velocity so it doesn't accelerate forever
            Vector3 vel = m_Rigidbody.linearVelocity;
            // Limit the upward/downward speed specifically
            vel.y = Mathf.Clamp(vel.y, m_MinVerticalSpeed, m_MaxVerticalSpeed);
            m_Rigidbody.linearVelocity = vel;
        }    

        private void RotateAcroPropellers()
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
        private void RotateAnglePropellers()
        {
            // Ensure the speed is always positive so they don't spin backward
            float propSpeed = Mathf.Abs(m_PitchInput)*.5f + Mathf.Abs(m_ThrottleInput)*.5f;
            float currentSpinSpeed = (propSpeed * m_MaxPropellerSpeed) + m_MaxPropellerSpeed/3 + 200f;
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