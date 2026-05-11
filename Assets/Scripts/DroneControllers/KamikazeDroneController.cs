using UnityEngine;
using UnityEngine.InputSystem;

public class KamikazeDroneController : MonoBehaviour
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
    [SerializeField] private float m_MaxVerticalSpeed = 15f;
    [SerializeField] private float m_YawStrength = 5f;
    [SerializeField] private float m_PitchStrength = 10f;
    [SerializeField] private float m_RollStrength = 10f;
    [Header("Smoothing")]
    [SerializeField] private float m_ThrottleLerpSpeed = 2f;
    [SerializeField] private float m_YawLerpSpeed = 4f;
    [Header("Grenade")]
    [SerializeField] private GameObject m_GrenadePrefab;
    [SerializeField] private Transform m_GrenadeHolder;
    private InputSystem_Actions m_inputSystem_Actions;
    private Rigidbody m_Rigidbody;
    private GameObject m_GrenadeObj;
    // input storage
    private float m_ThrottleInput;
    private float m_YawInput;
    private float m_PitchInput;
    private float m_RollInput;
    private float m_CameraRotationX = 0f; // Track total rotation
    
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        m_inputSystem_Actions = new InputSystem_Actions();
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Rigidbody.angularDamping = 2f;
        m_Rigidbody.linearDamping = 1f; // helps buttress against falling too fast
        m_GrenadeObj = Instantiate(m_GrenadePrefab);
        m_GrenadeObj.transform.parent = m_GrenadeHolder;
        m_GrenadeObj.transform.localPosition =Vector3.zero;
        m_GrenadeObj.transform.localEulerAngles = new Vector3(90, 0, 0);

    }
    void OnEnable()
    {
        m_inputSystem_Actions.KamikazeDrone.Enable();
        m_inputSystem_Actions.KamikazeDrone.DropAmmo.performed += DropAmmoPerformed;
    }

    void OnDisable()
    {
        m_inputSystem_Actions.KamikazeDrone.Disable();
        m_inputSystem_Actions.KamikazeDrone.DropAmmo.performed -= DropAmmoPerformed;
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

        // 4. Clear the reference so we don't try to drop it again
        m_GrenadeObj = null;
    }

    // Update is called once per frame
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
        Debug.Log($"Throttle input: {m_ThrottleInput}");
        
    }
    void FixedUpdate()
    {
        ApplyFlightPhysics();
        LimitVelocity();
    }

    private void HandleCameraRotation()
    {
        float m_CameraInput = m_inputSystem_Actions.KamikazeDrone.CameraTilt.ReadValue<float>();
        m_CameraRotationX += m_CameraInput * m_CameraTiltSpeed * Time.deltaTime;
        m_CameraRotationX = Mathf.Clamp(m_CameraRotationX, m_MinTilt, m_MaxTilt);
        m_Camera.localEulerAngles = new Vector3(m_CameraRotationX, 0, 0);

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
        vel.y = Mathf.Clamp(vel.y, -m_MaxVerticalSpeed, m_MaxVerticalSpeed);

        m_Rigidbody.linearVelocity = vel;
    }    
    private void RotatePropellers()
    {
        // Map throttle (0 to 1) to a rotation speed, with a small base spin for idling
        float currentSpinSpeed = ((m_ThrottleInput+Mathf.Abs(m_YawInput*.33f)) * m_MaxPropellerSpeed) + 200f;

        foreach (Transform prop in m_Propellers)
        {
            // Multiplied by Time.deltaTime for frame-rate independence
            prop.Rotate(Vector3.up * currentSpinSpeed * Time.deltaTime);
        }
    }
}
