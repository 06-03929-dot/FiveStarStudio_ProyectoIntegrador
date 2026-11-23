using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum TipoSalto { Simple, Doble }

// Murther en el plano 2D del nivel: movimiento lateral (gira su vista hacia donde camina),
// salto (botón A del control / Espacio) y doble salto, en el que solo sube la cabeza y el
// cuerpo la alcanza. La entrada usa el Input System nuevo (teclado y control).
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class MurtherController : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 8.5f;
    public float aceleracion = 85f;
    public float aceleracionAire = 55f;
    public float frenado = 95f;

    [Header("Salto")]
    public float fuerzaSalto = 16f;
    public float fuerzaDobleSalto = 14.5f;
    public float gravedad = 3.4f;
    public float multiplicadorCaida = 1.5f;
    [Range(0.1f, 1f)] public float corteSalto = 0.5f;
    public float tiempoCoyote = 0.1f;
    public float tiempoBuffer = 0.12f;

    [Header("Modelo")]
    public Transform visual;
    public Animator animator;              // estados Idle / Salto (parámetro bool "Aire")
    public GameObject modeloIdle;
    public GameObject modeloSalto;
    public Transform cabezaIdle;
    public Transform cabezaSalto;
    public float giroBase = 0f;            // ajuste manual extra (el giro se calibra solo con los ojos)
    [Range(0f, 60f)] public float giroHaciaCamara = 35f;  // 0 = perfil estricto; 35 = vista de 3/4 (se ve la cara)
    public float velocidadGiro = 900f;     // grados por segundo

    [Header("Doble salto: sube la cabeza y el cuerpo la alcanza")]
    public float alturaCabeza = 1.15f;
    public float tiempoSubidaCabeza = 0.12f;
    public float tiempoCuerpoSigue = 0.28f;

    public event Action<TipoSalto> Salto;
    public event Action Aterrizo;

    public bool EnSuelo { get; private set; }
    public float Direccion { get; private set; } = 1f;
    public float VelocidadX => rb.linearVelocity.x;

    Rigidbody2D rb;
    CapsuleCollider2D col;
    readonly RaycastHit2D[] impactos = new RaycastHit2D[8];
    ContactFilter2D filtro;

    float entradaH, bufferT, coyoteT, efectoT = 99f, yawModelo;
    int saltosUsados;
    bool cortarSalto, usaPoseSalto;
    Vector3 cabezaPosAplicada, cabezaOffsetAplicado, escalaVisual = Vector3.one;
    Transform cabezaActiva;

    void Awake()
    {
        if (visual != null) escalaVisual = visual.localScale;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        rb.freezeRotation = true;
        rb.gravityScale = gravedad;
        filtro = ContactFilter2D.noFilter;
        filtro.useTriggers = false;
        if (modeloSalto != null) modeloSalto.SetActive(false);
        AplicarGiro(true);
    }

    System.Collections.IEnumerator Start()
    {
        yield return CalibrarGiro();
    }

    // Averigua hacia dónde mira el modelo (los ojos respecto al centro de la cabeza, sin depender de
    // la rotación con la que venga el FBX) y deja el giro base para que mire a +X cuando Direccion > 0.
    System.Collections.IEnumerator CalibrarGiro()
    {
        if (visual == null) yield break;
        foreach (var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
        yield return null;                           // el Animator ya aplicó su primer cuadro

        // Posiciones en el espacio de "Visual": no dependen de cómo esté girado en este momento.
        Vector3 suma = Vector3.zero, cabeza = Vector3.zero;
        int ojos = 0; bool hayCabeza = false;
        foreach (var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string nombre = r.name.ToLowerInvariant();
            bool esOjo = nombre.StartsWith("ojo_"), esCabeza = nombre == "cabeza_geo";
            if (!esOjo && !esCabeza) continue;
            Transform origen = r.rootBone != null ? r.rootBone : r.transform;
            Vector3 c = visual.InverseTransformPoint(origen.TransformPoint(r.localBounds.center));
            if (esOjo) { suma += c; ojos++; }
            else { cabeza = c; hayCabeza = true; }
        }
        if (ojos > 0 && hayCabeza)
        {
            Vector3 frente = suma / ojos - cabeza;
            frente.y = 0f;
            if (frente.sqrMagnitude > 1e-6f) yawModelo = Mathf.Atan2(frente.z, frente.x) * Mathf.Rad2Deg;
        }
        AplicarGiro(true);
    }

    // Entrada externa (pruebas automáticas o un control físico): sustituye al teclado/mando.
    [HideInInspector] public bool usarEntradaExterna;
    [HideInInspector] public float entradaExterna;
    public void PedirSalto() { bufferT = tiempoBuffer; }

    void Update()
    {
        entradaH = usarEntradaExterna ? entradaExterna : LeerHorizontal();
        if (Mathf.Abs(entradaH) > 0.1f) Direccion = Mathf.Sign(entradaH);

        if (SaltoPresionado()) bufferT = tiempoBuffer;
        if (SaltoSoltado()) cortarSalto = true;

        ActualizarModelo();
        AplicarGiro(false);
        ActualizarLuzTinta();
        efectoT += Time.deltaTime;
    }

    // Luz de personaje para el shader "Five Star/Murther Tinta": gira con Murther, así la cara queda
    // iluminada y el costado en sombra (con trama) mirando a cualquiera de los dos lados. Con una luz
    // fija del mundo eso no es posible: hacia un lado la cara quedaría en sombra.
    [Header("Luz del shader de tinta (frente, arriba, hacia la cámara)")]
    public Vector3 luzTinta = new Vector3(0.75f, 0.6f, 0.3f);
    // Color de esa luz: el mismo tono que la luz que Murther lleva consigo y que ilumina el muro a su
    // alrededor, para que él y el escenario estén bajo la misma luz. Intensidad 0 = luz principal.
    public Color colorLuzTinta = new Color(1f, 0.92f, 0.82f);
    public float intensidadLuzTinta = 1f;
    static readonly int idLuzTinta = Shader.PropertyToID("_LuzTintaDir");
    static readonly int idColorLuzTinta = Shader.PropertyToID("_LuzTintaColor");

    void ActualizarLuzTinta()
    {
        if (visual == null) return;
        // Hacia dónde apunta la cara ahora mismo (incluye el giro en curso al voltear).
        float yaw = visual.localEulerAngles.y - yawModelo - giroBase;
        Vector3 frente = transform.rotation * (Quaternion.Euler(0f, yaw, 0f) * Vector3.right);
        Vector3 dir = frente * luzTinta.x + Vector3.up * luzTinta.y + Vector3.back * luzTinta.z;
        dir.Normalize();
        Shader.SetGlobalVector(idLuzTinta, new Vector4(dir.x, dir.y, dir.z, 1f));
        // Las luces llegan al shader en espacio lineal (color.linear × intensidad): igual aquí.
        Color c = colorLuzTinta.linear * intensidadLuzTinta;
        Shader.SetGlobalVector(idColorLuzTinta, intensidadLuzTinta > 0f ? new Vector4(c.r, c.g, c.b, 1f) : Vector4.zero);
    }

    void OnDisable()
    {
        Shader.SetGlobalVector(idLuzTinta, Vector4.zero);
        Shader.SetGlobalVector(idColorLuzTinta, Vector4.zero);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        bool estabaEnSuelo = EnSuelo;
        EnSuelo = DetectarSuelo();

        if (EnSuelo)
        {
            coyoteT = tiempoCoyote;
            saltosUsados = 0;
            if (!estabaEnSuelo) Aterrizo?.Invoke();
        }
        else coyoteT -= dt;

        var v = rb.linearVelocity;

        float objetivo = entradaH * velocidad;
        float a = Mathf.Abs(objetivo) > 0.01f ? (EnSuelo ? aceleracion : aceleracionAire) : frenado;
        v.x = Mathf.MoveTowards(v.x, objetivo, a * dt);

        bufferT -= dt;
        if (bufferT > 0f)
        {
            if (coyoteT > 0f && saltosUsados == 0)
            {
                v.y = fuerzaSalto;
                saltosUsados = 1;
                coyoteT = 0f;
                bufferT = 0f;
                cortarSalto = false;
                Salto?.Invoke(TipoSalto.Simple);
            }
            else if (!EnSuelo && saltosUsados < 2)
            {
                v.y = fuerzaDobleSalto;
                saltosUsados = 2;
                bufferT = 0f;
                cortarSalto = false;
                efectoT = 0f;
                Salto?.Invoke(TipoSalto.Doble);
            }
        }

        if (cortarSalto)
        {
            if (v.y > 0f && efectoT > tiempoSubidaCabeza + tiempoCuerpoSigue) v.y *= corteSalto;
            cortarSalto = false;
        }

        rb.gravityScale = v.y < 0f ? gravedad * multiplicadorCaida : gravedad;
        rb.linearVelocity = v;
    }

    bool DetectarSuelo()
    {
        int n = col.Cast(Vector2.down, filtro, impactos, 0.08f);
        for (int i = 0; i < n; i++)
            if (impactos[i].normal.y > 0.5f) return true;
        return false;
    }

    // ───────────── Entrada (teclado y control) ─────────────

    static float LeerHorizontal()
    {
        float h = 0f;
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) h -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) h += 1f;
        }
        var g = Gamepad.current;
        if (g != null)
        {
            float s = g.leftStick.x.ReadValue();
            if (Mathf.Abs(s) > 0.2f) h = s;
            if (g.dpad.left.isPressed) h = -1f;
            if (g.dpad.right.isPressed) h = 1f;
        }
        return Mathf.Clamp(h, -1f, 1f);
    }

    static bool SaltoPresionado()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && (k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame))
            || (g != null && g.buttonSouth.wasPressedThisFrame);
    }

    static bool SaltoSoltado()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && (k.spaceKey.wasReleasedThisFrame || k.wKey.wasReleasedThisFrame || k.upArrowKey.wasReleasedThisFrame))
            || (g != null && g.buttonSouth.wasReleasedThisFrame);
    }

    // ───────────── Modelo: pose de suelo / aire y giro ─────────────

    void ActualizarModelo()
    {
        if (animator != null) animator.SetBool("Aire", !EnSuelo);
        if (modeloIdle == null || modeloSalto == null) return;
        bool aire = !EnSuelo;
        if (aire == usaPoseSalto) return;
        usaPoseSalto = aire;
        modeloIdle.SetActive(!aire);
        modeloSalto.SetActive(aire);
    }

    void AplicarGiro(bool inmediato)
    {
        if (visual == null) return;
        // giroHaciaCamara gira la cara hacia el jugador hacia los dos lados (de perfil estricto el casco
        // se veía como un rectángulo blanco sin ojos ni dientes). Al voltear pasa por el frente.
        float yaw = Direccion > 0f ? giroHaciaCamara : 180f - giroHaciaCamara;
        var destino = Quaternion.Euler(0f, yawModelo + yaw + giroBase, 0f);
        visual.localRotation = inmediato
            ? destino
            : Quaternion.RotateTowards(visual.localRotation, destino, velocidadGiro * Time.deltaTime);
    }

    // Después de la animación: la cabeza sube, luego el cuerpo la retoma.
    void LateUpdate()
    {
        float k = FaseCabeza();
        var cabeza = usaPoseSalto ? cabezaSalto : cabezaIdle;

        if (cabeza != null)
        {
            // Si el Animator no escribe este hueso, conserva la posición del frame anterior.
            Vector3 baseWorld = (cabeza == cabezaActiva && (cabeza.position - cabezaPosAplicada).sqrMagnitude < 1e-10f)
                ? cabezaPosAplicada - cabezaOffsetAplicado
                : cabeza.position;
            cabezaOffsetAplicado = Vector3.up * (alturaCabeza * k);
            cabeza.position = baseWorld + cabezaOffsetAplicado;
            cabezaPosAplicada = cabeza.position;
            cabezaActiva = cabeza;
        }
        else if (visual != null)
        {
            // Sin hueso de cabeza: estirar el cuerpo hacia arriba desde los pies, respecto a la escala
            // original de "Visual" (antes la Y se fijaba en 1 y se perdía cualquier escala que tuviera).
            visual.localScale = new Vector3(escalaVisual.x, escalaVisual.y * (1f + 0.3f * k), escalaVisual.z);
        }
    }

    // 0..1: sube durante tiempoSubidaCabeza y baja durante tiempoCuerpoSigue.
    float FaseCabeza()
    {
        float t = efectoT;
        if (t < tiempoSubidaCabeza) return Suavizar(t / tiempoSubidaCabeza);
        t -= tiempoSubidaCabeza;
        if (t < tiempoCuerpoSigue) return 1f - Suavizar(t / tiempoCuerpoSigue);
        return 0f;
    }

    static float Suavizar(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
