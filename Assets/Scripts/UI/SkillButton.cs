using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Botón de habilidad del menú de armería. Al hacer clic alterna
/// equipar/desequipar la habilidad en los slots de PlayerDataManager.
/// Al pasar el ratón muestra el nombre y la descripción en el panel de info.
/// </summary>
public class SkillButton : MonoBehaviour, IPointerEnterHandler
{
    public SkillInfo skill;
    public bool selected;

    Button button;
    TextMeshProUGUI bText;
    TextMeshProUGUI textName;
    TextMeshProUGUI textInfo;
    // Resolución perezosa: sobrevive a recompilaciones en mitad del Play Mode
    PlayerDataManager gameManager => PlayerDataManager.Instance;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    /// <summary>Inyección de dependencias (la llama ArmeriaUI al crear el botón).</summary>
    public void InicializarReferencias(TextMeshProUGUI nombre, TextMeshProUGUI info)
    {
        textName = nombre;
        textInfo = info;
    }

    void Start()
    {
        if (button != null)
            bText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (bText != null)
            bText.text = skill != null ? skill.skillName : "?";
    }

    void Update()
    {
        if (button == null) return;

        selected = gameManager != null && gameManager.TieneSkill(skill);

        ColorBlock cb = button.colors;
        cb.colorMultiplier = selected ? 1.35f : 1f;
        button.colors = cb;
    }

    public void AlClickear()
    {
        if (gameManager == null || skill == null) return;
        gameManager.AlternarSkill(skill);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (skill == null) return;

        if (textName != null) textName.text = skill.skillName;
        if (textInfo != null) textInfo.text = skill.skillInfo;
    }
}
