using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class InventoryManager : MonoBehaviour
{
    [Header("Puntos de destino")]
    [SerializeField] private Camera camera;
    [SerializeField] private Transform[] puntos;

    [Header("Configuración")]
    public float velocidadMovimiento = 2f;
    public float velocidadRotacion = 2f;

    private int puntoActual = 0;
    private bool enTransicion = false;
    private Coroutine movimientoActual;

    [Header("Canvas")]
    [SerializeField] private GameObject canvasPrincipal;
    [SerializeField] private GameObject canvasWeapons;
    [SerializeField] private GameObject canvasSkills;
    [SerializeField] private GameObject canvasPlayer;

    private GameObject canvasActive;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // El inventario siempre arranca en CPrincipal, sin importar el estado
        // guardado en la escena.
        if (canvasPrincipal != null) ChangeCanvas(canvasPrincipal);
        else canvasActive = canvasPrincipal;
        if (camera != null && puntos != null && puntos.Length > 0 && puntos[0] != null)
        {
            camera.transform.localPosition = puntos[0].localPosition;
            camera.transform.localRotation = puntos[0].localRotation;
        }
    }

    /// <summary>
    /// Entrada directa al editor del pool (la llama StartMenu al abrir el
    /// inventario): muestra el canvas de armas y mueve la cámara allí.
    /// </summary>
    public void AbrirArmas()
    {
        if (canvasWeapons != null) ChangeCanvas(canvasWeapons);
        if (puntos != null && puntos.Length > 1 && puntos[1] != null)
            StartMove(puntos[1]);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public IEnumerator MoveCamera(Transform destiny){
        enTransicion = true;

        while (Vector3.Distance(camera.transform.position, destiny.position) > 0.01f)
        {
            // Mover posición
            camera.transform.position = Vector3.Lerp(
                camera.transform.position,
                destiny.position,
                velocidadMovimiento * Time.deltaTime
            );

            // Rotar suavemente
            camera.transform.rotation = Quaternion.Slerp(
                camera.transform.rotation,
                destiny.rotation,
                velocidadRotacion * Time.deltaTime
            );

            yield return null;
        }

        // Ajustar posición exacta al final
        camera.transform.position = destiny.position;
        camera.transform.rotation = destiny.rotation;

        //ChangeCanvas(c);

        movimientoActual = null;

        enTransicion = false;
    }

    public void ChangeCanvas(GameObject canvas){
        //if(enTransicion) return;
        if (canvas == null) return;

        if (canvasPrincipal != null) canvasPrincipal.SetActive(false);
        if (canvasWeapons != null) canvasWeapons.SetActive(false);
        if (canvasSkills != null) canvasSkills.SetActive(false);
        if (canvasPlayer != null) canvasPlayer.SetActive(false);

        canvas.SetActive(true);
        canvasActive = canvas;
    }

    public void StartMove(Transform destiny){
        // Sin cámara (el inventario ya vive en StartMenu): no hay transición.
        if (camera == null || destiny == null) return;
        if (movimientoActual != null)
            StopCoroutine(movimientoActual);
        movimientoActual = StartCoroutine(MoveCamera(destiny));
    }

    /// <summary>Navegación directa (para botones de escena, modo Void).</summary>
    public void MostrarPrincipal() => ChangeCanvas(canvasPrincipal);
    public void MostrarSkills() => ChangeCanvas(canvasSkills);
    public void MostrarPlayer() => ChangeCanvas(canvasPlayer);
    public void MostrarWeapons() => ChangeCanvas(canvasWeapons);

    /// <summary>
    /// Botón BACK: cierra los canvas del inventario (vuelve al menú/área).
    /// </summary>
    public void VolverAlMenu(){
        var host = FindFirstObjectByType<InventarioCanvasHost>(FindObjectsInactive.Include);
        if (host != null) { host.Cerrar(); return; }
        var menu = FindFirstObjectByType<SteamLobbyListUI>(FindObjectsInactive.Include);
        if (menu != null) { menu.CloseInventory(); return; }
        var sc = SceneManager.GetSceneByName("InventoryMenu");
        if (sc.isLoaded) SceneManager.UnloadSceneAsync(sc);
    }
}
