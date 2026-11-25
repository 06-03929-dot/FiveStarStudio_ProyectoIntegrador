using UnityEngine;

// Capa del fondo que está más lejos que el dibujo principal: se desplaza más despacio que él cuando
// la cámara avanza, y eso da profundidad. Se ejecuta después de CamaraNivel para usar su posición final.
[ExecuteAlways]
[DefaultExecutionOrder(100)]
public class CapaParallax : MonoBehaviour
{
    public Transform camara;
    public SpriteRenderer referencia;      // el fondo principal ("Fondo")
    [Range(0f, 1f)] public float factor = 0.3f;   // 0 = se mueve igual que el fondo, 1 = queda fija en pantalla
    public Vector3 posicionBase;           // posición con la cámara centrada en el fondo

    void LateUpdate()
    {
        if (camara == null || referencia == null) return;
        float dx = camara.position.x - referencia.bounds.center.x;
        transform.position = posicionBase + Vector3.right * (dx * factor);
    }
}
