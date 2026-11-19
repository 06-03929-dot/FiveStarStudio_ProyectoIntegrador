using UnityEngine;

// Efecto de tinta del jugador: la tinta sale POR DEBAJO del personaje.
//  - charcos de tinta en el suelo bajo los pies al correr, saltar, aterrizar, hacer dash y golpear
//  - gotas que brotan de los pies y, en el aire, gotean hacia abajo
//  - (opcional) rastro por detrás y arco de tinta al golpear: actívalos abajo
// Todo se crea por código. Los materiales los asigna Juego > Agregar tinta al jugador.
[RequireComponent(typeof(PlayerController3D))]
public class InkTrail : MonoBehaviour
{
    [Header("Materiales (los pone la herramienta del menú Juego)")]
    public Material dropMaterial;
    public Material trailMaterial;

    [Header("Tinta")]
    public Color inkColor = new Color(0.04f, 0.04f, 0.09f, 1f);
    public float puddleSize = 1.1f;          // tamaño de los charcos bajo los pies
    public float puddleEvery = 0.5f;         // un charco cada tantos metros al correr
    public float puddleLifetime = 1.8f;

    [Header("Extras (apagados: la tinta solo sale por debajo)")]
    public bool rearTrail = false;           // rastro de tinta por detrás del personaje
    public bool swingArc = false;            // arco de tinta delante al golpear
    public float trailTime = 0.45f;
    public float trailWidth = 0.5f;
    public float swingRadius = 1.1f;

    PlayerController3D pc;
    PlayerHealth health;
    Collider col;
    ParticleSystem drops, puddles;
    TrailRenderer bodyTrail, swingTrail;
    Transform swingPoint;
    float stepDistance, dashTimer, dripTimer, minVy;
    bool wasGrounded = true;

    void Awake()
    {
        pc = GetComponent<PlayerController3D>();
        health = GetComponent<PlayerHealth>();
        col = GetComponent<Collider>();

        var bodyGo = new GameObject("TintaRastro");
        bodyGo.transform.SetParent(transform, false);
        bodyGo.transform.localPosition = new Vector3(0f, -0.3f, -0.3f);
        bodyTrail = ConfigurarRastro(bodyGo, trailTime, trailWidth);

        var swingGo = new GameObject("TintaGolpe");
        swingGo.transform.SetParent(transform, false);
        swingPoint = swingGo.transform;
        swingTrail = ConfigurarRastro(swingGo, 0.25f, trailWidth * 0.8f);

        drops = CrearSistema("TintaGotas", false);
        puddles = CrearSistema("TintaCharcos", true);
    }

    void Start()
    {
        pc.Jumped += OnJump;
        pc.Attacked += OnAttack;
        if (health != null) health.Hurt += OnHurt;
    }

    void OnDestroy()
    {
        if (pc != null)
        {
            pc.Jumped -= OnJump;
            pc.Attacked -= OnAttack;
        }
        if (health != null) health.Hurt -= OnHurt;
    }

