using System.Collections.Generic;
using UnityEngine;

// El rig de Maya de Murther se anima con controles (curvas) y cinemática inversa, que Unity
// no calcula. Este componente copia el movimiento de cada control (respecto a su pose inicial)
// al hueso correspondiente, así las animaciones de Iddle.fbx y Jump.fbx mueven el cuerpo.
// Va en la raíz del modelo y se ejecuta después del Animator.
[DefaultExecutionOrder(-10)]
public class RigControlesAHuesos : MonoBehaviour
{
    class Par
    {
        public Transform control, hueso;
        public Vector3 posControl, posHueso, escalaControl, escalaHueso;
        public Quaternion rotControl, rotHueso;
        public int profundidad;
        public bool usaEscala;
    }

    static readonly string[,] Mapa =
    {
        { "global_ctrl", "raiz_jnt" },
        { "cadera_ctrl", "cadera_jnt" },
        { "cabeza_ctrl", "cabeza_jnt" },
        { "hombro_izq_ctrl", "hombro_izq_jnt" },
        { "codo_izq_ctrl", "codo_izq_jnt" },
        { "mano_izq_ctrl", "mano_izq_jnt" },
        { "hombro_der_ctrl", "hombro_der_jnt" },
        { "codo_der_ctrl", "codo_der_jnt" },
        { "mano_der_ctrl", "mano_der_jnt" },
        { "rodilla_izq_ctrl", "rodilla_izq_jnt" },
        { "pie_izq_ctrl", "pie_izq_jnt" },
        { "rodilla_der_ctrl", "rodilla_der_jnt" },
        { "pie_der_ctrl", "pie_der_jnt" },
    };

    // Las animaciones de Maya giran el rig (p. ej. 67° en Y) y eso dejaría la cabeza y el cuerpo mirando
    // a lados distintos; por defecto solo se copian posiciones y la orientación del modelo no cambia.
    public bool copiarRotaciones = false;

    readonly List<Par> pares = new List<Par>();

    void Awake()
    {
        var porNombre = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (!porNombre.ContainsKey(t.name)) porNombre[t.name] = t;

        for (int i = 0; i < Mapa.GetLength(0); i++)
        {
            if (!porNombre.TryGetValue(Mapa[i, 0], out var c) || !porNombre.TryGetValue(Mapa[i, 1], out var h)) continue;
            pares.Add(new Par
            {
                control = c,
                hueso = h,
                posControl = Local(c.position),
                rotControl = LocalRot(c.rotation),
                escalaControl = c.lossyScale,
                posHueso = Local(h.position),
                rotHueso = LocalRot(h.rotation),
                escalaHueso = h.localScale,
                profundidad = Profundidad(h),
                usaEscala = Mapa[i, 1] == "cabeza_jnt",
            });
        }
        pares.Sort((a, b) => a.profundidad.CompareTo(b.profundidad));
    }

    void LateUpdate()
    {
        foreach (var p in pares)
        {
            Vector3 dp = Local(p.control.position) - p.posControl;
            Quaternion dr = LocalRot(p.control.rotation) * Quaternion.Inverse(p.rotControl);
            p.hueso.position = transform.TransformPoint(p.posHueso + dp);
            if (copiarRotaciones) p.hueso.rotation = transform.rotation * (dr * p.rotHueso);
            if (p.usaEscala)
            {
                Vector3 s = p.control.lossyScale;
                p.hueso.localScale = new Vector3(
                    p.escalaHueso.x * Razon(s.x, p.escalaControl.x),
                    p.escalaHueso.y * Razon(s.y, p.escalaControl.y),
                    p.escalaHueso.z * Razon(s.z, p.escalaControl.z));
            }
        }
    }

    Vector3 Local(Vector3 mundo) { return transform.InverseTransformPoint(mundo); }
    Quaternion LocalRot(Quaternion mundo) { return Quaternion.Inverse(transform.rotation) * mundo; }
    static float Razon(float a, float b) { return Mathf.Abs(b) > 1e-5f ? a / b : 1f; }

    static int Profundidad(Transform t)
    {
        int n = 0;
        for (; t.parent != null; t = t.parent) n++;
        return n;
    }
}
