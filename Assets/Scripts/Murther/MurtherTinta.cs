using UnityEngine;

// Rastro de tinta negra bajo los pies de Murther: gotas que salpican y manchas que quedan
// en el suelo al caminar, y ráfagas al saltar, hacer doble salto y aterrizar.
// Los dos Particle System se crean aquí y cuelgan de "PiesTinta", justo debajo de los pies.
[RequireComponent(typeof(MurtherController))]
public class MurtherTinta : MonoBehaviour
{
    public Material materialGota;
    public Material materialMancha;
    public Color colorTinta = new Color(0.03f, 0.03f, 0.07f, 1f);
    public float distanciaEntreManchas = 0.45f;
    public float alturaPies = 0.03f;
    public float escalaTinta = 1.3f;        // tamaño de gotas y manchas

    MurtherController murther;
    ParticleSystem gotas, manchas;
    Transform pies;
    float distancia, goteoT;
    int ordenCapa = 20;

    void Awake()
    {
        murther = GetComponent<MurtherController>();

        pies = new GameObject("PiesTinta").transform;
        pies.SetParent(transform, false);
        pies.localPosition = new Vector3(0f, alturaPies, -0.2f);

        gotas = CrearSistema("Gotas", materialGota, 400, 0f);
        manchas = CrearSistema("Manchas", materialMancha, 120, 0f);
        gotas.gameObject.transform.SetParent(pies, false);
        manchas.gameObject.transform.SetParent(pies, false);
    }

    void OnEnable()
    {
        murther = GetComponent<MurtherController>();
        murther.Salto += AlSaltar;
        murther.Aterrizo += AlAterrizar;
    }

    void OnDisable()
    {
        murther.Salto -= AlSaltar;
        murther.Aterrizo -= AlAterrizar;
    }

    void Update()
    {
        float vx = murther.VelocidadX;
        if (murther.EnSuelo)
        {
            distancia += Mathf.Abs(vx) * Time.deltaTime;
            if (Mathf.Abs(vx) > 0.5f && distancia >= distanciaEntreManchas)
            {
                distancia = 0f;
                Mancha(Random.Range(-0.15f, 0.15f), Random.Range(0.35f, 0.6f));
                int n = Random.Range(2, 4);
                for (int i = 0; i < n; i++)
                    Gota(new Vector2(-Mathf.Sign(vx) * Random.Range(0.5f, 2.2f), Random.Range(1.4f, 3.2f)), 0.07f, 0.16f);
            }
        }
        else
        {
            goteoT -= Time.deltaTime;
            if (goteoT <= 0f)
            {
                goteoT = 0.07f;
                Gota(new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-0.5f, 0.8f)), 0.08f, 0.2f);
            }
        }
    }

    void AlSaltar(TipoSalto tipo)
    {
        if (tipo == TipoSalto.Simple)
        {
            Mancha(0f, 0.8f);
            Mancha(Random.Range(-0.3f, 0.3f), 0.55f);
            for (int i = 0; i < 14; i++) Gota(Salpicadura(2.2f, 4.5f), 0.08f, 0.22f);
        }
        else
        {
            // En el aire: gotas grandes y pequeñas que caen desde debajo de Murther.
            for (int i = 0; i < 10; i++) Gota(Salpicadura(1.2f, 3f), 0.18f, 0.34f);
            for (int i = 0; i < 18; i++) Gota(Salpicadura(2f, 5f), 0.07f, 0.2f);
        }
    }

    void AlAterrizar()
    {
        Mancha(0f, 0.9f);
        Mancha(Random.Range(-0.35f, 0.35f), 0.6f);
        for (int i = 0; i < 12; i++) Gota(Salpicadura(2.5f, 4.2f), 0.07f, 0.2f);
    }

    static Vector2 Salpicadura(float minV, float maxV)
    {
        float lado = Random.value < 0.5f ? -1f : 1f;
        return new Vector2(lado * Random.Range(minV * 0.4f, maxV), Random.Range(minV * 0.4f, maxV));
    }

    void Gota(Vector2 velocidad, float tamMin, float tamMax)
    {
        var p = new ParticleSystem.EmitParams
        {
            position = pies.position + new Vector3(Random.Range(-0.25f, 0.25f), 0f, 0f),
            velocity = velocidad,
            startSize = Random.Range(tamMin, tamMax) * escalaTinta,
            startLifetime = Random.Range(0.45f, 0.9f),
            startColor = colorTinta,
            rotation = Random.Range(0f, 360f) * Mathf.Deg2Rad,
        };
        gotas.Emit(p, 1);
    }

    void Mancha(float desplazamientoX, float ancho)
    {
        ancho *= escalaTinta;
        var p = new ParticleSystem.EmitParams
        {
            position = pies.position + new Vector3(desplazamientoX, 0f, 0.05f),
            velocity = Vector3.zero,
            startSize3D = new Vector3(ancho, ancho * Random.Range(0.28f, 0.4f), 1f),
            startLifetime = Random.Range(1.4f, 2.2f),
            startColor = colorTinta,
        };
        manchas.Emit(p, 1);
    }

    ParticleSystem CrearSistema(string nombre, Material material, int maximo, float gravedad)
    {
        var go = new GameObject(nombre);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maximo;
        main.startSpeed = 0f;
        main.startLifetime = 1f;
        main.startSize3D = nombre == "Manchas";
        main.gravityModifier = nombre == "Gotas" ? 2.4f : gravedad;

        var emision = ps.emission;
        emision.enabled = false;
        var forma = ps.shape;
        forma.enabled = false;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        if (nombre == "Gotas")
        {
            var tam = ps.sizeOverLifetime;
            tam.enabled = true;
            tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.7f, 0.9f), new Keyframe(1f, 0.2f)));
        }

        var render = ps.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.sortingOrder = ordenCapa++;
        render.sharedMaterial = material != null ? material : new Material(Shader.Find("Sprites/Default"));
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        return ps;
    }
}
