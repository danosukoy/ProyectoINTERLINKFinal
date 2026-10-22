using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Desactiva la navegación del EventSystem mientras este campo de texto está
/// seleccionado.
///
/// Motivo: en el Input Manager los ejes "Submit" y "Cancel" incluyen
/// "joystick button 0" y "joystick button 1". Con un mando (o el mando virtual
/// que crea Steam) conectado, esos botones se disparan solos varias veces por
/// segundo y el StandaloneInputModule los envía al objeto seleccionado. En un
/// TMP_InputField eso ejecuta OnSubmit/OnCancel, que llaman a
/// DeactivateInputField(): el cursor se desactiva y se reactiva en bucle (el
/// "parpadeo" del texto) y ninguna tecla llega a escribirse.
///
/// Con sendNavigationEvents = false no se envían Submit/Cancel, pero el ratón
/// y el teclado siguen funcionando con normalidad.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public class CampoTextoNavegacionSegura : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private bool navegacionDesactivada;

    public void OnSelect(BaseEventData eventData)
    {
        navegacionDesactivada = true;
        Aplicar(false);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        navegacionDesactivada = false;
        Aplicar(true);
    }

    private void OnDisable()
    {
        // Si el campo desaparece mientras se editaba (p. ej. el modal se cierra
        // por código), hay que devolver la navegación o el resto del menú se
        // queda sin poder navegarse con teclado/mando.
        if (!navegacionDesactivada) return;
        navegacionDesactivada = false;
        Aplicar(true);
    }

    private static void Aplicar(bool navegacionActiva)
    {
        var es = EventSystem.current;
        if (es != null) es.sendNavigationEvents = navegacionActiva;
    }
}