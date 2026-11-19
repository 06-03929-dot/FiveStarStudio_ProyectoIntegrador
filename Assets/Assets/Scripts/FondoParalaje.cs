using UnityEngine;

// Fondo 2D (una imagen) que se pega a la cámara y se desplaza más lento que el nivel (parallax).
// Lo crea la herramienta: Juego > Poner fondo 2D
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FondoParalaje : MonoBehaviour
{
    public Material material;           // lo asigna la herramienta (con tu imagen)
    public float distancia = 80f;       // qué tan lejos de la cámara está (más lejos = más al fondo)
    [Range(0f, 1f)] public float parallaxX = 0.1f;   // 0 = fijo en pantalla, más = se mueve más
    [Range(0f, 1f)] public float parallaxY = 0f;

    Camera cam;
    Mesh mesh;
    Texture tex;
    readonly Vector2[] uvs = new Vector2[4];

    void Awake()
    {
        mesh = new Mesh { name = "QuadFondo" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f));

        GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (material != null)
        {
            tex = material.mainTexture;
            if (tex != null) tex.wrapMode = TextureWrapMode.Repeat;
        }
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null || tex == null) return;
        }

        // Tamaño que llena la pantalla a esa distancia
        float h = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * distancia * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w = h * cam.aspect;

        Transform c = cam.transform;
        transform.position = c.position + c.forward * distancia;
        transform.rotation = c.rotation;
        transform.localScale = new Vector3(w, h, 1f);

        // La imagen cubre toda la pantalla sin deformarse
        float imgAspect = tex.width / (float)tex.height;
        float ih = Mathf.Max(h, w / imgAspect);
        float iw = ih * imgAspect;
        float uSpan = w / iw;
        float vSpan = h / ih;

        float u0 = (1f - uSpan) * 0.5f + c.position.x * parallaxX / iw;
        float v0 = (1f - vSpan) * 0.5f + c.position.y * parallaxY / ih;
        v0 = Mathf.Clamp(v0, 0f, 1f - vSpan);

        uvs[0] = new Vector2(u0, v0);
        uvs[1] = new Vector2(u0 + uSpan, v0);
        uvs[2] = new Vector2(u0, v0 + vSpan);
        uvs[3] = new Vector2(u0 + uSpan, v0 + vSpan);
        mesh.uv = uvs;
    }
}
