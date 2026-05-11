using UnityEngine;
using UnityEngine.InputSystem;

public class KamikazeDroneController : MonoBehaviour
{
    [Header("Flight Settings")]
    [SerializeField] private float m_ThrottleMultiplier = 20f;
    [SerializeField] private float m_MaxVerticalSpeed = 15f;
    [SerializeField] private float m_YawStrength = 5f;
    [SerializeField] private float m_PitchStrength = 10f;
    [SerializeField] private float m_RollStrength = 10f;
    private InputSystem_Actions m_inputSystem_Actions;
    private Rigidbody m_Rigidbody;
    private Animator animator;
    // input storage
    private float m_ThrottleInput;
    private float m_YawInput;
    private float m_PitchInput;
    private float m_RollInput;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        m_inputSystem_Actions = new InputSystem_Actions();
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Rigidbody.angularDamping = 2f;
        m_Rigidbody.linearDamping = 1f; // helps buttress against falling too fast
    }
    void OnEnable()
    {
        m_inputSystem_Actions.KamikazeDrone.Enable();
    }

    void OnDisable()
    {
        m_inputSystem_Actions.KamikazeDrone.Disable();
    }


    // Update is called once per frame
    void Update()
    {
        Vector2 throttleYaw = m_inputSystem_Actions.KamikazeDrone.ThrottleYaw.ReadValue<Vector2>();
        Vector2 pitchRoll = m_inputSystem_Actions.KamikazeDrone.PitchRoll.ReadValue<Vector2>();
        m_ThrottleInput = throttleYaw.y;
        m_YawInput = throttleYaw.x;
        m_PitchInput = pitchRoll.y;
        m_RollInput = pitchRoll.x;
    }
    void FixedUpdate()
    {
        ApplyFlightPhysics();
        LimitVelocity();
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

}
