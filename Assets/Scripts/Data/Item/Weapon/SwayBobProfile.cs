using UnityEngine;

/// <summary>
/// Perfil data-driven para sway (inercia de mirada) y bobbing (balanceo al caminar).
/// Permite diferenciar rifle / pistola / shotgun / melee sin tocar código.
/// </summary>
[CreateAssetMenu(fileName = "SwayBobProfile", menuName = "Item/SwayBobProfile")]
public class SwayBobProfile : ScriptableObject
{
    [Header("Sway - Inercia de mirada (viewmodel lag)")]
    [Tooltip("Escala de desplazamiento por input de ratón")]
    public float step = 0.012f;
    [Tooltip("Máximo desplazamiento sway")]
    public float maxStepDistance = 0.06f;
    [Tooltip("Escala de rotación por input")]
    public float rotationStep = 4f;
    public float maxRotationStep = 5f;
    [Tooltip("Suavizado posición/rotación (spring). Mayor = más rígido")]
    public float smooth = 10f;
    public float smoothRot = 12f;
    [Tooltip("Inercia extra al strafe (lag por velocidad lateral)")]
    public float strafeSway = 0.015f;

    [Header("Bob - Balanceo al caminar")]
    [Tooltip("Frecuencia base del paso (cuando isGrounded y se mueve)")]
    public float bobFrequency = 8f;
    [Tooltip("Exageración con velocidad (multiplica frecuencia por speed)")]
    public float bobExaggeration = 1.2f;
    [Tooltip("Amplitud de traslación del bob (x lateral, y vertical, z frontal)")]
    public Vector3 travelLimit = new Vector3(0.025f, 0.025f, 0.025f);
    [Tooltip("Amplitud de onda senoidal adicional")]
    public Vector3 bobLimit = new Vector3(0.01f, 0.01f, 0.01f);
    [Tooltip("Multiplicador de rotación de bob (pitch,yaw,roll)")]
    public Vector3 bobRotationMultiplier = new Vector3(1f, 1f, 1f);

    [Header("Head Bob (cámara)")]
    [Tooltip("Amplitud de head bob en cámara (sutil). 0 = sin head bob")]
    public float headBobAmplitude = 0.008f;
    [Tooltip("Head bob solo cuando se mueve y está en suelo")]
    public float headBobFrequency = 10f;

    [Header("Estado")]
    [Tooltip("Multiplicador cuando corre (sprint)")]
    public float sprintMultiplier = 1.4f;
    [Tooltip("Multiplicador en aire (0 = sin bob en aire)")]
    public float airDamping = 0.15f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        step = Mathf.Clamp(step, 0f, 0.05f);
        maxStepDistance = Mathf.Clamp(maxStepDistance, 0f, 0.2f);
        smooth = Mathf.Max(1f, smooth);
        smoothRot = Mathf.Max(1f, smoothRot);
        bobFrequency = Mathf.Max(1f, bobFrequency);
        sprintMultiplier = Mathf.Clamp(sprintMultiplier, 0.5f, 2f);
    }
#endif
}
