using UnityEngine;
using Steamworks;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    [Header("Configuración de Steam")]
    [SerializeField] private uint appId = 480;

    private bool isInitialized;
    public bool IsInitialized => isInitialized;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSteam();
    }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    [System.Runtime.InteropServices.DllImport("libdl.dylib")]
    private static extern System.IntPtr dlopen(string filename, int flags);
#endif

    private void InitializeSteam()
    {
        try
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            string dylibPath = System.IO.Path.Combine(Application.dataPath, "Plugins/macOS/libsteam_api.dylib");
            if (System.IO.File.Exists(dylibPath))
            {
                System.IntPtr handle = dlopen(dylibPath, 2);
                Debug.Log($"[SteamManager] macOS native dylib loaded from {dylibPath}, handle: {handle}");
            }
#endif
            SteamClient.Init(appId);
            isInitialized = SteamClient.IsValid;

            if (isInitialized)
            {
                Debug.Log($"[SteamManager - Facepunch] SteamAPI inicializado correctamente. Usuario: {SteamClient.Name} (SteamID: {SteamClient.SteamId})");
                // OBLIGATORIO para sockets relay (P2P): sin esto CreateRelaySocket
                // falla con "Invalid Socket" y el host nunca arranca.
                try
                {
                    SteamNetworkingUtils.InitRelayNetworkAccess();
                    Debug.Log("[SteamManager - Facepunch] Acceso a red relay solicitado (InitRelayNetworkAccess).");
                }
                catch (System.Exception exRelay)
                {
                    Debug.LogWarning($"[SteamManager - Facepunch] InitRelayNetworkAccess falló: {exRelay.Message}");
                }

                // Diagnóstico temporal: vuelca el log interno de red de Steam
                // (SDR/relay) a consola para ver por qué no hay relay.
                try
                {
                    SteamNetworkingUtils.DebugLevel = NetDebugOutput.Msg;
                    SteamNetworkingUtils.OnDebugOutput -= VolcarDebugRed;
                    SteamNetworkingUtils.OnDebugOutput += VolcarDebugRed;
                }
                catch (System.Exception exDbg)
                {
                    Debug.LogWarning($"[SteamManager - Facepunch] No se pudo activar debug de red: {exDbg.Message}");
                }
            }
            else
            {
                Debug.LogWarning("[SteamManager - Facepunch] SteamClient.Init() falló. Asegúrate de tener el cliente de Steam abierto.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SteamManager - Facepunch] Excepción al inicializar Facepunch.Steamworks: {e}");
        }
    }

    private static void VolcarDebugRed(NetDebugOutput tipo, string mensaje)
    {
        // Solo relay/SDR para no inundar (los paquetes P2P vendrían después)
        if (mensaje != null && (mensaje.Contains("SDR") || mensaje.Contains("Relay") || mensaje.Contains("relay")
            || mensaje.Contains("CERT") || mensaje.Contains("Auth") || mensaje.Contains("P2P")))
            Debug.Log($"[SteamNet:{tipo}] {mensaje}");
    }

    private void Update()
    {
        if (isInitialized)
        {
            SteamClient.RunCallbacks();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this && isInitialized)
        {
            SteamClient.Shutdown();
            isInitialized = false;
        }
    }
}
