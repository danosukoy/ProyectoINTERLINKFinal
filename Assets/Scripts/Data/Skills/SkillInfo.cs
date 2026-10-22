using UnityEngine;


[CreateAssetMenu(fileName="Skill", menuName="Skill")]
public class SkillInfo : ScriptableObject
{
   public string skillName;
   public Sprite skillImage;
   public int skillLevel;
   public string skillInfo;
   public int skillDurtantion;
   public int skillCooldown;

}
