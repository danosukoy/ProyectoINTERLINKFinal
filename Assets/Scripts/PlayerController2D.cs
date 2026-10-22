using UnityEngine;

public class PlayerController2D: MonoBehaviour
{
    [Header("Configuración")]
    public float velocidad = 5f;

    private Rigidbody2D rb;
    private Vector2 movimiento;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Si hay CUALQUIER overlay abierto, bloquear input pero mantener el componente activo
        // para que pueda volver a comprobar en el siguiente frame.
        if (BloqueoInterfaz.HayModalAbierta)
        {
            movimiento = Vector2.zero;
            return;
        }

        // Si el tiempo está congelado no se puede avanzar (FixedUpdate no corre).
        if (Time.timeScale == 0f)
        {
            movimiento = Vector2.zero;
            return;
        }

        // Se leen teclas concretas en lugar de los ejes "Horizontal"/"Vertical":
        // esos ejes también incluyen joystick y, con un mando (o el mando virtual
        // de Steam) conectado, el ruido del eje movía al personaje solo.
        float h = 0f;
        float v = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;

        movimiento = new Vector2(h, v);
    }

    void FixedUpdate()
    {
        if (movimiento.sqrMagnitude > 0.0001f)
            rb.MovePosition(rb.position + movimiento.normalized * velocidad * Time.fixedDeltaTime);
    }
}