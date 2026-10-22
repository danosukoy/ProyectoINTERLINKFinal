using System.Collections;
using UnityEngine;

public class PlayerSmoothMovement : MonoBehaviour
{
    public PlayerController mover;
    public Transform arms;

    [Header("Sway (Legacy fallback - usado si no hay perfil)")]
    public float step = 0.01f;
    public float maxStepDistance = 0.06f;
    Vector3 swayPos;
    private Vector3 swayPosVel;

    [Header("Sway Rotation")]
    public float rotationStep = 4f;
    public float maxRotationStep = 5f;
    Vector3 swayEulerRot;
    private Vector3 swayRotVel;

    public float smooth = 10f;
    float smoothRot = 12f;

    [Header("Bobbing (Legacy fallback)")]
    public float speedCurve;
    float curveSin {get => Mathf.Sin(speedCurve);}
    float curveCos {get => Mathf.Cos(speedCurve);}

    public Vector3 travelLimit = Vector3.one * 0.025f;
    public Vector3 bobLimit = Vector3.one * 0.01f;
    Vector3 bobPosition;

    public float bobExaggeration;

    [Header("Bob Rotation")]
    public Vector3 multiplier;
    Vector3 bobEulerRotation;

    [Header("SwayBob Perfil")]
    [SerializeField] private SwayBobProfile fallbackSwayBob;
    private Vector3 baseCameraPos;
    private bool baseCameraCached = false;

    [Header("Recoil (Profesional)")]
    [SerializeField] private RecoilProfile fallbackProfile;
    private RecoilProfile activeProfile;
    private float recoilT = 1f;
    private Vector3 activeKickPos;
    private Vector3 activeKickRot;
    private Vector3 recoilPos;
    private Vector3 recoilRot;
    private int patternIndex = 0;
    private float lastFireTime = -10f;
    private Vector3 legacyMaxPos = new Vector3(0f, 0.015f, -0.06f);
    private Vector3 legacyMaxRot = new Vector3(-8f, 2f, 0f);
    private float legacyDuration = 0.14f;
    
    [Header("ArmsRotation")]
    [SerializeField] private Vector3 downRotation = new Vector3(70f, 0f, 0f);
    [SerializeField] private Vector3 upRotation = Vector3.zero;
    
    private int selectedWeapon = 0;
    public bool isSwitching = false;
    private Rigidbody cachedRb;

    private void Awake()
    {
        if (mover == null)
        {
            mover = GetComponentInParent<PlayerController>();
            if (mover == null)
                mover = GetComponent<PlayerController>();
        }
        cachedRb = mover != null ? mover.GetComponent<Rigidbody>() : GetComponentInParent<Rigidbody>();
    }

    void Start()
    {
        ResetToWeaponBase();
        if (fallbackProfile == null)
        {
            fallbackProfile = ScriptableObject.CreateInstance<RecoilProfile>();
            fallbackProfile.kickPosition = legacyMaxPos;
            fallbackProfile.kickRotation = legacyMaxRot;
            fallbackProfile.duration = legacyDuration;
        }
        if (fallbackSwayBob == null)
        {
            fallbackSwayBob = ScriptableObject.CreateInstance<SwayBobProfile>();
            fallbackSwayBob.step = step;
            fallbackSwayBob.maxStepDistance = maxStepDistance;
            fallbackSwayBob.rotationStep = rotationStep;
            fallbackSwayBob.maxRotationStep = maxRotationStep;
            fallbackSwayBob.smooth = smooth;
            fallbackSwayBob.smoothRot = smoothRot;
            fallbackSwayBob.travelLimit = travelLimit;
            fallbackSwayBob.bobLimit = bobLimit;
            fallbackSwayBob.bobExaggeration = bobExaggeration;
            fallbackSwayBob.bobRotationMultiplier = multiplier;
        }
        if (mover != null && mover.cameraHolder != null && !baseCameraCached)
        {
            baseCameraPos = mover.cameraHolder.localPosition;
            baseCameraCached = true;
        }
    }

