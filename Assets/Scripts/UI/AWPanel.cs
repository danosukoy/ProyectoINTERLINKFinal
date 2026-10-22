using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AWPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI pText;
    [SerializeField] private TextMeshProUGUI sText;
    [SerializeField] private TextMeshProUGUI eText;
    [SerializeField] private PlayerDataManager gameManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake() {
        gameManager = PlayerDataManager.Instance;
        if (gameManager == null)
            gameManager = FindFirstObjectByType<PlayerDataManager>(FindObjectsInactive.Include);
    }

    void Start()
    {

    }

    private string ultimoP;
    private string ultimoS;
    private string ultimoE;

    // Update is called once per frame
    void Update()
    {
        if (gameManager == null) return;

        PonTexto(ref ultimoP, pText, gameManager.wPrimary?.itemName);
        PonTexto(ref ultimoS, sText, gameManager.wSecondary?.itemName);
        PonTexto(ref ultimoE, eText, gameManager.wExtra?.itemName);
    }

    private static void PonTexto(ref string ultimo, TextMeshProUGUI tmp, string valor)
    {
        if (tmp == null || ultimo == valor) return;
        ultimo = valor;
        tmp.text = valor;
    }
}
