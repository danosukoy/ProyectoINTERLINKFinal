using UnityEngine;

/// <summary>
/// Perfil de recoil procedural por arma. Define kick de viewmodel, kick de cámara,
/// curvas, patrón y recuperación. Diseñado para ser data-driven y testeable.
/// </summary>
[CreateAssetMenu(fileName = "RecoilProfile", menuName = "Item/RecoilProfile")]
public class RecoilProfile : ScriptableObject
{
    [Header("Viewmodel Kick")]
    [Tooltip("Kick máximo de posición local del WeaponHolder (Z negativo = retroceso)")]
    public Vector3 kickPosition = new Vector3(0f, 0.015f, -0.12f);
    [Tooltip("Kick máximo de rotación (X pitch negativo = cañón sube)")]
    public Vector3 kickRotation = new Vector3(-6f, 1.5f, 0.2f);
    [Tooltip("Duración del kick (s). Se adapta si es 0: usa FireDelay*0.85 del arma")]
    public float duration = 0.14f;

    [Header("Curvas")]
    [Tooltip("Curva 0→1 de posición a lo largo del kick (0 inicio, 1 fin).")]
    public AnimationCurve positionCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 8f),
        new Keyframe(0.15f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -2f, 0f));
    [Tooltip("Curva 0→1 de rotación. Puede diferir de posición para snap más agresivo.")]
    public AnimationCurve rotationCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 10f),
        new Keyframe(0.12f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -2f, 0f));

    [Header("Camera Kick")]
    [Tooltip("Pitch de cámara por disparo (grados). Se suma a xLookRotation y decae.")]
    public float cameraPitch = 1.2f;
    [Tooltip("Yaw aleatorio de cámara por disparo")]
    public float cameraYaw = 0.35f;
    [Tooltip("Velocidad de recuperación de cámara (lerp)")]
    public float cameraRecovery = 8f;

    [Header("Pattern (acumulativo)")]
    [Tooltip("Patrón de recoil por disparo indexado. Si vacío, usa kickRotation + random.")]
    public Vector2[] pattern;
    [Tooltip("Random adicional sobre el patrón (dispersión)")]
    public float patternRandom = 0.3f;
    [Tooltip("Tiempo sin disparar para resetear índice de patrón (s)")]
    public float patternResetTime = 0.6f;
    [Tooltip("Suavizado del patrón acumulado hacia 0 cuando no se dispara")]
    public float patternRecovery = 6f;

    [Header("Stacking")]
    [Tooltip("Si true, disparos en ráfaga acumulan kick (se suma). Si false, reinicia.")]
    public bool additive = true;
    [Tooltip("Clamp máximo acumulado de posición/rotación para evitar desorbitado")]
    public Vector3 maxAccumulatedPos = new Vector3(0.08f, 0.08f, 0.25f);
    public Vector3 maxAccumulatedRot = new Vector3(12f, 6f, 4f);

#if UNITY_EDITOR
    private void OnValidate()
    {
        duration = Mathf.Clamp(duration, 0f, 0.5f);
        cameraPitch = Mathf.Clamp(cameraPitch, 0f, 5f);
        patternResetTime = Mathf.Max(0.1f, patternResetTime);
        if (pattern == null) pattern = new Vector2[0];
    }
#endif

    public Vector2 GetPatternOffset(int index, System.Random rng = null)
    {
        if (pattern != null && pattern.Length > 0)
        {
            Vector2 baseOff = pattern[index % pattern.Length];
            if (patternRandom > 0f)
            {
                float rx = (rng != null ? (float)rng.NextDouble() * 2f - 1f : Random.Range(-1f, 1f)) * patternRandom;
                float ry = (rng != null ? (float)rng.NextDouble() * 2f - 1f : Random.Range(-1f, 1f)) * patternRandom;
                baseOff += new Vector2(rx, ry);
            }
            return baseOff;
        }
        // Sin patrón: pequeño random yaw
        float yaw = Random.Range(-cameraYaw, cameraYaw);
        return new Vector2(yaw, 0f);
    }
}
