using UnityEngine;

// Encuadra la cámara ortográfica para que el sprite de fondo la cubra por completo
// (sin bordes vacíos) con cualquier relación de aspecto. El fondo queda detrás en Z.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class AjustarCamaraAFondo : MonoBehaviour
{
    public SpriteRenderer fondo;
    public float distanciaZ = 10f;

    void LateUpdate() { Encuadrar(); }

    public void Encuadrar()
    {
        if (fondo == null || fondo.sprite == null) return;
        var cam = GetComponent<Camera>();
        cam.orthographic = true;

        var b = fondo.bounds;
        // Cubrir: el menor de los tamaños que ajustan alto y ancho.
        cam.orthographicSize = Mathf.Min(b.extents.y, b.extents.x / cam.aspect);
        transform.position = new Vector3(b.center.x, b.center.y, fondo.transform.position.z - distanciaZ);
        transform.rotation = Quaternion.identity;
    }
}
