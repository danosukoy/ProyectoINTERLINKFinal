using UnityEngine;

public class GrenadeSkill : SkillBase
{
    [SerializeField] private GameObject grenade;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public override void OnUse(){
        GameObject newGrenade = Instantiate(grenade);
        Rigidbody gRb = newGrenade.GetComponent<Rigidbody>();
        gRb.AddForce(newGrenade.transform.forward * 10f, ForceMode.Impulse);

    }
}
