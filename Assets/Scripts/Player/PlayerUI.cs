using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour, IRecargaUI
{
    public PlayerController player;
    
    public TextMeshProUGUI currentAmmo;
    public TextMeshProUGUI reserveAmmo;
    public Image circle;

    public Image TopCH;
    public Image BottomCH;
    public Image LeftCH;
    public Image RightCH;
    
    public TextMeshProUGUI healthText;
    public Image healthBar;
    
    private Coroutine currentLoadRoutine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (player != null)
        {
            if (player.currentRuntime != null)
            {
                if (currentAmmo != null) currentAmmo.text = player.currentRuntime.currentAmmo.ToString();
                if (reserveAmmo != null) reserveAmmo.text = player.currentRuntime.reserveAmmo.ToString();
            }

            if (player.Vida != null)
            {
                if (healthText != null) healthText.text = Mathf.CeilToInt(player.Vida.Vida).ToString();
                if (healthBar != null) healthBar.fillAmount = player.Vida.VidaMaxima > 0 ? player.Vida.Vida / player.Vida.VidaMaxima : 1f;
            }
        }
    }

    public void TriggerLoadRoutine(float duration)
    {
        StopLoadRoutine();
        if (circle != null)
        {
            currentLoadRoutine = StartCoroutine(LoadRoutine(duration));
        }
    }

    public void StopLoadRoutine()
    {
        if (currentLoadRoutine != null)
        {
            StopCoroutine(currentLoadRoutine);
            currentLoadRoutine = null;
        }
        if (circle != null)
        {
            circle.fillAmount = 0f;
        }
    }

    public IEnumerator LoadRoutine(float duration)
    {
        float elapsed = 0f;
        if (circle != null) circle.fillAmount = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            if (circle != null) circle.fillAmount = elapsed / duration;

            yield return null;
        }

        if (circle != null) circle.fillAmount = 0f;
        currentLoadRoutine = null;
    }
}
