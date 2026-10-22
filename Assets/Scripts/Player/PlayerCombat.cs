using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Combate del jugador: loadout (local + red), disparo con predicción,
/// recarga, cambio de arma, viewmodel y FX. Autoridad de servidor para
/// munición y daño. La orquesta PlayerController.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCombat : NetworkBehaviour
{
    [Header("Gun Setup")]
    // Definiciones de arma por defecto; el loadout real llega de PlayerDataManager
    [SerializeField] public List<WeaponDefinition> Weapons = new List<WeaponDefinition>();
    [SerializeField] public Transform gunParent;

    // Cuchillo (slot fijo): se usa si PlayerDataManager no tiene uno asignado
    [SerializeField] private WeaponDefinition cuchilloPorDefecto;

    [Header("Red: sincronización de loadout")]
    // Registros compartidos: los índices son iguales en todos los clientes.
    [SerializeField] private WeaponRegistry weaponRegistry;
    [SerializeField] private SkillRegistry skillRegistry;

    // Loadout del jugador como índices dentro de los registros. Escribe el
    // servidor; lo leen TODOS los clientes para montar las armas (también las
    // de los jugadores remotos). El cuchillo no viaja por red: es fijo e
    // idéntico para todos, cada cliente lo añade en local.
    private NetworkList<int> loadoutRed = new NetworkList<int>();
    private NetworkList<int> skillsRed = new NetworkList<int>();

    [Header("FX (opcional)")]
    [SerializeField] private GameObject fxMuzzleFlash;
    [SerializeField] private GameObject fxImpacto;
    [SerializeField] private GameObject fxTrazadora;

    [Header("Runtime Filled")]
    public bool canShoot = true;

    // Estado runtime de las armas equipadas (una instancia por definición)
    private List<WeaponRuntime> armasRuntime = new List<WeaponRuntime>();
    private int indiceArmaLocal = 0; // índice activo cuando Netcode no está activo
    private GameObject modeloArmaActual; // viewmodel instanciado

    // Network synced active weapon index
    public NetworkVariable<int> activeWeaponIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Munición autoritativa del arma activa (solo escribe el servidor)
    public NetworkVariable<int> ammoMag = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> ammoReserva = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private PlayerController control;
    private PlayerLocomotion locomocion;
    private Coroutine reloadCoroutine;
    private bool suscritoEscena = false;

    // ---------- Acceso al arma activa ----------

    private int IndiceActivo => IsSpawned ? activeWeaponIndex.Value : indiceArmaLocal;

    public WeaponRuntime currentRuntime
    {
        get
        {
            int i = IndiceActivo;
            return (i >= 0 && i < armasRuntime.Count) ? armasRuntime[i] : null;
        }
    }

    public WeaponDefinition currentWeapon => currentRuntime != null ? currentRuntime.def : null;

    private void Awake()
    {
        control = GetComponent<PlayerController>();
        locomocion = GetComponent<PlayerLocomotion>();
    }

    private void Start()
    {
        if (!IsSpawned)
        {
            LoadAssignedWeapons();
            EquipWeaponVisual(0);
            SuscribirCambioEscena();
        }
    }

    public override void OnDestroy()
    {
        DesuscribirCambioEscena();
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        activeWeaponIndex.OnValueChanged += OnWeaponIndexChanged;
        ammoMag.OnValueChanged += OnAmmoMagChanged;
        ammoReserva.OnValueChanged += OnAmmoReservaChanged;
        loadoutRed.OnListChanged += OnLoadoutRedChanged;
        skillsRed.OnListChanged += OnSkillsRedChanged;
        SuscribirCambioEscena();

        if (IsOwner)
        {
            LoadAssignedWeapons();

            // Publica el loadout elegido en el lobby hacia el servidor
            EnviarLoadout();

            if (IsServer)
            {
                activeWeaponIndex.Value = 0;
            }
            else
            {
                RequestChangeWeaponServerRpc(0);
            }
        }

        // Spawn del arma visual para todos los clientes
        EquipWeaponVisual(activeWeaponIndex.Value);

        // El servidor publica la munición inicial autoritativa
        if (IsServer)
            SyncAmmo();
    }

    public override void OnNetworkDespawn()
    {
        activeWeaponIndex.OnValueChanged -= OnWeaponIndexChanged;
        ammoMag.OnValueChanged -= OnAmmoMagChanged;
        ammoReserva.OnValueChanged -= OnAmmoReservaChanged;
        loadoutRed.OnListChanged -= OnLoadoutRedChanged;
        skillsRed.OnListChanged -= OnSkillsRedChanged;
        DesuscribirCambioEscena();
    }

    private void SuscribirCambioEscena()
    {
        if (suscritoEscena) return;
        suscritoEscena = true;
        SceneManager.activeSceneChanged += AlCambiarEscena;
    }

    private void DesuscribirCambioEscena()
    {
        if (!suscritoEscena) return;
        suscritoEscena = false;
        SceneManager.activeSceneChanged -= AlCambiarEscena;
    }

    /// <summary>
    /// Los avatares migran de LobbyRoom a SampleScene sin re-spawnear
    /// (OnNetworkSpawn no se repite): aquí se recarga el loadout completo
    /// al entrar a partida (y se vuelve a solo-cuchillo al volver al lobby).
    /// Los datos ya están replicados (PlayerDataManager en dueño, loadoutRed
    /// en el resto), solo hay que reconstruir.
    /// </summary>
    private void AlCambiarEscena(Scene anterior, Scene nueva)
    {
        if (this == null || gameObject == null) return;

        if (IsSpawned && !IsOwner)
        {
            // Vista remota (también en el servidor): reconstruye desde la red
            ConstruirArmasDesdeRed();
            int idx = (Weapons != null && Weapons.Count > 0)
                ? Mathf.Clamp(IndiceActivo, 0, Weapons.Count - 1) : 0;
            EquipWeaponVisual(idx);
            if (IsServer) SyncAmmo();
        }
        else
        {
            // Dueño (u offline): recarga desde PlayerDataManager y re-equipa
            // la primaria (índice 0)
            LoadAssignedWeapons();
            if (IsSpawned)
            {
                if (IsServer) activeWeaponIndex.Value = 0;
                else RequestChangeWeaponServerRpc(0);
            }
            else indiceArmaLocal = 0;
            EquipWeaponVisual(0);
            if (IsServer) SyncAmmo();
            // Al volver al lobby la overview de la escena revive: se apaga
            // para no ver el avatar en 3ª persona (solo el dueño la apaga).
            if (IsSpawned) ApagarOverviewLobby();
        }
    }

    private static void ApagarOverviewLobby()
    {
        GameObject overviewCam = GameObject.Find("Overview Main Camera");
        if (overviewCam != null) overviewCam.SetActive(false);
    }

    /// <summary>Input de combate. Lo llama PlayerController cuando es local.</summary>
    public void TickLocal()
    {
        // Tablero visible (TAB): los clicks son para la UI, no disparan
        if (BloqueoInterfaz.DisparoBloqueadoPorUI) return;
        HandleWeaponSwitchInput();
        HandleShootInput();
        HandleReloadInput();
    }

    // ---------- Loadout ----------

    private bool EsSalaLobby()
    {
        string scene = SceneManager.GetActiveScene().name;
        return scene == "LobbyRoom" || scene == "Lobby";
    }

    private void LoadAssignedWeapons()
    {
        // En lobby solo cuchillo (pulido sistema multiplayer solicitado)
        if (EsSalaLobby())
        {
            WeaponDefinition defCuchilloLobby = cuchilloPorDefecto;
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.cuchillo != null)
                defCuchilloLobby = PlayerDataManager.Instance.cuchillo;

            Weapons = new List<WeaponDefinition>();
            if (defCuchilloLobby != null) Weapons.Add(defCuchilloLobby);

            armasRuntime = new List<WeaponRuntime>();
            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i] != null) armasRuntime.Add(new WeaponRuntime(Weapons[i]));
            return;
        }

        List<WeaponDefinition> loadedWeapons = new List<WeaponDefinition>();

        WeaponDefinition defCuchillo = cuchilloPorDefecto;

        if (PlayerDataManager.Instance != null)
        {
            if (PlayerDataManager.Instance.wPrimary != null)
                loadedWeapons.Add(PlayerDataManager.Instance.wPrimary);

            if (PlayerDataManager.Instance.wSecondary != null)
                loadedWeapons.Add(PlayerDataManager.Instance.wSecondary);

            if (PlayerDataManager.Instance.wExtra != null)
                loadedWeapons.Add(PlayerDataManager.Instance.wExtra);

            if (PlayerDataManager.Instance.cuchillo != null)
                defCuchillo = PlayerDataManager.Instance.cuchillo;
        }

        // Sin loadout en PlayerDataManager (o vacío): se conservan las armas por
        // defecto serializadas en el prefab. Útil para pruebas offline/solo
        // abriendo SampleScene directamente desde el editor.
        if (loadedWeapons.Count == 0 && Weapons != null)
        {
            for (int i = 0; i < Weapons.Count; i++)
            {
                WeaponDefinition def = Weapons[i];
                if (def != null && !loadedWeapons.Contains(def))
                    loadedWeapons.Add(def);
            }
        }

        // Cuchillo: slot fijo, siempre equipado y siempre al final de la lista
        if (defCuchillo != null && !loadedWeapons.Contains(defCuchillo))
            loadedWeapons.Add(defCuchillo);

        if (loadedWeapons.Count > 0)
        {
            Weapons = loadedWeapons;
        }
        // Si no hay loadout ni cuchillo se conservan las definiciones por
        // defecto serializadas en el prefab.

        // Las definiciones son inmutables: el estado vive en WeaponRuntime
        armasRuntime = new List<WeaponRuntime>();
        for (int i = 0; i < Weapons.Count; i++)
        {
            if (Weapons[i] != null)
                armasRuntime.Add(new WeaponRuntime(Weapons[i]));
        }
    }

    // ---------- Sincronización de loadout en red (fase 2) ----------

    /// <summary>Índice de un arma en el registro compartido, o -1 si no está.</summary>
    private int IndiceRegistro(WeaponDefinition def)
    {
        if (def == null || weaponRegistry == null) return -1;
        for (int i = 0; i < weaponRegistry.armas.Count; i++)
            if (weaponRegistry.armas[i] == def) return i;
        return -1;
    }

    /// <summary>Índice de una habilidad en el registro compartido, o -1 si no está.</summary>
    private int IndiceRegistroSkill(SkillInfo s)
    {
        if (s == null || skillRegistry == null) return -1;
        for (int i = 0; i < skillRegistry.habilidades.Count; i++)
            if (skillRegistry.habilidades[i] == s) return i;
        return -1;
    }

    /// <summary>
    /// El dueño envía su loadout (índices de los registros) al servidor para
    /// que el resto de clientes vea sus armas y habilidades.
    /// </summary>
    private void EnviarLoadout()
    {
        var pdm = PlayerDataManager.Instance;
        int p = pdm != null ? IndiceRegistro(pdm.wPrimary) : -1;
        int s = pdm != null ? IndiceRegistro(pdm.wSecondary) : -1;
        int e = pdm != null ? IndiceRegistro(pdm.wExtra) : -1;

        int sk0 = -1, sk1 = -1, sk2 = -1;
        if (pdm != null && pdm.skillsEquipadas != null)
        {
            if (pdm.skillsEquipadas.Length > 0) sk0 = IndiceRegistroSkill(pdm.skillsEquipadas[0]);
            if (pdm.skillsEquipadas.Length > 1) sk1 = IndiceRegistroSkill(pdm.skillsEquipadas[1]);
            if (pdm.skillsEquipadas.Length > 2) sk2 = IndiceRegistroSkill(pdm.skillsEquipadas[2]);
        }

        if (IsServer)
            AplicarLoadoutEnServidor(p, s, e, sk0, sk1, sk2);
        else
            EnviarLoadoutServerRpc(p, s, e, sk0, sk1, sk2);
    }

    [Rpc(SendTo.Server)]
    private void EnviarLoadoutServerRpc(int primary, int secondary, int extra, int sk0, int sk1, int sk2)
    {
        AplicarLoadoutEnServidor(primary, secondary, extra, sk0, sk1, sk2);
    }

    /// <summary>
    /// El servidor vuelca el loadout en las NetworkList; el cambio se propaga
    /// a todos los clientes, que reconstruyen las armas.
    /// </summary>
    private void AplicarLoadoutEnServidor(int primary, int secondary, int extra, int sk0, int sk1, int sk2)
    {
        loadoutRed.Clear();
        if (primary >= 0) loadoutRed.Add(primary);
        if (secondary >= 0) loadoutRed.Add(secondary);
        if (extra >= 0) loadoutRed.Add(extra);

        skillsRed.Clear();
        if (sk0 >= 0) skillsRed.Add(sk0);
        if (sk1 >= 0) skillsRed.Add(sk1);
        if (sk2 >= 0) skillsRed.Add(sk2);
    }

    private void OnLoadoutRedChanged(NetworkListEvent<int> changeEvent)
    {
        // Evita que la sincronización inicial vacía borre el loadout del dueño
        // antes de que su ServerRpc llegue y el servidor publique la lista real.
        if (IsOwner && loadoutRed.Count == 0) return;

        ConstruirArmasDesdeRed();
        EquipWeaponVisual(IndiceActivo);
        if (IsServer) SyncAmmo();
    }

    private void OnSkillsRedChanged(NetworkListEvent<int> changeEvent)
    {
        // Las habilidades aún no afectan al gameplay; cuando lo hagan, este es
        // el punto donde cada cliente aplica las habilidades del jugador.
    }

    /// <summary>
    /// Reconstruye la lista de armas a partir de los índices sincronizados más
    /// el cuchillo fijo local. Lo usan todos los clientes (también para ver las
    /// armas de los jugadores remotos).
    /// </summary>
    private void ConstruirArmasDesdeRed()
    {
        if (EsSalaLobby())
        {
            WeaponDefinition defCuchilloLobby = cuchilloPorDefecto;
            var pdmLobby = PlayerDataManager.Instance;
            if (pdmLobby != null && pdmLobby.cuchillo != null) defCuchilloLobby = pdmLobby.cuchillo;
            Weapons = defCuchilloLobby != null ? new List<WeaponDefinition> { defCuchilloLobby } : new List<WeaponDefinition>();
            armasRuntime = new List<WeaponRuntime>();
            for (int i = 0; i < Weapons.Count; i++) if (Weapons[i] != null) armasRuntime.Add(new WeaponRuntime(Weapons[i]));
            return;
        }

        if (weaponRegistry == null) return;

        var nuevas = new List<WeaponDefinition>();
        for (int i = 0; i < loadoutRed.Count; i++)
        {
            int idx = loadoutRed[i];
            if (idx >= 0 && idx < weaponRegistry.armas.Count && weaponRegistry.armas[idx] != null)
                nuevas.Add(weaponRegistry.armas[idx]);
        }

        // Cuchillo fijo (idéntico para todos; no viaja por red)
        var pdm = PlayerDataManager.Instance;
        WeaponDefinition defCuchillo = (pdm != null && pdm.cuchillo != null) ? pdm.cuchillo : cuchilloPorDefecto;
        if (defCuchillo != null) nuevas.Add(defCuchillo);

        if (nuevas.Count > 0) Weapons = nuevas;

        armasRuntime = new List<WeaponRuntime>();
        for (int i = 0; i < Weapons.Count; i++)
            if (Weapons[i] != null) armasRuntime.Add(new WeaponRuntime(Weapons[i]));
    }

    // ---------- Input ----------

    /// <summary>
    /// Cambio de arma con las teclas 1/2/3 según el orden del loadout
    /// (primario, secundaria, extra, cuchillo...).
    /// </summary>
    private void HandleWeaponSwitchInput()
    {
        int maxSlots = Mathf.Min(Weapons.Count, 3);
        for (int i = 0; i < maxSlots; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i != IndiceActivo)
            {
                ChangeWeapon(i);
                return;
            }
        }
    }

    // ---------- Disparo ----------

    private void HandleShootInput()
    {
        WeaponRuntime rt = currentRuntime;
        if (!canShoot || rt == null || rt.def == null) return;

        bool shootPressed = rt.def.IsAutomatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (shootPressed)
        {
            if (rt.PuedeDisparar())
            {
                Shoot();
            }
            else if (rt.currentAmmo == 0 && rt.reserveAmmo > 0 && !rt.isReloading)
            {
                // Auto-recarga al intentar disparar con el cargador vacío
                StartReload();
            }
        }
    }

    private void Shoot()
    {
        WeaponRuntime rt = currentRuntime;
        if (rt == null || rt.def == null) return;
        if (locomocion == null) return;

        Camera cam = locomocion.ObtenerCamara();
        Transform holder = locomocion.cameraHolder;
        Vector3 rayOrigin = (cam != null) ? cam.transform.position : (holder != null ? holder.position : transform.position);
        Vector3 rayDirection = (cam != null) ? cam.transform.forward : transform.forward;

        // Consume munición y cooldown locales (predicción) y obtiene el spread
        if (!rt.IntentarDisparar(out Vector3 spreadBase)) return;

        // Feedback local inmediato: pitch/yaw de cámara (data-driven) + retroceso viewmodel
        var recoilProf = rt.def.recoilProfile;
        float camPitch = recoilProf != null ? recoilProf.cameraPitch : 1.2f;
        float camYaw = recoilProf != null ? UnityEngine.Random.Range(-recoilProf.cameraYaw, recoilProf.cameraYaw) : 0f;
        locomocion.AplicarRetrocesoCamara(camPitch, camYaw);

        PlayerSmoothMovement sm = control != null ? control.GetSmoothMovement() : GetComponentInChildren<PlayerSmoothMovement>(true);
        if (sm != null)
        {
            sm.PlayRecoilForce();
        }

        SpawnMuzzleFlash();

        // Shotgun: múltiples perdigones, daño por perdigón (visual local ya; servidor replica autoritativamente)
        if (rt.def.IsShotgun)
        {
            int pellets = Mathf.Max(1, rt.def.pelletCount);
            float dmgPerPellet = rt.def.damagePerPellet ? rt.def.damage / pellets : rt.def.damage;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 pelletSpread = new Vector3(
                    UnityEngine.Random.Range(-rt.def.Spread.x, rt.def.Spread.x),
                    UnityEngine.Random.Range(-rt.def.Spread.y, rt.def.Spread.y),
                    UnityEngine.Random.Range(-rt.def.Spread.z, rt.def.Spread.z));
                if (i == 0) pelletSpread = spreadBase;
                Vector3 pelletDir = (rayDirection + pelletSpread).normalized;
                Vector3 fin = rayOrigin + pelletDir * rt.def.maxDistance;
                bool hitLocalPellet = Physics.Raycast(rayOrigin, pelletDir, out RaycastHit hitP, rt.def.maxDistance, rt.def.hitMask);
                if (hitLocalPellet) { fin = hitP.point; SpawnImpacto(hitP.point, hitP.normal); }
                SpawnTrazadora(rayOrigin, fin);
                if (!IsSpawned && hitLocalPellet) AplicarDanioImpacto(hitP, pelletDir, dmgPerPellet);
            }
            if (IsSpawned) PerformShotgunServerRpc(rayOrigin, rayDirection, spreadBase);
            return;
        }

        // Armas no-shotgun: un solo rayo
        Vector3 spreadDir = (rayDirection + spreadBase).normalized;
        Vector3 finTrazadora = rayOrigin + spreadDir * rt.def.maxDistance;
        bool impactoLocal = Physics.Raycast(rayOrigin, spreadDir, out RaycastHit hitLocal, rt.def.maxDistance, rt.def.hitMask);
        if (impactoLocal)
        {
            finTrazadora = hitLocal.point;
            SpawnImpacto(hitLocal.point, hitLocal.normal);
        }

        SpawnTrazadora(rayOrigin, finTrazadora);

        if (IsSpawned)
        {
            PerformShootServerRpc(rayOrigin, spreadDir);
        }
        else if (impactoLocal)
        {
            AplicarDanioImpacto(hitLocal, spreadDir, rt.def.damage);
        }
    }

    [Rpc(SendTo.Server)]
    private void PerformShootServerRpc(Vector3 origin, Vector3 direction)
    {
        // Validación autoritativa: el servidor comprueba munición y cadencia
        WeaponRuntime rt = currentRuntime;
        if (rt == null || rt.def == null) return;

        if (!rt.PuedeDisparar())
        {
            // El cliente está desincronizado: corrige su munición
            SyncAmmo();
            return;
        }

        rt.IntentarDisparar(out _); // consume munición y cooldown en el servidor

        // Lag compensation: el raycast se evalúa donde veía al objetivo el tirador
        using (AlcanceRewind())
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, rt.def.maxDistance, rt.def.hitMask))
            {
                AplicarDanioImpacto(hit, direction, rt.def.damage);
            }
        }

        // FX de disparo para el resto de clientes
        PlayShootFxClientRpc(origin, direction);

        SyncAmmo();
    }

    /// <summary>
    /// Scope de rewind para el tirador dueño de este avatar. Sin manager,
    /// sin RTT medido o sin red devuelve un scope vacío (sin efecto).
    /// </summary>
    private LagCompensator.RewindScope AlcanceRewind()
    {
        var gm = GameModeManager.Instance;
        if (gm == null || gm.Compensador == null || NetworkManager.Singleton == null)
            return default;
        float rtt = gm.Compensador.GetRtt(OwnerClientId);
        if (rtt <= 0.001f) return default;
        float ahora = (float)NetworkManager.Singleton.LocalTime.Time;
        return gm.Compensador.RebobinarExcepto(OwnerClientId, rtt, ahora);
    }

    [Rpc(SendTo.Server)]
    private void PerformShotgunServerRpc(Vector3 origin, Vector3 baseDirection, Vector3 spreadBase)
    {
        WeaponRuntime rt = currentRuntime;
        if (rt == null || rt.def == null || !rt.def.IsShotgun) return;
        if (!rt.PuedeDisparar()) { SyncAmmo(); return; }
        rt.IntentarDisparar(out _); // consume 1 cartucho para la ráfaga completa

        int pellets = Mathf.Max(1, rt.def.pelletCount);
        float dmgPerPellet = rt.def.damagePerPellet ? rt.def.damage / pellets : rt.def.damage;
        PlayShotgunMuzzleFxClientRpc();
        // Un solo rewind para toda la ráfaga (las poses no cambian entre perdigones)
        using (AlcanceRewind())
        {
            for (int i = 0; i < pellets; i++)
            {
                Vector3 pelletSpread = new Vector3(
                    UnityEngine.Random.Range(-rt.def.Spread.x, rt.def.Spread.x),
                    UnityEngine.Random.Range(-rt.def.Spread.y, rt.def.Spread.y),
                    UnityEngine.Random.Range(-rt.def.Spread.z, rt.def.Spread.z));
                if (i == 0) pelletSpread = spreadBase;
                Vector3 pelletDir = (baseDirection + pelletSpread).normalized;
                if (Physics.Raycast(origin, pelletDir, out RaycastHit hit, rt.def.maxDistance, rt.def.hitMask))
                    AplicarDanioImpacto(hit, pelletDir, dmgPerPellet);
                PlayShotgunPelletFxClientRpc(origin, pelletDir);
            }
        }
        SyncAmmo();
    }

    [Rpc(SendTo.NotMe)]
    private void PlayShotgunMuzzleFxClientRpc()
    {
        PlayerSmoothMovement sm = control != null ? control.GetSmoothMovement() : GetComponentInChildren<PlayerSmoothMovement>(true);
        if (sm != null) sm.PlayRecoilForce();
        SpawnMuzzleFlash();
    }

    [Rpc(SendTo.NotMe)]
    private void PlayShotgunPelletFxClientRpc(Vector3 origin, Vector3 direction)
    {
        Vector3 fin = origin + direction * 100f;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, 100f))
        {
            fin = hit.point;
            SpawnImpacto(hit.point, hit.normal);
        }
        SpawnTrazadora(origin, fin);
    }

    [Rpc(SendTo.NotMe)]
    private void PlayShootFxClientRpc(Vector3 origin, Vector3 direction)
    {
        // El resto de clientes reproduce el disparo del jugador remoto
        PlayerSmoothMovement sm = control != null ? control.GetSmoothMovement() : GetComponentInChildren<PlayerSmoothMovement>(true);
        if (sm != null)
        {
            sm.PlayRecoilForce();
        }

        SpawnMuzzleFlash();

        Vector3 fin = origin + direction * 100f;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, 100f))
        {
            fin = hit.point;
            SpawnImpacto(hit.point, hit.normal);
        }
        SpawnTrazadora(origin, fin);
    }

    /// <summary>
    /// Resuelve el daño de un impacto: busca el jugador dueño del collider,
    /// aplica el multiplicador de la zona de impacto (cabeza x2) y delega en Health.
    /// </summary>
    private void AplicarDanioImpacto(RaycastHit hit, Vector3 direction, float damageBase)
    {
        PlayerController targetPlayer = hit.collider.GetComponentInParent<PlayerController>();
        if (targetPlayer == null || (control != null && targetPlayer == control)) return;

        float multiplicador = 1f;
        HitboxZones zona = hit.collider.GetComponent<HitboxZones>();
        if (zona != null)
        {
            multiplicador = zona.multiplicador;
        }

        if (targetPlayer.Vida != null)
        {
            targetPlayer.Vida.RecibirDanio(damageBase * multiplicador, hit.point, direction, gameObject);
        }
    }

    // ---------- Recarga ----------

    private void HandleReloadInput()
    {
        WeaponRuntime rt = currentRuntime;
        if (Input.GetKeyDown(KeyCode.R) && rt != null && rt.PuedeRecargar())
        {
            StartReload();
        }
    }

    public void StartReload()
    {
        WeaponRuntime rt = currentRuntime;
        if (rt == null || rt.def == null || !rt.PuedeRecargar()) return;

        StopReload();
        rt.IniciarRecarga();
        canShoot = false;

        IRecargaUI ui = control != null ? control.Ui : null;
        if (ui != null)
        {
            ui.TriggerLoadRoutine(rt.def.reloadTime);
        }

        if (IsSpawned)
        {
            IniciarRecargaServerRpc();
        }

        reloadCoroutine = StartCoroutine(ReloadCoroutine());
    }

    public void StopReload()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        WeaponRuntime rt = currentRuntime;
        if (rt != null && rt.isReloading)
        {
            rt.CancelarRecarga();
            if (IsSpawned)
            {
                CancelarRecargaServerRpc();
            }
        }

        canShoot = true;

        IRecargaUI ui = control != null ? control.Ui : null;
        if (ui != null)
        {
            ui.StopLoadRoutine();
        }
    }

    private IEnumerator ReloadCoroutine()
    {
        WeaponRuntime rt = currentRuntime;
        float espera = (rt != null && rt.def != null) ? rt.def.reloadTime : 2f;

        yield return new WaitForSeconds(espera);

        rt = currentRuntime;
        if (rt != null)
        {
            rt.CompletarRecarga();
        }

        canShoot = true;
        reloadCoroutine = null;

        if (IsSpawned)
        {
            ConfirmarRecargaServerRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void IniciarRecargaServerRpc()
    {
        WeaponRuntime rt = currentRuntime;
        if (rt != null) rt.IniciarRecarga();
    }

    [Rpc(SendTo.Server)]
    private void ConfirmarRecargaServerRpc()
    {
        WeaponRuntime rt = currentRuntime;
        if (rt != null)
        {
            rt.CompletarRecarga();
            SyncAmmo();
        }
    }

    [Rpc(SendTo.Server)]
    private void CancelarRecargaServerRpc()
    {
        WeaponRuntime rt = currentRuntime;
        if (rt != null) rt.CancelarRecarga();
    }

    // ---------- Sincronización de munición ----------

    private void SyncAmmo()
    {
        if (!IsServer) return;

        WeaponRuntime rt = currentRuntime;
        if (rt == null) return;

        ammoMag.Value = rt.currentAmmo;
        ammoReserva.Value = rt.reserveAmmo;
    }

    private void OnAmmoMagChanged(int previousValue, int newValue)
    {
        AplicarCorreccionMunicion();
    }

    private void OnAmmoReservaChanged(int previousValue, int newValue)
    {
        AplicarCorreccionMunicion();
    }

    private void AplicarCorreccionMunicion()
    {
        // El servidor ya tiene el valor autoritativo localmente
        if (IsServer) return;

        WeaponRuntime rt = currentRuntime;
        if (rt != null)
        {
            rt.ForzarMunicion(ammoMag.Value, ammoReserva.Value);
        }
    }

    // ---------- Cambio de arma ----------

    private void OnWeaponIndexChanged(int previousValue, int newValue)
    {
        EquipWeaponVisual(newValue);
        if (IsServer)
        {
            SyncAmmo();
        }
    }

    public void ChangeWeapon(int index)
    {
        if (index < 0 || index >= Weapons.Count) return;

        if (IsSpawned)
        {
            if (IsServer)
            {
                activeWeaponIndex.Value = index;
            }
            else
            {
                RequestChangeWeaponServerRpc(index);
            }
        }
        else
        {
            indiceArmaLocal = index;
            EquipWeaponVisual(index);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestChangeWeaponServerRpc(int index)
    {
        if (index >= 0 && index < Weapons.Count)
        {
            activeWeaponIndex.Value = index;
        }
    }

    private void EquipWeaponVisual(int index)
    {
        StopReload();

        if (Weapons == null || Weapons.Count == 0)
        {
            LoadAssignedWeapons();
        }

        if (index < 0 || index >= Weapons.Count) return;

        SpawnWeaponModel(Weapons[index]);
    }

    private void SpawnWeaponModel(WeaponDefinition def)
    {
        DespawnWeaponModel();

        if (def == null || def.model == null)
        {
            if (def != null)
                Debug.LogWarning($"[PlayerCombat] El arma '{def.itemName}' no tiene modelo asignado.");
            return;
        }

        Transform holder = locomocion != null ? locomocion.cameraHolder : null;
        Transform parentTransform = (gunParent != null) ? gunParent : (holder != null ? holder : transform);

        modeloArmaActual = Instantiate(def.model);
        modeloArmaActual.transform.SetParent(parentTransform, false);
        modeloArmaActual.transform.localPosition = Vector3.zero;
        modeloArmaActual.transform.localRotation = Quaternion.identity;
        modeloArmaActual.SetActive(true);

        // Viewmodel del jugador local: layer Weapons (lo dibuja WeaponCamera
        // como overlay, siempre encima).
        // Arma de jugadores remotos: layer Default (la dibuja la cámara
        // principal con profundidad normal, sin atravesar paredes).
        bool esLocal = control != null ? control.IsLocalPlayer : (!IsSpawned || IsOwner);
        int capa = esLocal ? LayerMask.NameToLayer("Weapons") : 0;
        if (capa >= 0)
        {
            AplicarCapaRecursiva(modeloArmaActual.transform, capa);
        }
    }

    private static void AplicarCapaRecursiva(Transform t, int capa)
    {
        t.gameObject.layer = capa;
        for (int i = 0; i < t.childCount; i++)
        {
            AplicarCapaRecursiva(t.GetChild(i), capa);
        }
    }

    // ---------- FX ----------

    private void SpawnMuzzleFlash()
    {
        if (fxMuzzleFlash == null) return;

        Transform boca = (gunParent != null) ? gunParent : transform;
        Vector3 pos = boca.position + boca.forward * 0.6f;
        FXPool.Spawn(fxMuzzleFlash, pos, boca.rotation, 0.15f);
    }

    private void SpawnImpacto(Vector3 punto, Vector3 normal)
    {
        if (fxImpacto == null) return;

        Quaternion rot = (normal.sqrMagnitude > 0.001f)
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;
        FXPool.Spawn(fxImpacto, punto, rot, 1.5f);
    }

    private void SpawnTrazadora(Vector3 origen, Vector3 destino)
    {
        if (fxTrazadora == null) return;

        // duracion 0: la propia Trazadora gestiona su vida y retorno al pool
        GameObject instancia = FXPool.Spawn(fxTrazadora, origen, Quaternion.identity, 0f);
        if (instancia != null)
        {
            Trazadora traz = instancia.GetComponent<Trazadora>();
            if (traz != null)
            {
                traz.Inicializar(fxTrazadora, origen, destino, 0.12f);
            }
        }
    }

    private void DespawnWeaponModel()
    {
        if (modeloArmaActual != null)
        {
            Destroy(modeloArmaActual);
            modeloArmaActual = null;
        }
    }

    // ---------- Respawn (GameMode FFA) ----------

    /// <summary>
    /// Reposiciona al jugador, restaura vida/munición y reactiva disparo.
    /// Llamado por GameModeManager (servidor u offline).
    /// </summary>
    public void Respawn(Vector3 posicion)
    {
        StopReload();
        // El transform tiene autoridad de cliente: el teleport lo aplica el
        // dueño (RPC dirigido); el servidor además lo deja puesto en su copia.
        if (IsSpawned && IsServer)
        {
            if (locomocion != null) locomocion.Teleport(posicion);
            else transform.position = posicion;
            TeleportPropietarioClientRpc(posicion,
                new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp) } });
        }
        else if (!IsSpawned)
        {
            if (locomocion != null) locomocion.Teleport(posicion);
            else transform.position = posicion;
        }

        Health vida = control != null ? control.Vida : GetComponent<Health>();
        if (vida != null)
        {
            if (!IsSpawned || IsServer) vida.RestaurarVida();
            else SolicitarRespawnServerRpc(posicion);
        }

        // Restaura munición de todas las armas
        for (int i = 0; i < armasRuntime.Count; i++)
            armasRuntime[i]?.Reiniciar();
        if (IsServer) SyncAmmo();

        canShoot = true;
    }

    /// <summary>
    /// El dueño aplica su propio teleport (su transform es client-authoritative:
    /// lo que ponga el servidor puede no replicársele).
    /// </summary>
    [Rpc(SendTo.SpecifiedInParams)]
    private void TeleportPropietarioClientRpc(Vector3 posicion, RpcParams rpc = default)
    {
        if (!IsOwner) return;
        if (locomocion != null) locomocion.Teleport(posicion);
        else transform.position = posicion;
    }

    [Rpc(SendTo.Server)]
    private void SolicitarRespawnServerRpc(Vector3 posicion)
    {
        // Cliente pide respawn (solo lo usa GameMode offline/server; validación mínima)
        if (locomocion != null) locomocion.Teleport(posicion);
        else transform.position = posicion;
        TeleportPropietarioClientRpc(posicion,
            new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp) } });
        Health vida = control != null ? control.Vida : GetComponent<Health>();
        if (vida != null) vida.RestaurarVida();
        for (int i = 0; i < armasRuntime.Count; i++)
            armasRuntime[i]?.Reiniciar();
        SyncAmmo();
    }
}
