using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PUiButtonManager : MonoBehaviour
{
    public bool selected;
    public SkillInfo skill;

    PlayerDataManager gameManager;

    public PUiButtonManager[] pButtons;

    Button button;

    Image image;

  // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake(){
      button = GetComponent<Button>();
      image = GetComponent<Image>();
      pButtons = transform.parent != null
          ? transform.parent.GetComponentsInChildren<PUiButtonManager>(true)
          : new PUiButtonManager[]{ this };

      gameManager = PlayerDataManager.Instance;
      if (gameManager == null)
          gameManager = FindFirstObjectByType<PlayerDataManager>(FindObjectsInactive.Include);
    }

    void Start()
    {
        if (!RefsListas()) { Invoke(nameof(ReintentarAsignarSkills), 1f); return; }
        AsignarSkills();
    }

    private bool RefsListas() => gameManager != null && gameManager.skills != null && pButtons != null;

    private void ReintentarAsignarSkills()
    {
        if (this == null) return;
        if (gameManager == null)
        {
            gameManager = PlayerDataManager.Instance;
            if (gameManager == null)
                gameManager = FindFirstObjectByType<PlayerDataManager>(FindObjectsInactive.Include);
        }
        if (!RefsListas()) { Invoke(nameof(ReintentarAsignarSkills), 1f); return; }
        AsignarSkills();
    }

    private void AsignarSkills()
    {
        int n = Mathf.Min(pButtons.Length, gameManager.skills.Length);
        for(int i = 0; i < n; i++){
            if (pButtons[i] != null) pButtons[i].skill = gameManager.skills[i];
        }
    }

    private bool skillsSucias = true;

    public void MarcarSkillsSucias() => skillsSucias = true;

    // Update is called once per frame
    void Update()
    {
      Sprite quiero = skill != null ? skill.skillImage : null;
      if (image != null && image.sprite != quiero) image.sprite = quiero;

      // Solo reescribe el array cuando algo cambió (clic o asignación).
      if (!skillsSucias || !RefsListas()) return;
      skillsSucias = false;

      int n = Mathf.Min(pButtons.Length, gameManager.skills.Length);
      for(int i = 0; i < n; i++){
          if (pButtons[i] != null) gameManager.skills[i] = pButtons[i].skill;
      }
    }

    public void AlClickear(){
      foreach(var p in pButtons){
        if(p != this){
          p.selected = false;

        }
      }

      selected = !selected;
      skillsSucias = true;
      Debug.Log($"selected: {selected}");
    }

    void ActualizarVisual(){

    }

    void UpdateSkills(){
        foreach(var p in pButtons){
          if(p.skill == this.skill && p != this){
            p.skill = null;

          }
        }
    }
}
