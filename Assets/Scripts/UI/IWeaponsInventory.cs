using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Controlador del inventario total estilo CoD (IWeapons en InventoryMenu).
/// Bindea los paneles ya puestos en escena: SelectedWeapons (5) con el pool
/// y TotalWeapons (15) con el registro filtrado por la categoría visible.
/// El clic alterna pertenencia al pool con tope de 5. Sin Update, sin Find
/// por frame: todo por eventos (pool + categoría).
/// </summary>
public class IWeaponsInventory : MonoBehaviour
{
    private static int MaxSeleccionadas => PlayerDataManager.MaxPoolPorCategoria;

    [Header("Datos (directos, sin ArmeriaUI)")]
    [SerializeField] private WeaponRegistry registro;

    private readonly List<WeaponCard> cardsSeleccionadas = new List<WeaponCard>();
    private readonly List<WeaponCard> cardsTotales = new List<WeaponCard>();
    private Transform colPrimary;
    private Transform colSecondary;
    private Transform colExtra;

    public WeaponCategory categoriaVisible = WeaponCategory.Primary;

    private TextMeshProUGUI textoNombre;
    private TextMeshProUGUI textoInfo;

    /// <summary>Total en espera de hueco: el próximo clic en una seleccionada la intercambia.</summary>
    private WeaponDefinition pendienteTotal;
    /// <summary>Último total clicado (se mantiene marcado para saber qué se pulsó).</summary>
    private WeaponDefinition ultimoClicTotal;
    /// <summary>Seleccionada marcada para salir: el próximo clic en un total la intercambia.</summary>
    private WeaponDefinition pendienteSalida;

    private void Start()
    {
        if (!BuscarTodo()) { Invoke(nameof(ReintentarInicio), 1f); return; }
        if (!DatosListos()) { Invoke(nameof(ReintentarInicio), 1f); return; }
        Suscribir();

        MostrarCategoria(categoriaVisible);
    }

    /// <summary>Localiza grids, columnas y textos aunque estén inactivos.</summary>
    private bool BuscarTodo()
    {
        var sel = BuscarInactivo("SelectedWeapons");
        var tot = BuscarInactivo("TotalWeapons");
        if (sel == null || tot == null)
        {
            Debug.LogWarning("[IWeaponsInventory] Falta SelectedWeapons o TotalWeapons en escena.");
            return false;
        }

        cardsSeleccionadas.Clear();
        cardsTotales.Clear();
        foreach (Transform hijo in sel)
            if (hijo != null) cardsSeleccionadas.Add(ObtenerCard(hijo));
        foreach (Transform hijo in tot)
            if (hijo != null) cardsTotales.Add(ObtenerCard(hijo));

        colPrimary = BuscarInactivo("Primarys");
        colSecondary = BuscarInactivo("Secondarys");
        colExtra = BuscarInactivo("Extras");

        BuscarTextosInfo();
        return true;
    }

    private static Transform BuscarInactivo(string nombre)
    {
        var todos = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
            if (todos[i] != null && todos[i].name == nombre) return todos[i];
        return null;
    }

    private bool DatosListos() => PlayerDataManager.Instance != null && registro != null;

    private void ReintentarInicio()
    {
        if (this == null) return;
        if ((cardsSeleccionadas.Count == 0 || cardsTotales.Count == 0) && !BuscarTodo())
        {
            Invoke(nameof(ReintentarInicio), 1f);
            return;
        }
        if (!DatosListos()) { Invoke(nameof(ReintentarInicio), 1f); return; }
        Suscribir();
        MostrarCategoria(categoriaVisible);
    }

    private bool suscrito;
    private void Suscribir()
    {
        if (suscrito) return;
        suscrito = true;
        PlayerDataManager.OnPoolCambiado += Refrescar;
    }

    private void OnDestroy()
    {
        if (suscrito) PlayerDataManager.OnPoolCambiado -= Refrescar;
    }

    private void BuscarTextosInfo()
    {
        var cw = BuscarInactivo("CWeapons");
        if (cw == null) return;
        var tNombre = cw.Find("Panel (2)/TextName");
        var tInfo = cw.Find("Panel (1)/TextInfo");
        if (tNombre != null) textoNombre = tNombre.GetComponent<TextMeshProUGUI>();
        if (tInfo != null) textoInfo = tInfo.GetComponent<TextMeshProUGUI>();
    }

    private void AlPasarMouse(WeaponDefinition def)
    {
        if (def == null) return;
        if (textoNombre != null && textoNombre.text != def.itemName) textoNombre.text = def.itemName;
        if (textoInfo != null && textoInfo.text != def.itemInfo) textoInfo.text = def.itemInfo;
    }