    SwayBobProfile GetActiveSwayBob()
    {
        if (mover != null && mover.currentWeapon != null && mover.currentWeapon.swayBobProfile != null)
            return mover.currentWeapon.swayBobProfile;
        return fallbackSwayBob;
    }

    public void ResetToWeaponBase()
    {
        if (mover != null && mover.currentWeapon != null)
        {
            currentSwayBobPos = mover.currentWeapon.weaponPosition;
            currentSwayBobRot = (mover.currentWeapon.weaponRotation.w == 0 && mover.currentWeapon.weaponRotation.x == 0 && mover.currentWeapon.weaponRotation.y == 0 && mover.currentWeapon.weaponRotation.z == 0)
                ? Quaternion.identity
                : mover.currentWeapon.weaponRotation;
        }
    }

    void Update()
    {
        bool isLocal = (mover == null || mover.IsLocalPlayer);
        // Modal o cuenta atrás: el viewmodel reposa (el Lerp de
        // CompositePositionRotation suaviza la vuelta a base).
        bool bloqueado = mover != null && mover.EntradaDeJuegoBloqueada();

        if (isLocal && !bloqueado)
        {
            GetInput();
            Sway();
            SwayRotation();
            BobOffset();
            BobRotation();
            ApplyHeadBob();
            SwitchWeapon();
        }
        else
        {
            walkInput = Vector2.zero;
            lookInput = Vector2.zero;
            swayPos = Vector3.zero;
            swayEulerRot = Vector3.zero;
            bobPosition = Vector3.zero;
            bobEulerRotation = Vector3.zero;
            if (isLocal)
                headBobOffset = Vector3.Lerp(headBobOffset, Vector3.zero, Time.deltaTime * 6f);
        }

        RecoilForce();
        CompositePositionRotation();
    }

    Vector2 walkInput;
    Vector2 lookInput;

    void GetInput(){
        walkInput.x = Input.GetAxis("Horizontal");
        walkInput.y = Input.GetAxis("Vertical");
        walkInput = walkInput.normalized;

        lookInput.x = Input.GetAxis("Mouse X");
        lookInput.y = Input.GetAxis("Mouse Y");
    }

    void Sway(){
        var p = GetActiveSwayBob();
        float s = p != null ? p.step : step;
        float max = p != null ? p.maxStepDistance : maxStepDistance;
        // Inercia por strafe: añade lag lateral basado en velocidad
        float strafe = 0f;
        if (cachedRb != null) strafe = cachedRb.linearVelocity.magnitude * 0.002f;
        else strafe = walkInput.magnitude * 0.01f;
        Vector3 target = lookInput * -s;
        target.x = Mathf.Clamp(target.x, -max, max);
        target.y = Mathf.Clamp(target.y, -max, max);
        // Suavizado spring
        float sm = p != null ? p.smooth : smooth;
        swayPos = Vector3.SmoothDamp(swayPos, target, ref swayPosVel, 1f / sm);
        // Añade strafe sway sutil
        if (p != null) swayPos.x += -walkInput.x * p.strafeSway;
    }

    void SwayRotation(){
        var p = GetActiveSwayBob();
        float rStep = p != null ? p.rotationStep : rotationStep;
        float rMax = p != null ? p.maxRotationStep : maxRotationStep;
        Vector2 target = lookInput * -rStep;
        target.x = Mathf.Clamp(target.x, -rMax, rMax);
        target.y = Mathf.Clamp(target.y, -rMax, rMax);
        Vector3 targetEuler = new Vector3(target.y, target.x, target.x);
        float smRot = p != null ? p.smoothRot : smoothRot;
        swayEulerRot = Vector3.SmoothDamp(swayEulerRot, targetEuler, ref swayRotVel, 1f / smRot);
    }

