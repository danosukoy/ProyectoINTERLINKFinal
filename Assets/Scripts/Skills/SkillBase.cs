using UnityEngine;

public class SkillBase : MonoBehaviour
{
    public SkillInfo skill;
    int currentCooldown;
    int currentDurantion;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void Use(){
      OnUse();

    }

    public virtual void OnUse(){

    }
}
