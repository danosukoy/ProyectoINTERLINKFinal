using UnityEngine;


[CreateAssetMenu(fileName="Weapon", menuName="Item/Weapon")]

public class WeaponInfo : ItemInfo
{
    [Header("Data")]
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] Vector3 weaponPosition;
    public LayerMask hitMask;
    public Vector3 Spread = new Vector3(0.1f, 0.1f, 0.1f);
    
    [Header("Fire")]
    public float damage;
    public float maxDistance;

    public float fireRate = 0.25f;
    
    [Header("Ammo & Reload")]
    public int currentAmmo;
    public int magSize;

    public float reloadTime;
    public bool isReloading;

    //--

    private float lastShootTime;
    private MonoBehaviour ActiveMonoBehaviour;
    private GameObject model;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Spawn(Transform parent, MonoBehaviour ActiveMonoBehaviour){
        this.ActiveMonoBehaviour = ActiveMonoBehaviour;
        lastShootTime = 0;

        model = Instantiate(modelPrefab);
        model.transform.SetParent(parent, false);

        model.transform.localPosition = weaponPosition;
        model.transform.localRotation = parent.transform.localRotation;
    }

    public void Despawn(){
        model.SetActive(false);
        Destroy(model);
    }

    public void Shoot(){
        if(Time.time > fireRate * lastShootTime){
            Debug.Log("Shoot");
        }
    }

    
}