    private Vector3 currentSwayBobPos;
    private Quaternion currentSwayBobRot = Quaternion.identity;
    private Vector3 headBobOffset;

    void CompositePositionRotation()
    {
        Vector3 basePos = (mover != null && mover.currentWeapon != null) ? mover.currentWeapon.weaponPosition : Vector3.zero;
        Quaternion baseRot = (mover != null && mover.currentWeapon != null) ? mover.currentWeapon.weaponRotation : Quaternion.identity;

        Vector3 targetSwayBobPos = basePos + swayPos + bobPosition;
        Quaternion targetSwayBobRot = baseRot * Quaternion.Euler(swayEulerRot + bobEulerRotation);

        var p = GetActiveSwayBob();
        float sm = p != null ? p.smooth : smooth;
        float smR = p != null ? p.smoothRot : smoothRot;
        currentSwayBobPos = Vector3.Lerp(currentSwayBobPos, targetSwayBobPos, Time.deltaTime * sm);
        currentSwayBobRot = Quaternion.Slerp(currentSwayBobRot, targetSwayBobRot, Time.deltaTime * smR);

        transform.localPosition = currentSwayBobPos + recoilPos + headBobOffset * 0.3f;

        Quaternion recoilRotation = Quaternion.AngleAxis(recoilRot.x, Vector3.right) *
                                   Quaternion.AngleAxis(recoilRot.y, Vector3.up) *
                                   Quaternion.AngleAxis(recoilRot.z, Vector3.forward);

        transform.localRotation = currentSwayBobRot * recoilRotation;
    }

    void BobOffset(){
        var p = GetActiveSwayBob();
        float freq = p != null ? p.bobFrequency : 8f;
        float exaggeration = p != null ? p.bobExaggeration : bobExaggeration;
        Vector3 tLim = p != null ? p.travelLimit : travelLimit;
        Vector3 bLim = p != null ? p.bobLimit : bobLimit;

        // Velocity-driven: usa rb.velocity si existe, si no walkInput
        float speed = 0f;
        if (cachedRb != null) speed = new Vector2(cachedRb.linearVelocity.x, cachedRb.linearVelocity.z).magnitude;
        else speed = walkInput.magnitude * 3f;

        bool isGrounded = mover != null && mover.grounded;
        float sprintMul = (mover != null && mover.isRunning) ? (p != null ? p.sprintMultiplier : 1.4f) : 1f;
        float airMul = isGrounded ? 1f : (p != null ? p.airDamping : 0.15f);
        float moveFactor = Mathf.Clamp01(speed / 3f) * airMul;

        // Avance de curva proporcional a velocidad
        float targetFreq = freq * (isGrounded && moveFactor > 0.05f ? sprintMul : 0.6f) * Mathf.Max(moveFactor, 0.2f);
        speedCurve += Time.deltaTime * targetFreq * Mathf.Max(exaggeration, 1f);

        float groundedFactor = isGrounded ? 1f : 0f;

        bobPosition.x = -(walkInput.x * tLim.x) * sprintMul;
        bobPosition.y = (curveSin * bLim.y * moveFactor) - (Mathf.Abs(walkInput.y) * tLim.y * 0.5f);
        bobPosition.z = (curveCos * bLim.z * groundedFactor * moveFactor) - (walkInput.y * tLim.z * moveFactor);
    }

    void BobRotation(){
        var p = GetActiveSwayBob();
        Vector3 mult = p != null ? p.bobRotationMultiplier : multiplier;
        float speed = cachedRb != null ? new Vector2(cachedRb.linearVelocity.x, cachedRb.linearVelocity.z).magnitude : walkInput.magnitude * 3f;
        bool isMoving = speed > 0.15f && mover != null && mover.grounded;

        float sinValue = Mathf.Sin(2 * speedCurve);
        float cosValue = curveCos;
        float moveFactor = Mathf.Clamp01(speed / 3f);

        bobEulerRotation.x = mult.x * (isMoving ? sinValue * moveFactor : sinValue * 0.2f);
        bobEulerRotation.y = mult.y * (isMoving ? cosValue * moveFactor : cosValue * 0.15f);
        bobEulerRotation.z = mult.z * (isMoving ? cosValue * walkInput.x * moveFactor : cosValue * walkInput.x * 0.15f);
    }

