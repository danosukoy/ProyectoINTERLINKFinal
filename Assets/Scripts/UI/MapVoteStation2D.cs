using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Estación de votación de mapas para la lobby 2D.
///
/// Equivalente a MapVoteStation, pero sin nada 3D: la detección es por
/// distancia en el plano XY contra el avatar local (LobbyWalkablePlayer2D.Local)
/// y el visual es un simple SpriteRenderer. Así la lobby 2D no arrastra un cubo
/// con canvas world-space ni raycasts desde cámara.
///
/// Se auto-instala solo si la escena activa es una lobby 2D (tiene
/// LobbyAvatarSpawner2D); la lobby 3D sigue usando MapVoteStation.
/// </summary>
public class MapVoteStation2D : MonoBehaviour
{
    [Header("Interacción")]
    [SerializeField] private float radio = 2.2f;
    [SerializeField] private KeyCode tecla = KeyCode.F;
    [SerializeField] private string prompt = "F - VOTAR MAPA";

    [Header("Colocación (plano XY)")]
    [SerializeField] private Vector2 posicion = new Vector2(0f, 3.15f);
    [SerializeField] private Vector2 tamano = new Vector2(2.4f, 1.2f);
    [SerializeField] private Color color = new Color(0.20f, 0.45f, 0.85f, 1f);

    private bool promptVisible;
    private static Sprite spriteBlanco;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstalar()
    {
        SceneManager.sceneLoaded += AlCargarEscena;
        AlCargarEscena(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        // Solo en la lobby 2D (la 3D instala su propia pantalla).
        if (FindFirstObjectByType<LobbyAvatarSpawner2D>(FindObjectsInactive.Include) == null) return;
        if (FindFirstObjectByType<MapVoteStation2D>(FindObjectsInactive.Include) != null) return;

        var go = new GameObject("VoteStation2D");
        go.AddComponent<MapVoteStation2D>();
    }

    private void Start()
    {
        transform.position = new Vector3(posicion.x, posicion.y, 0f);

        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteBlanco();
        sr.color = color;
        sr.sortingOrder = -5;
        transform.localScale = new Vector3(tamano.x, tamano.y, 1f);
    }

    private void Update()
    {
        // Sin manager (solo/offline) no hay votación: no se muestra el prompt.
        if (MapVoteManager.Instance == null) { OcultarPrompt(); return; }

        // Con un modal abierto (armería, inventario, el propio panel de voto)
        // el avatar no se mueve y la tecla no debe reabrir nada.
        if (BloqueoInterfaz.HayModalAbierta) { OcultarPrompt(); return; }

        var local = LobbyWalkablePlayer2D.Local;
        if (local == null) { OcultarPrompt(); return; }

        bool cerca = Vector2.Distance(local.transform.position, transform.position) <= radio;
        if (!cerca) { OcultarPrompt(); return; }

        if (!promptVisible)
        {
            MapVotePromptUI.Mostrar(prompt);
            promptVisible = true;
        }

        if (Input.GetKeyDown(tecla))
            MapVoteManager.SolicitarAbrirPanelVotacion();
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

    /// <summary>Sprite 1x1 blanco generado una vez (se tinta con el color).</summary>
    private static Sprite SpriteBlanco()
    {
        if (spriteBlanco != null) return spriteBlanco;

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();

        spriteBlanco = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return spriteBlanco;
    }
}
