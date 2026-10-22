using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Estación 2D interactuable (lobby list / inventario): al acercarse el
/// jugador muestra prompt y con F dispara el evento cableado en escena
/// (p.ej. SteamLobbyListUI.OnEnterLobbiesClicked u OnOpenInventoryClicked).
/// Solo abre paneles del canvas; no toca el ambiente 3D.
/// </summary>
public class InteractStation2D : MonoBehaviour
{
    [Header("Interacción")]
    [Tooltip("Jugador (si vacío, busca por tag 'Player' una vez por segundo).")]
    [SerializeField] private Transform jugador;
    [Tooltip("Distancia para interactuar.")]
    [SerializeField] private float radio = 2.5f;
    [Tooltip("Tecla de interacción.")]
    [SerializeField] private KeyCode tecla = KeyCode.F;
    [Tooltip("Texto del prompt, p.ej. 'F - VER SALAS'.")]
    [SerializeField] private string prompt = "F - ABRIR";

    [Header("Acción (cablear en el Inspector)")]
    public UnityEvent alInteractuar;

    private float proximaBusqueda;
    private bool promptVisible;

    private void Start()
    {
        // Auto-cableado por nombre si no hay nada en el Inspector:
        // StationLobby -> lista de salas, StationInventario -> inventario.
        if (alInteractuar == null) return;
        if (alInteractuar.GetPersistentEventCount() > 0) return;
        if (name.Contains("Inventario"))
        {
            var host = FindFirstObjectByType<InventarioCanvasHost>(FindObjectsInactive.Include);
            if (host != null) { alInteractuar.AddListener(host.Abrir); return; }
        }
        var ui = FindFirstObjectByType<SteamLobbyListUI>(FindObjectsInactive.Include);
        if (ui == null) return;
        if (name.Contains("Lobby"))
            alInteractuar.AddListener(ui.OnEnterLobbiesClicked);
        else if (name.Contains("Inventario"))
            alInteractuar.AddListener(ui.OnOpenInventoryClicked);
    }

    private void Update()
    {
        // Con un menú/overlay abierto el aviso "F - ..." no tiene sentido (y el
        // jugador está bloqueado), así que se oculta y se ignora la tecla.
        if (BloqueoInterfaz.HayModalAbierta)
        {
            OcultarPrompt();
            return;
        }

        Transform j = JugadorActual();
        if (j == null) { OcultarPrompt(); return; }

        bool cerca = Vector2.Distance(j.position, transform.position) <= radio;
        if (!cerca) { OcultarPrompt(); return; }

        // Mostrar es idempotente: si dos estaciones comparten el prompt global,
        // llamarlo cada frame evita quedarse con el aviso oculto por otro.
        MapVotePromptUI.Mostrar(prompt);
        promptVisible = true;

        if (Input.GetKeyDown(tecla))
        {
            OcultarPrompt();
            alInteractuar?.Invoke();
        }
    }

    private Transform JugadorActual()
    {
        if (jugador != null && jugador.gameObject.activeInHierarchy) return jugador;
        if (Time.time < proximaBusqueda) return jugador;
        proximaBusqueda = Time.time + 1f;
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) go = GameObject.Find("Player");
        if (go != null) jugador = go.transform;
        return jugador;
    }

    private void OcultarPrompt()
    {
        if (!promptVisible) return;
        promptVisible = false;
        MapVotePromptUI.Ocultar();
    }

    private void OnDisable() => OcultarPrompt();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radio);
    }
}