    void ApplyHeadBob()
    {
        var p = GetActiveSwayBob();
        if (p == null || mover == null || mover.cameraHolder == null) return;
        if (!baseCameraCached) { baseCameraPos = mover.cameraHolder.localPosition; baseCameraCached = true; }

        float speed = cachedRb != null ? new Vector2(cachedRb.linearVelocity.x, cachedRb.linearVelocity.z).magnitude : walkInput.magnitude * 3f;
        bool isGrounded = mover.grounded;
        float moveFactor = Mathf.Clamp01(speed / 3f);
        if (!isGrounded || moveFactor < 0.05f)
        {
            headBobOffset = Vector3.Lerp(headBobOffset, Vector3.zero, Time.deltaTime * 6f);
            mover.cameraHolder.localPosition = Vector3.Lerp(mover.cameraHolder.localPosition, baseCameraPos, Time.deltaTime * 6f);
            return;
        }
        float amp = p.headBobAmplitude * moveFactor * (mover.isRunning ? p.sprintMultiplier : 1f);
        float freq = p.headBobFrequency;
        headBobOffset.y = Mathf.Sin(speedCurve * freq * 0.15f) * amp;
        headBobOffset.x = Mathf.Cos(speedCurve * freq * 0.08f) * amp * 0.6f;
        Vector3 targetCamPos = baseCameraPos + new Vector3(headBobOffset.x * 0.5f, headBobOffset.y, 0f);
        mover.cameraHolder.localPosition = Vector3.Lerp(mover.cameraHolder.localPosition, targetCamPos, Time.deltaTime * 10f);
    }

    void RecoilForce()
    {
        RecoilProfile profile = activeProfile ?? fallbackProfile;
        if (profile == null) { recoilPos = Vector3.zero; recoilRot = Vector3.zero; return; }

        float dur = profile.duration;
        if (dur <= 0.01f && mover != null && mover.currentWeapon != null)
        {
            float rateDelay = mover.currentWeapon.FireDelay;
            dur = Mathf.Clamp(rateDelay * 0.85f, 0.08f, 0.25f);
        }
        if (dur <= 0.01f) dur = 0.14f;

        if (recoilT < 1f)
        {
            recoilT += Time.deltaTime / dur;
            float t = Mathf.Clamp01(recoilT);
            float posK = profile.positionCurve != null ? profile.positionCurve.Evaluate(t) : Mathf.Sin(t * Mathf.PI);
            float rotK = profile.rotationCurve != null ? profile.rotationCurve.Evaluate(t) : Mathf.Sin(t * Mathf.PI);
            recoilPos = activeKickPos * posK;
            recoilRot = activeKickRot * rotK;
        }
        else
        {
            float rec = profile.patternRecovery > 0 ? profile.patternRecovery : 6f;
            recoilPos = Vector3.Lerp(recoilPos, Vector3.zero, Time.deltaTime * rec);
            recoilRot = Vector3.Lerp(recoilRot, Vector3.zero, Time.deltaTime * rec);
            if (recoilPos.sqrMagnitude < 0.00001f) recoilPos = Vector3.zero;
            if (recoilRot.sqrMagnitude < 0.00001f) recoilRot = Vector3.zero;
        }
    }
    
