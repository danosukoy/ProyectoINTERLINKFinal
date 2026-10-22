using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class WeaponButton : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler
{
    public WeaponDefinition weapon;
    public Button button;
    TextMeshProUGUI bText;
    TextMeshProUGUI textName;
    TextMeshProUGUI textInfo;
    Transform wgot;
    // Resolución perezosa: sobrevive a recompilaciones en mitad del Play Mode
    PlayerDataManager gameManager => PlayerDataManager.Instance;
    public bool selected;


    // Grupo de deselección (hermanos del mismo slot). Lo asigna ArmeriaUI.
    private WeaponButton[] grupoDeseleccion;

    /// <summary>
    /// Modo pool (InventoryMenu): el click alterna la pertenencia al inventario
    /// seleccionado en vez de equipar en un slot.
    /// </summary>
    [HideInInspector] public bool modoPool = false;

    void Awake(){
        button = GetComponent<Button>();
    }

    /// <summary>Inyección de dependencias (la llama ArmeriaUI al crear el botón).</summary>
    public void InicializarReferencias(TextMeshProUGUI nombre, TextMeshProUGUI info, Transform preview)
    {
        textName = nombre;
        textInfo = info;
        wgot = preview;
    }

    public void AsignarGrupo(WeaponButton[] grupo)
    {
        grupoDeseleccion = grupo;
    }

    void Start()
    {
        if (button != null)
            bText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (bText != null)
            bText.text = weapon != null ? weapon.itemName : "?";

        if (gameManager == null || weapon == null) return;

        selected = modoPool
            ? gameManager.EnPool(weapon)
            : gameManager.wPrimary == weapon
                || gameManager.wSecondary == weapon
                || gameManager.wExtra == weapon
                || gameManager.cuchillo == weapon;
    }

    private bool ultimoSelected;
    private bool colorInicializado;

    // Update is called once per frame
    void Update()
    {
        if (button == null) return;

        // Solo toca colors cuando cambia (evita rebuild de UI cada frame).
        if (!colorInicializado || ultimoSelected != selected)
        {
            ultimoSelected = selected;
            colorInicializado = true;
            ColorBlock cb = button.colors;
            cb.colorMultiplier = selected ? 1.35f : 1f;
            button.colors = cb;
        }
    }

    public void AlClickear(){

        if (gameManager == null || weapon == null) return;

        // Modo pool: alterna pertenencia al inventario seleccionado
        if (modoPool){
            selected = gameManager.AlternarPool(weapon);
            return;
        }

        // Melee (cuchillo) va al slot fijo de cuchillo, antes que la categoría
        if (weapon.IsMelee){
            gameManager.cuchillo = weapon;
            DeseleccionarGrupo();
        }
        else if(weapon.category == WeaponCategory.Primary){
            gameManager.wPrimary = weapon;
            DeseleccionarGrupo();
        }

        else if(weapon.category == WeaponCategory.Secondary){
            gameManager.wSecondary = weapon;
            DeseleccionarGrupo();
        }

        else if(weapon.category == WeaponCategory.Extra){
            gameManager.wExtra = weapon;
            DeseleccionarGrupo();
        }

        selected = true;

        // Persistir el loadout elegido (fase 2)
        gameManager.GuardarLoadout();
    }

    private void DeseleccionarGrupo()
    {
        if (grupoDeseleccion == null) return;
        foreach (WeaponButton b in grupoDeseleccion)
        {
            if (b != null && b != this) b.selected = false;
        }
    }

    // Caché de previews: un modelo instanciado una vez y reutilizado
    // (evita el tirón de Instantiate/Destroy en cada hover).
    private static readonly Dictionary<WeaponDefinition, GameObject> previewCache = new Dictionary<WeaponDefinition, GameObject>();
    private static GameObject previewActual;

    public void OnPointerEnter(PointerEventData eventData)
        {
            if (weapon == null) return;

            if (textName != null) textName.text = weapon.itemName;
            if (textInfo != null) textInfo.text = weapon.itemInfo;

            if (previewActual != null) previewActual.SetActive(false);
            previewActual = null;

            if (weapon.model != null && wgot != null)
            {
                if (!previewCache.TryGetValue(weapon, out GameObject pv) || pv == null)
                {
                    pv = Instantiate(weapon.model);
                    previewCache[weapon] = pv;
                }
                pv.transform.SetParent(wgot, false);
                pv.transform.localPosition = Vector3.zero;
                pv.transform.localRotation = Quaternion.identity;
                pv.SetActive(true);
                previewActual = pv;
            }
        }

    public void OnPointerExit(PointerEventData eventData)
    {

    }
}
