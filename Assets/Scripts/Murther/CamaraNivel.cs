using UnityEngine;

// Cámara del nivel: altura de mundo fija (así Murther y el fondo conservan su escala),
// sigue a Murther en horizontal y nunca sale de la imagen de fondo (sin bordes vacíos).
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CamaraNivel : MonoBehaviour
{
    public SpriteRenderer fondo;
    public Transform objetivo;
    public float alturaMundo = 12f;
    public float suavizado = 8f;
    public float distanciaZ = 10f;

    void LateUpdate() { Actualizar(!Application.isPlaying); }

    public void Actualizar(bool inmediato = true)
    {
        if (fondo == null || fondo.sprite == null) return;
        var cam = GetComponent<Camera>();
        cam.orthographic = true;

        var b = fondo.bounds;
        float aspecto = cam.aspect;
        float tam = Mathf.Min(alturaMundo * 0.5f, b.extents.y, b.extents.x / aspecto);
        cam.orthographicSize = tam;

        float mitadAncho = tam * aspecto;
        float x = objetivo != null ? objetivo.position.x : b.center.x;
        x = Mathf.Clamp(x, b.min.x + mitadAncho, b.max.x - mitadAncho);
        // Anclada abajo: el piso siempre se ve; si sobra alto, se recorta por arriba.
        float y = Mathf.Clamp(b.min.y + tam, b.min.y + tam, b.max.y - tam);

        var p = transform.position;
        float nx = inmediato ? x : Mathf.Lerp(p.x, x, 1f - Mathf.Exp(-suavizado * Time.deltaTime));
        transform.position = new Vector3(nx, y, -distanciaZ);      // delante del plano de juego (z = 0)
        transform.rotation = Quaternion.identity;
    }
}
