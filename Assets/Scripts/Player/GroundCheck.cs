using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    PlayerLocomotion locomocion;

    // Capas de jugadores: no deben contar como "suelo"
    // (hitboxes de disparo y cuerpos físicos ajenos)
    static int layerHitbox = -1;
    static int layerPlayerBody = -1;

    void Awake()
    {
        locomocion = GetComponentInParent<PlayerLocomotion>();
        if (layerHitbox < 0) layerHitbox = LayerMask.NameToLayer("Hitbox");
        if (layerPlayerBody < 0) layerPlayerBody = LayerMask.NameToLayer("PlayerBody");
    }

    bool Ignorar(GameObject go)
    {
        if (locomocion != null && go == locomocion.gameObject)
            return true;

        int layer = go.layer;
        return layer == layerHitbox || layer == layerPlayerBody;
    }

    void OnTriggerEnter(Collider other)
    {
        if (locomocion == null || Ignorar(other.gameObject))
            return;

        locomocion.SetGroundedState(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (locomocion == null || Ignorar(other.gameObject))
            return;

        locomocion.SetGroundedState(false);
    }

    void OnTriggerStay(Collider other)
    {
        if (locomocion == null || Ignorar(other.gameObject))
            return;

        locomocion.SetGroundedState(true);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (locomocion == null || Ignorar(collision.gameObject))
            return;

        locomocion.SetGroundedState(true);
    }

    void OnCollisionExit(Collision collision)
    {
        if (locomocion == null || Ignorar(collision.gameObject))
            return;

        locomocion.SetGroundedState(false);
    }

    void OnCollisionStay(Collision collision)
    {
        if (locomocion == null || Ignorar(collision.gameObject))
            return;

        locomocion.SetGroundedState(true);
    }
}
