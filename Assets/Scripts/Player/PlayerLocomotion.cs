using UnityEngine;

/// <summary>
/// Locomoción en primera persona: movimiento suavizado por Rigidbody,
/// salto y mirada (yaw en el cuerpo, pitch en el cameraHolder).
/// Sin red propia: el dueño mueve en local y los remotos se replican
/// por ClientNetworkTransform. La orquesta PlayerController.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLocomotion : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private BoxCollider groundCheck;
    [SerializeField] public bool grounded = true;
    private Vector3 smoothMoveVelocity;
    [SerializeField] private float sprintSpeed = 10f, walkSpeed = 6f, jumpForce = 300f, smoothTime = 0.1f;
    [SerializeField] private Vector3 moveAmount;

    [Header("Look")]
    [SerializeField] public Transform cameraHolder;
    [SerializeField] public float mouseSensitivity = 2f;

    [HideInInspector] public float yLookRotation;
    [HideInInspector] public float xLookRotation;
    [HideInInspector] public float zRotation = 5f;

    public Rigidbody Rb { get; private set; }
    public bool IsRunning { get; set; }

    private Camera playerCamera;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        // Sensibilidad guardada en OPCIÓN (si no hay, se usa la del prefab)
        float sens = PlayerPrefs.GetFloat("opcion_sensibilidad", -1f);
        if (sens > 0f) mouseSensitivity = sens;
    }

    /// <summary>Input + mirada. Lo llama PlayerController cuando es local.</summary>
    public void TickLocal()
    {
        Move();
        Jump();
        Look();
    }

    /// <summary>Física. Lo llama PlayerController.FixedUpdate cuando es local.</summary>
    public void TickFixed()
    {
        if (Rb == null) return;
        Rb.MovePosition(Rb.position + transform.TransformDirection(moveAmount) * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Frena en seco: cero input acumulado para que el cuerpo no deslice
    /// con la última velocidad al abrir un modal o entrar en countdown.
    /// </summary>
    public void Detener()
    {
        moveAmount = Vector3.zero;
        smoothMoveVelocity = Vector3.zero;
        IsRunning = false;
        if (Rb != null)
            Rb.linearVelocity = new Vector3(0f, Rb.linearVelocity.y, 0f);
    }

    private void Move()
    {
        IsRunning = Input.GetKey(KeyCode.LeftShift);
        Vector3 moveDir = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;

        float targetSpeed = grounded ? (IsRunning ? sprintSpeed : walkSpeed) : (IsRunning ? sprintSpeed * 0.8f : walkSpeed * 0.8f);
        moveAmount = Vector3.SmoothDamp(moveAmount, moveDir * targetSpeed, ref smoothMoveVelocity, smoothTime);
    }

    private void Jump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            if (Rb != null) Rb.AddForce(transform.up * jumpForce);
        }
    }

    public void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        yLookRotation += Input.GetAxis("Mouse X") * mouseSensitivity;
        xLookRotation -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        xLookRotation = Mathf.Clamp(xLookRotation, -85f, 85f);

        transform.rotation = Quaternion.Euler(0, yLookRotation, 0);
        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(xLookRotation, 0, 0);
        }
    }

    /// <summary>Patada de cámara al disparar (datos del RecoilProfile).</summary>
    public void AplicarRetrocesoCamara(float pitchDown, float yawDelta)
    {
        xLookRotation = Mathf.Clamp(xLookRotation - pitchDown, -85f, 85f);
        yLookRotation += yawDelta;
        transform.rotation = Quaternion.Euler(0, yLookRotation, 0);
        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(xLookRotation, 0, 0);
        }
    }

    /// <summary>Reposiciona cuerpo, velocidades y mirada (respawn).</summary>
    public void Teleport(Vector3 posicion)
    {
        transform.position = posicion;
        if (Rb != null)
        {
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
        }
        yLookRotation = posicion.x * 10f % 360f;
        xLookRotation = 0f;
        transform.rotation = Quaternion.Euler(0, yLookRotation, 0);
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(0, 0, 0);
    }

    public void SetCuerpoCinematico(bool cinematico)
    {
        if (Rb != null) Rb.isKinematic = cinematico;
    }

    public void SetGroundedState(bool _grounded)
    {
        grounded = _grounded;
    }

    public Camera ObtenerCamara()
    {
        if (playerCamera != null) return playerCamera;

        if (cameraHolder != null)
        {
            playerCamera = cameraHolder.GetComponent<Camera>();
            if (playerCamera == null)
                playerCamera = cameraHolder.GetComponentInChildren<Camera>(true);
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        return playerCamera;
    }
}