    /// <summary>
    /// Cambia la categoría visible (la llaman las pestañas de la escena).
    /// Apaga las otras columnas y re-bindea los grids.
    /// </summary>
    public void MostrarCategoria(WeaponCategory categoria)
    {
        categoriaVisible = categoria;
        pendienteTotal = null;
        pendienteSalida = null;
        ultimoClicTotal = null;
        if (colPrimary != null) colPrimary.gameObject.SetActive(categoria == WeaponCategory.Primary);
        if (colSecondary != null) colSecondary.gameObject.SetActive(categoria == WeaponCategory.Secondary);
        if (colExtra != null) colExtra.gameObject.SetActive(categoria == WeaponCategory.Extra);
        Refrescar();
    }

    /// <summary>Pestañas de escena (llamadas sin argumentos: modo Void, a prueba de bindings).</summary>
    public void MostrarPrimary() => MostrarCategoria(WeaponCategory.Primary);
    public void MostrarSecondary() => MostrarCategoria(WeaponCategory.Secondary);
    public void MostrarExtra() => MostrarCategoria(WeaponCategory.Extra);

    private static WeaponCard ObtenerCard(Transform panel)
    {
        var card = panel.GetComponent<WeaponCard>();
        if (card == null)
            Debug.LogWarning($"[IWeaponsInventory] {panel.name} sin WeaponCard en escena.");
        return card;
    }