    [ContextMenu("Test Recoil")]
    public void PlayRecoilForce()
    {
        RecoilProfile profile = null;
        if (mover != null && mover.currentWeapon != null) profile = mover.currentWeapon.recoilProfile;
        if (profile == null) profile = fallbackProfile;

        if (profile == null)
        {
            activeProfile = null;
            activeKickPos = legacyMaxPos;
            activeKickRot = legacyMaxRot;
            recoilT = 0f;
            return;
        }

        activeProfile = profile;

        if (Time.time - lastFireTime > profile.patternResetTime) patternIndex = 0;
        lastFireTime = Time.time;

        Vector2 patternOff = profile.GetPatternOffset(patternIndex);
        patternIndex++;

        Vector3 basePos = profile.kickPosition;
        Vector3 baseRot = profile.kickRotation + new Vector3(patternOff.y, patternOff.x, 0f);

        if (mover != null && mover.currentWeapon != null && mover.currentWeapon.IsShotgun)
        {
            basePos *= 1.4f;
            baseRot *= 1.3f;
        }

        if (profile.additive)
        {
            activeKickPos = Vector3.ClampMagnitude(basePos, profile.maxAccumulatedPos.magnitude);
            activeKickRot = new Vector3(
                Mathf.Clamp(baseRot.x, -profile.maxAccumulatedRot.x, profile.maxAccumulatedRot.x),
                Mathf.Clamp(baseRot.y, -profile.maxAccumulatedRot.y, profile.maxAccumulatedRot.y),
                Mathf.Clamp(baseRot.z, -profile.maxAccumulatedRot.z, profile.maxAccumulatedRot.z));
        }
        else
        {
            activeKickPos = basePos;
            activeKickRot = baseRot;
        }

        recoilT = 0f;
    }

    IEnumerator MoveArmsWeapons(int index)
    {
        isSwitching = true;
        if (mover != null)
        {
            mover.StopReload();
            mover.canShoot = false;
        }
        
        Quaternion startRot = Quaternion.Euler(upRotation);
        Quaternion endRot = Quaternion.Euler(downRotation);
        
        float t = 0;
        
        float switchSpeed = (mover != null && mover.currentWeapon != null && mover.currentWeapon.speed > 0) ? mover.currentWeapon.speed : 10f;
        Transform targetArms = (arms != null) ? arms : transform;

        while (t < 2f / switchSpeed)
        {
            t += Time.deltaTime * 0.5f;
            targetArms.localRotation = Quaternion.Lerp(targetArms.localRotation, endRot, t);
            yield return null;
        }

        if (mover != null) mover.ChangeWeapon(index);
        
        t = 0;
        switchSpeed = (mover != null && mover.currentWeapon != null && mover.currentWeapon.speed > 0) ? mover.currentWeapon.speed : 10f;

        while (t < 2f / switchSpeed)
        {
            t += Time.deltaTime * 0.5f;
            targetArms.localRotation = Quaternion.Lerp(targetArms.localRotation, startRot, t);
            yield return null;
        }
        
        isSwitching = false;
        if (mover != null) mover.canShoot = true;
    }

    void SwitchWeapon()
    {
        if (isSwitching) return;
        // Modal, cuenta atrás o tablero: ni scroll ni teclas cambian el arma
        if (BloqueoInterfaz.DisparoBloqueadoPorUI) return;
        if (mover != null && mover.EntradaDeJuegoBloqueada()) return;

        int previous = selectedWeapon;
        int count = (mover != null && mover.Weapons != null && mover.Weapons.Count > 0) ? mover.Weapons.Count : transform.childCount;
        if (count <= 0) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            selectedWeapon = (selectedWeapon + 1) % count;
        }
        if (scroll < 0f)
        {
            selectedWeapon--;
            if (selectedWeapon < 0)
                selectedWeapon = count - 1;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) && count > 0) selectedWeapon = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2) && count > 1) selectedWeapon = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3) && count > 2) selectedWeapon = 2;

        if (previous != selectedWeapon)
        {
            StartCoroutine(MoveArmsWeapons(selectedWeapon));
        }
    }

}