    TrailRenderer ConfigurarRastro(GameObject go, float time, float width)
    {
        var t = go.AddComponent<TrailRenderer>();
        t.time = time;
        t.widthMultiplier = width;
        t.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.7f), new Keyframe(1f, 0f));

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(inkColor, 0f), new GradientColorKey(inkColor, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        t.colorGradient = g;

        t.minVertexDistance = 0.04f;
        t.numCornerVertices = 3;
        t.numCapVertices = 3;
        t.alignment = LineAlignment.View;
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.receiveShadows = false;
        if (trailMaterial != null) t.sharedMaterial = trailMaterial;
        t.emitting = false;
        return t;
    }

    // horizontal = true: planos sobre el suelo (charcos). false: gotas que caen.
    ParticleSystem CrearSistema(string nombre, bool horizontal)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = horizontal ? puddleLifetime : 0.8f;
        main.startSize = horizontal ? puddleSize : 0.15f;
        main.startColor = inkColor;
        main.gravityModifier = horizontal ? 0f : 2.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = horizontal ? 120 : 400;

        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = horizontal
            ? new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.15f, 1f), new Keyframe(1f, 1f)))
            : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(inkColor, 0f), new GradientColorKey(inkColor, 1f) },
            horizontal
                ? new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = g;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (horizontal) rend.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        if (dropMaterial != null) rend.sharedMaterial = dropMaterial;

        ps.Play();   // queda encendido sin emitir; todo se lanza con Emit()
        return ps;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        Vector3 v = pc.Velocity;
        float speed = Mathf.Abs(v.x);
        bool dashing = pc.IsDashing;
        bool grounded = pc.IsGrounded;
        Bounds b = col.bounds;
        Vector3 under = new Vector3(b.center.x, b.min.y + 0.03f, b.min.z - 0.1f);   // bajo los pies
        float dirX = Mathf.Abs(v.x) > 0.1f ? Mathf.Sign(v.x) : pc.Facing;

        // Charcos y gotas bajo los pies al correr
        if (grounded && speed > 2.5f)
        {
            stepDistance += speed * dt;
            if (stepDistance >= puddleEvery)
            {
                stepDistance = 0f;
                Puddle(PuddlePos(b), puddleSize);
                Burst(under, 2, Vector3.up, 1.2f, 2.5f, 0.12f);
            }
        }

        // Dash: una hilera de charcos bajo los pies
        if (dashing)
        {
            dashTimer += dt;
            if (dashTimer >= 0.06f)
            {
                dashTimer = 0f;
                if (grounded) Puddle(PuddlePos(b), puddleSize * 1.1f);
                Burst(under, 2, Vector3.up, 1.2f, 3f, 0.15f);
            }
        }

        // En el aire la tinta gotea hacia abajo desde los pies
        if (!grounded && !dashing)
        {
            dripTimer += dt;
            if (dripTimer >= 0.1f)
            {
                dripTimer = 0f;
                Burst(under, 1, Vector3.down, 0.3f, 1.5f, 0.12f);
            }
        }

        // Aterrizaje: charco grande y salpicón
        if (!grounded) minVy = Mathf.Min(minVy, v.y);
        if (grounded && !wasGrounded)
        {
            if (minVy < -5f)
            {
                Puddle(PuddlePos(b), puddleSize * 1.6f);
                Burst(under, 12, Vector3.up, 1.5f, 4f, 0.18f);
            }
            minVy = 0f;
        }
        wasGrounded = grounded;

        // Extras opcionales
        bool moving = dashing || speed > 2.5f || (!grounded && v.y > 1f);
        bodyTrail.emitting = rearTrail && moving;
        bodyTrail.time = dashing ? trailTime * 1.5f : trailTime;
        bodyTrail.widthMultiplier = trailWidth * (dashing ? 1.5f : Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(speed / 12f)));

        if (swingArc && pc.IsAttacking)
        {
            float p = pc.AttackProgress;
            float a = Mathf.Lerp(80f, -50f, p) * Mathf.Deg2Rad;
            Vector3 c = b.center;
            Vector3 tip = new Vector3(c.x + pc.Facing * Mathf.Cos(a) * swingRadius, c.y + Mathf.Sin(a) * swingRadius, b.min.z - 0.1f);
            swingPoint.position = tip;
            swingTrail.emitting = p > 0.05f && p < 0.95f;
        }
        else
        {
            swingTrail.emitting = false;
            swingPoint.position = new Vector3(b.center.x, b.center.y, b.min.z - 0.1f);
        }
    }

    // Eventos
    void OnJump()
    {
        Bounds b = col.bounds;
        Puddle(PuddlePos(b), puddleSize * 1.3f);
        Burst(new Vector3(b.center.x, b.min.y + 0.03f, b.min.z - 0.1f), 10, Vector3.up, 1.5f, 4f, 0.16f);
    }

    void OnAttack()
    {
        Bounds b = col.bounds;
        if (pc.IsGrounded) Puddle(PuddlePos(b), puddleSize * 1.5f);
        // la tinta sale desde abajo y salta hacia delante
        Burst(new Vector3(b.center.x + pc.Facing * 0.5f, b.min.y + 0.1f, b.min.z - 0.1f), 12, new Vector3(pc.Facing, 0.5f, 0f), 0.8f, 6f, 0.17f);
    }

    void OnHurt()
    {
        Bounds b = col.bounds;
        if (pc.IsGrounded) Puddle(PuddlePos(b), puddleSize * 1.4f);
        Burst(new Vector3(b.center.x, b.min.y + 0.1f, b.min.z - 0.1f), 14, Vector3.up, 2f, 5f, 0.18f);
    }

    // Posición del charco: sobre el suelo, justo bajo el centro del jugador
    Vector3 PuddlePos(Bounds b) => new Vector3(b.center.x, b.min.y + 0.03f, b.center.z);

    void Puddle(Vector3 pos, float size)
    {
        var ep = new ParticleSystem.EmitParams();
        ep.position = pos;
        ep.velocity = Vector3.zero;
        ep.startSize = size * Random.Range(0.85f, 1.15f);
        ep.startLifetime = puddleLifetime * Random.Range(0.8f, 1.1f);
        ep.startColor = inkColor;
        ep.rotation = Random.Range(0f, 360f);
        puddles.Emit(ep, 1);
    }

    void Burst(Vector3 pos, int count, Vector3 dir, float spread, float speed, float size)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 vel = (dir.normalized + Random.insideUnitSphere * spread).normalized * Random.Range(0.4f, 1f) * speed;
            vel.z = 0f;

            var ep = new ParticleSystem.EmitParams();
            ep.position = pos + new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0f, 0.15f), 0f);
            ep.velocity = vel;
            ep.startSize = size * Random.Range(0.6f, 1.4f);
            ep.startLifetime = Random.Range(0.5f, 1f);
            ep.startColor = inkColor;
            drops.Emit(ep, 1);
        }
    }
}