    /// <summary>Re-bindea ambos grids desde registro + pool. Cero allocations de UI.</summary>
    public void Refrescar()
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null || registro == null || registro.armas == null) return;

        WeaponCategory categoria = categoriaVisible;

        // Totales: registro filtrado por categoría visible (melee cae en Extra).
        var totales = new List<WeaponDefinition>();
        for (int i = 0; i < registro.armas.Count; i++)
        {
            WeaponDefinition def = registro.armas[i];
            if (def == null) continue;
            WeaponCategory cat = def.IsMelee ? WeaponCategory.Extra : def.category;
            if (cat == categoria) totales.Add(def);
        }
        if (totales.Count > cardsTotales.Count)
            Debug.LogWarning($"[IWeaponsInventory] Hay {totales.Count} armas en {categoria} y {cardsTotales.Count} slots.");

        for (int i = 0; i < cardsTotales.Count; i++)
        {
            if (cardsTotales[i] == null) continue;
            if (i < totales.Count)
                cardsTotales[i].Bind(totales[i], pdm.EnPool(totales[i]), AlElegirTotal, AlPasarMouse, pendienteTotal == totales[i] || ultimoClicTotal == totales[i]);
            else
                cardsTotales[i].Vaciar();
        }

        // Seleccionadas: el pool filtrado por la categoría visible.
        // Así la cantidad mostrada (X) siempre cuadra con las seleccionadas de ese tipo.
        // Orden estable por registro para que no "salten" de slot al añadir/quitar.
        var orden = new Dictionary<WeaponDefinition, int>(registro.armas.Count);
        for (int i = 0; i < registro.armas.Count; i++)
        {
            WeaponDefinition r = registro.armas[i];
            if (r != null && !orden.ContainsKey(r)) orden[r] = i;
        }
        List<WeaponDefinition> poolCat = new List<WeaponDefinition>();
        if (pdm.poolArmas != null)
        {
            for (int i = 0; i < pdm.poolArmas.Count; i++)
            {
                WeaponDefinition def = pdm.poolArmas[i];
                if (def == null) continue;
                WeaponCategory cat = def.IsMelee ? WeaponCategory.Extra : def.category;
                if (cat == categoria) poolCat.Add(def);
            }
        }
        poolCat.Sort((a, b) => OrdenDe(orden, a).CompareTo(OrdenDe(orden, b)));
        for (int i = 0; i < cardsSeleccionadas.Count; i++)
        {
            if (cardsSeleccionadas[i] == null) continue;
            if (i < poolCat.Count)
                cardsSeleccionadas[i].Bind(poolCat[i], true, AlElegirSeleccionada, AlPasarMouse, pendienteSalida == poolCat[i]);
            else
                cardsSeleccionadas[i].Vaciar();
        }
    }

    /// <summary>
    /// Clic en totales: si está dentro se quita; si hay hueco entra solo;
    /// si está lleno queda pendiente (o intercambia con la salida marcada).
    /// </summary>
    private void AlElegirTotal(WeaponDefinition def)
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null || def == null) return;

        ultimoClicTotal = def;

        // En totales no se quita: si ya está dentro solo se marca.
        // Quitar es trabajo de las seleccionadas.
        if (pdm.poolArmas != null && pdm.poolArmas.Contains(def))
        {
            pendienteTotal = null;
            return;
        }

        WeaponCategory cat = CatDe(def);
        if (ContarPoolDe(pdm, cat) < MaxSeleccionadas)
        {
            pendienteTotal = null;
            pendienteSalida = null;
            pdm.AlternarPool(def);
            Debug.Log($"[IWeaponsInventory] {def.itemName} añadida al pool de {cat} ({ContarPoolDe(pdm, cat)}/{MaxSeleccionadas}).");
            return;
        }

        if (pendienteSalida != null && CatDe(pendienteSalida) == cat)
        {
            Intercambiar(pdm, pendienteSalida, def);
            return;
        }

        pendienteTotal = def;
        pendienteSalida = null;
        Debug.Log($"[IWeaponsInventory] Pool de {cat} lleno. Pulsa una seleccionada para cambiarla por {def.itemName}.");
        Refrescar(); // sin cambio de datos: hay que pintar la marca a mano.
    }

    /// <summary>
    /// Clic en seleccionadas: con pendiente de total, intercambia; con pool
    /// lleno, marca la salida; si no, quita directo.
    /// </summary>
    private void AlElegirSeleccionada(WeaponDefinition def)
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null || def == null) return;

        if (pendienteTotal != null && pendienteTotal != def)
        {
            Intercambiar(pdm, def, pendienteTotal);
            return;
        }

        if (ContarPoolDe(pdm, CatDe(def)) >= MaxSeleccionadas)
        {
            pendienteSalida = def;
            pendienteTotal = null;
            Debug.Log($"[IWeaponsInventory] {def.itemName} marcada para salir. Pulsa un total para cambiarla.");
            Refrescar(); // sin cambio de datos: hay que pintar la marca a mano.
            return;
        }

        pendienteTotal = null;
        pendienteSalida = null;
        pdm.AlternarPool(def);
        // El refresco llega vía OnPoolCambiado.
    }

    /// <summary>
    /// Intercambio atómico: sale una, entra otra, con verificación final.
    /// </summary>
    private void Intercambiar(PlayerDataManager pdm, WeaponDefinition sale, WeaponDefinition entra)
    {
        WeaponCategory catSale = CatDe(sale);
        WeaponCategory catEntra = CatDe(entra);
        bool mismaCat = catSale == catEntra;
        bool enPoolSale = pdm.poolArmas != null && pdm.poolArmas.Contains(sale);
        bool enPoolEntra = pdm.poolArmas != null && pdm.poolArmas.Contains(entra);

        pendienteTotal = null;
        pendienteSalida = null;

        if (!enPoolSale || enPoolEntra)
        {
            Debug.LogWarning($"[IWeaponsInventory] Intercambio inválido: {sale?.itemName} -> {entra?.itemName}.");
            return;
        }
        if (!mismaCat && ContarPoolDe(pdm, catEntra) >= MaxSeleccionadas)
        {
            pendienteTotal = entra;
            Debug.Log($"[IWeaponsInventory] Sin hueco en {catEntra} para {entra.itemName}.");
            Refrescar(); // sin cambio de datos: hay que pintar la marca a mano.
            return;
        }

        pdm.AlternarPool(sale);
        pdm.AlternarPool(entra);

        // Post-condición: la que entra debe estar dentro sí o sí.
        if (pdm.poolArmas == null || !pdm.poolArmas.Contains(entra))
        {
            if (pdm.poolArmas == null) pdm.poolArmas = new List<WeaponDefinition>();
            if (!pdm.poolArmas.Contains(entra)) pdm.poolArmas.Add(entra);
            pdm.GuardarLoadout();
        }
        Debug.Log($"[IWeaponsInventory] Cambio: sale {sale.itemName}, entra {entra.itemName}.");
    }

    private static WeaponCategory CatDe(WeaponDefinition def) =>
        def != null && def.IsMelee ? WeaponCategory.Extra : def.category;

    private static int OrdenDe(Dictionary<WeaponDefinition, int> orden, WeaponDefinition def) =>
        def != null && orden.TryGetValue(def, out int o) ? o : int.MaxValue;

    private static int ContarPoolDe(PlayerDataManager pdm, WeaponCategory cat)
    {
        if (pdm.poolArmas == null) return 0;
        int n = 0;
        for (int i = 0; i < pdm.poolArmas.Count; i++)
        {
            WeaponDefinition d = pdm.poolArmas[i];
            if (d == null) continue;
            if ((d.IsMelee ? WeaponCategory.Extra : d.category) == cat) n++;
        }
        return n;
    }
}
