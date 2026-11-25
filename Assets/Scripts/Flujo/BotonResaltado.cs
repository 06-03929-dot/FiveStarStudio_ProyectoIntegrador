using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Botón del menú con borde y relleno; se resalta al pasar el cursor (o al seleccionar con teclado).
public class BotonResaltado : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Text texto;
    public Graphic borde;
    public Graphic relleno;
    public Color colorTexto = new Color(0.95f, 0.95f, 0.97f);
    public Color colorTextoResaltado = new Color(1f, 0.85f, 0.4f);
    public Color colorBorde = new Color(1f, 0.82f, 0.35f, 0.6f);
    public Color colorBordeResaltado = new Color(1f, 0.9f, 0.5f, 1f);
    public Color colorRelleno = new Color(0.03f, 0.03f, 0.06f, 0.78f);
    public Color colorRellenoResaltado = new Color(0.28f, 0.2f, 0.04f, 0.9f);
    public float escalaResaltado = 1.08f;
    public float velocidad = 12f;

    bool resaltado;

    void OnEnable() { resaltado = false; Aplicar(1f); }
    void OnDisable() { Aplicar(1f); }

    // El ratón mueve la selección: así nunca quedan dos botones resaltados a la vez
    // (uno elegido con el control o las flechas y otro bajo el cursor).
    public void OnPointerEnter(PointerEventData e)
    {
        resaltado = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData e)
    {
        resaltado = false;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }
    public void OnSelect(BaseEventData e) { resaltado = true; }
    public void OnDeselect(BaseEventData e) { resaltado = false; }

    void Update()
    {
        float objetivo = resaltado ? escalaResaltado : 1f;
        float actual = transform.localScale.x;
        Aplicar(Mathf.Lerp(actual, objetivo, 1f - Mathf.Exp(-velocidad * Time.unscaledDeltaTime)));
    }

    void Aplicar(float escala)
    {
        transform.localScale = new Vector3(escala, escala, 1f);
        float k = Mathf.InverseLerp(1f, escalaResaltado, escala);
        if (texto != null) texto.color = Color.Lerp(colorTexto, colorTextoResaltado, k);
        if (borde != null) borde.color = Color.Lerp(colorBorde, colorBordeResaltado, k);
        if (relleno != null) relleno.color = Color.Lerp(colorRelleno, colorRellenoResaltado, k);
    }
}
