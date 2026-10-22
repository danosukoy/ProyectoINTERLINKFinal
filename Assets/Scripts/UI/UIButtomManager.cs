using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class UIButtomManager: MonoBehaviour
{
    private Button button;
    private bool selected;
    public SkillInfo skill;
    private string Clave => $"hab_{skill.skillName}";
    [SerializeField] private static GameObject panelInfo;
    [SerializeField] private static GameObject panelPInfo;
    [SerializeField] private PUiButtonManager[] pButtons;
    [SerializeField] private UIButtomManager[] sButtons;

    [SerializeField] private PUiButtonManager actualP;

    public bool HasAnyPSelected
    {
        get
        {
            foreach (PUiButtonManager p in pButtons)
            {
                if (p.selected) {

                actualP = p;
                return true;
                }

            }
            return false;
        }
    }

    void Awake()
    {
        button = GetComponent<Button>();
        if (panelInfo == null)
        {
            panelInfo = GameObject.Find("Principal");
            pButtons = panelInfo.GetComponentsInChildren<PUiButtonManager>();
        }

        if(panelPInfo == null){
          panelPInfo = GameObject.Find("Secondary");
          sButtons = panelPInfo.GetComponentsInChildren<UIButtomManager>();
        }

    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
      if(HasAnyPSelected){
        button.interactable = true;
      }else{
        button.interactable = false;
      }
      if(actualP?.skill != skill){
        selected = false;
        ActualizarVisual();
      }

    }

    public void AlClickear()
    {
        selected = !selected;

        actualP.skill = skill;

        actualP.selected = false;

        foreach(var p in pButtons){
          if(p.skill == actualP.skill && p != actualP){
            p.skill = null;

          }
        }

        foreach (UIButtomManager s in sButtons){
          foreach (PUiButtonManager p in pButtons){
            if(p.skill == s.skill){
              s.selected = true;
              Debug.Log($"Guardado: {Clave}");

            }else{
              s.selected = false;
              Debug.Log($"Eliminado: {Clave}");
            }
          }
        }

        PlayerPrefs.Save();
        ActualizarVisual();
    }

    void ActualizarVisual()
    {
        ColorBlock cb = button.colors;
        cb.colorMultiplier = selected ? 1.35f : 1f;
        button.colors = cb;
    }
}
