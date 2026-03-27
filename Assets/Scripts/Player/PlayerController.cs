using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    // Move
    [SerializeField] BoxCollider groundCheck;
    [SerializeField] bool grounded = true;
    Vector3 smoothMoveVelocity;
    [SerializeField] float sprintSpeed, walkSpeed, jumpForce, smoothTime;
    [SerializeField] Vector3 moveAmount;
    
    [Header("Look")]
    //Look
    [SerializeField] Transform cameraHolder;
    [SerializeField] float mouseSensitivity;

    //Gun Setup
    [SerializeField] List<WeaponInfo> Weapons;
    [SerializeField] Transform gunParent;
    [Space]
    [Header("Runtime Filled")]
    public WeaponInfo activeWeapon;

    
    [HideInInspector]
    public float yLookRotation;
    [HideInInspector]
    public float xLookRotation;
    [HideInInspector]
    public float zRotation = 5f;
    
    Rigidbody rb;

     void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
    
    // Start is called before the first frame update
    void Start()
    {
        ChangeWeapon(0);
    }

    // Update is called once per frame
    void Update()
    {
        Move();
        Jump();
        Look();
        
    }

    void FixedUpdate(){
        rb.MovePosition(rb.position + transform.TransformDirection(moveAmount) * Time.fixedDeltaTime);
    }

    void Move()
    {
        if(!grounded) return;
        Vector3 moveDir = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;

        moveAmount = Vector3.SmoothDamp(moveAmount, moveDir * (Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed), ref smoothMoveVelocity, smoothTime);
    }

    void Jump()
    {
        if(Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            rb.AddForce(transform.up * jumpForce);
        }
    }

    void Look(){
        yLookRotation += Input.GetAxis("Mouse X") * mouseSensitivity;
        xLookRotation -= Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.rotation = Quaternion.Euler(0, yLookRotation, 0);
        cameraHolder.localRotation = Quaternion.Euler(xLookRotation, 0, 0);
    }

    public void SetGroundedState(bool _grounded)
    {
        grounded = _grounded;
    }

    void Shoot(){
        if(Input.GetMouseButtonDown(0) && activeWeapon != null){
            activeWeapon.Shoot();
        }
    }

    void ChangeWeapon(int index){
        if(activeWeapon != null){
            activeWeapon.Despawn();
        }
        
        WeaponInfo weapon = Weapons[index];


        if(weapon != null){
            Debug.Log(weapon);
            activeWeapon = weapon;
            Debug.Log(activeWeapon);
            activeWeapon.Spawn(gunParent, this);
        }else{
            Debug.LogError("No Weapons");
        }

    }
}
