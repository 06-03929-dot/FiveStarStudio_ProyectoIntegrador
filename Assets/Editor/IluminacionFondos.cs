using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Iluminación de los fondos dibujados de Nivel1-3:
//  - Material "Five Star/Fondo Iluminado" con un mapa de normales generado del propio dibujo.
//  - Capa trasera que el equipo dibujó pero no estaba en la escena (columnas del vacío en el nivel 1,
//    bosque del limbo en el nivel 3): se ve por los huecos transparentes del fondo, con parallax.
//  - Luces puntuales donde el dibujo tiene fuentes de luz (faroles, vitral) y una luz fría detrás
//    de los huecos para la capa trasera. En el nivel 1, una luz suave que acompaña a Murther.
// Menú: Juego > Iluminación de fondos. También se aplica al final de "Fase 1" para que rehacer los
// niveles no borre la iluminación.
public static class IluminacionFondos
{
    const string ShaderFondo = "Five Star/Fondo Iluminado";
    const string CarpetaMateriales = "Assets/Fondos/Materiales";
    const string NombreGrupo = "Luces del Escenario";
    const string NombreCapaLejana = "Fondo Lejano";
    const string NombreLuzMurther = "Luz de Murther";
    const float ProfundidadCapaLejana = 4f;   // metros detrás del fondo principal

    // Capas traseras por GUID (los nombres de archivo llevan acentos). null = el nivel no tiene.
    static readonly string[] GuidCapaLejana =
    {
        "7cb191e7f42a25f43bfff583a5138d91",   // Fondo Nivel 1/Columnas del vacío nivel 1.png
        null,                                 // Nivel 2: las ventanas dan a negro (Vacío nivel 2 es negro liso)
        "db936233e5781e44a8378cb063ac0a2a",   // Fondo Nivel 3/Bosque del limbo nivel 3.png
    };

    struct LuzFondo
    {
        public string nombre;
        public Vector2 uv;          // posición en el dibujo (0-1, origen abajo a la izquierda)
        public float z;             // metros respecto al fondo principal (negativo = delante)
        public Color color;
        public float intensidad, rango, parpadeo;

        public LuzFondo(string nombre, float u, float v, float z, Color color, float intensidad, float rango, float parpadeo)
        {
            this.nombre = nombre; uv = new Vector2(u, v); this.z = z; this.color = color;
            this.intensidad = intensidad; this.rango = rango; this.parpadeo = parpadeo;
        }
    }

    static readonly Color Calida = new Color(1f, 0.72f, 0.42f);
    static readonly Color Fria = new Color(0.55f, 0.66f, 1f);

    static LuzFondo[] Luces(int nivel)
    {
        switch (nivel)
        {
            case 1:
                return new[]
                {
                    // Detrás del hueco central: iluminan las columnas del vacío, no el muro de delante.
                    new LuzFondo("Luz del Vacío Izquierda", 0.35f, 0.75f, 1.2f, Fria, 6f, 14f, 0f),
                    new LuzFondo("Luz del Vacío Derecha", 0.72f, 0.66f, 1.2f, Fria, 6f, 14f, 0f),
                };
            case 2:
                return new[]
                {
                    // Los dos faroles de la pared (centro del cristal en el dibujo): son la luz principal del nivel.
                    new LuzFondo("Farol Izquierdo", 0.100f, 0.473f, -1.5f, Calida, 9f, 13f, 0.18f),
                    new LuzFondo("Farol Derecho", 0.855f, 0.459f, -1.5f, Calida, 9f, 13f, 0.18f),
                    // Luz de luna que entra por las dos ventanas: marca los marcos y el muro de alrededor.
                    new LuzFondo("Luna Ventana Izquierda", 0.212f, 0.52f, -1.4f, Fria, 4.5f, 9f, 0f),
                    new LuzFondo("Luna Ventana Derecha", 0.733f, 0.50f, -1.4f, Fria, 4.5f, 9f, 0f),
                };
            case 3:
                return new[]
                {
                    // El vitral amarillo deja pasar luz; y luz fría sobre el bosque por el hueco del muro.
                    new LuzFondo("Luz del Vitral", 0.482f, 0.53f, -1.5f, new Color(0.9f, 0.88f, 0.45f), 9f, 13f, 0.06f),
                    new LuzFondo("Luz del Bosque", 0.49f, 0.80f, 1.5f, new Color(0.62f, 0.68f, 0.9f), 5f, 14f, 0f),
                };
        }
        return new LuzFondo[0];
    }

    [MenuItem("Juego/Iluminación de fondos/Aplicar a los tres niveles")]
    public static void AplicarATodos()
    {
        if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Iluminación de fondos",
                "Se vuelven a crear las luces del escenario, la capa trasera y los materiales de los fondos de Nivel1-3.\n\n" +
                "Se pierden los ajustes hechos a mano en esas luces y materiales.", "Aplicar", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        for (int n = 1; n <= 3; n++)
        {
            var escena = EditorSceneManager.OpenScene("Assets/Scenes/Nivel" + n + ".unity", OpenSceneMode.Single);
            Aplicar(escena, n);
            EditorSceneManager.SaveScene(escena);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Juego/Iluminación de fondos/Regenerar mapas de normales")]
    public static void RegenerarNormales()
    {
        for (int n = 1; n <= 3; n++)
        {
            foreach (var ruta in Ilustraciones(n)) GeneradorNormalesFondo.Generar(ruta);
        }
        Debug.Log("Mapas de normales regenerados en " + GeneradorNormalesFondo.Carpeta);
    }

    // Ilustraciones del nivel que llevan mapa de normales: el fondo de la escena y su capa trasera.
    static string[] Ilustraciones(int nivel)
    {
        var rutas = new System.Collections.Generic.List<string>();
        string frente = RutaFondoPrincipal(nivel);
        if (frente != null) rutas.Add(frente);
        string lejana = RutaCapaLejana(nivel);
        if (lejana != null) rutas.Add(lejana);
        return rutas.ToArray();
    }

    static string RutaFondoPrincipal(int nivel)
    {
        // El sprite del fondo se lee de la escena (si el equipo cambia el dibujo, el mapa se genera del nuevo).
        string escena = File.ReadAllText("Assets/Scenes/Nivel" + nivel + ".unity");
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Fondos" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta.StartsWith(GeneradorNormalesFondo.Carpeta)) continue;
            if (escena.Contains("guid: " + guid) && guid != GuidCapaLejana[nivel - 1]) return ruta;
        }
        return null;
    }

    static string RutaCapaLejana(int nivel)
    {
        string guid = GuidCapaLejana[nivel - 1];
        if (guid == null) return null;
        string ruta = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(ruta) ? null : ruta;
    }

    public static void Aplicar(Scene escena, int nivel)
    {
        var fondoGO = BuscarRaiz(escena, "Fondo");
        var camGO = BuscarRaiz(escena, "Main Camera");
        if (fondoGO == null || camGO == null) { Debug.LogError("Nivel" + nivel + ": falta Fondo o Main Camera."); return; }
        var fondo = fondoGO.GetComponent<SpriteRenderer>();
        string rutaFrente = AssetDatabase.GetAssetPath(fondo.sprite.texture);

        // Material del fondo principal
        fondo.sharedMaterial = MaterialFondo("Fondo_Nivel" + nivel, Normal(rutaFrente), false);
        EditorUtility.SetDirty(fondo);

        // Capa trasera con parallax
        var viejaCapa = BuscarRaiz(escena, NombreCapaLejana);
        if (viejaCapa != null) Object.DestroyImmediate(viejaCapa);
        string rutaLejana = RutaCapaLejana(nivel);
        if (rutaLejana != null)
        {
            var sprite = PrepararSprite(rutaLejana);
            var capa = new GameObject(NombreCapaLejana);
            SceneManager.MoveGameObjectToScene(capa, escena);
            capa.transform.localScale = fondoGO.transform.localScale;
            // Mismo lienzo que el fondo: se alinea por el centro de los límites.
            var pos = fondo.bounds.center + Vector3.forward * ProfundidadCapaLejana;
            capa.transform.position = pos;
            var sr = capa.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = fondo.sortingOrder - 10;
            sr.sharedMaterial = MaterialFondo("FondoLejano_Nivel" + nivel, Normal(rutaLejana), true);
            capa.transform.position += pos - sr.bounds.center;   // por si el pivote del sprite no es el centro
            var par = capa.AddComponent<CapaParallax>();
            par.camara = camGO.transform;
            par.referencia = fondo;
            par.factor = 0.3f;
            par.posicionBase = capa.transform.position;
        }

        // Luz principal (los niveles 2 y 3 no tenían ninguna luz)
        if (BuscarRaiz(escena, "Luz Direccional") == null)
        {
            var luzGO = new GameObject("Luz Direccional");
            SceneManager.MoveGameObjectToScene(luzGO, escena);
            luzGO.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            var luz = luzGO.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.05f;
            luz.color = new Color(1f, 0.98f, 0.95f);
        }

        // Luces del escenario
        var viejo = BuscarRaiz(escena, NombreGrupo);
        if (viejo != null) Object.DestroyImmediate(viejo);
        var grupo = new GameObject(NombreGrupo);
        SceneManager.MoveGameObjectToScene(grupo, escena);
        var b = fondo.bounds;
        foreach (var d in Luces(nivel))
        {
            var go = new GameObject(d.nombre);
            go.transform.SetParent(grupo.transform, false);
            go.transform.position = new Vector3(b.min.x + d.uv.x * b.size.x, b.min.y + d.uv.y * b.size.y,
                                                fondoGO.transform.position.z + d.z);
            CrearLuzPuntual(go, d.color, d.intensidad, d.rango);
            if (d.parpadeo > 0f) go.AddComponent<LuzParpadeo>().variacion = d.parpadeo;
        }

        // Luz que acompaña a Murther (solo el nivel 1 tiene a Murther por ahora)
        var murther = BuscarRaiz(escena, "Murther");
        if (murther != null)
        {
            var vieja = murther.transform.Find(NombreLuzMurther);
            if (vieja != null) Object.DestroyImmediate(vieja.gameObject);
            var go = new GameObject(NombreLuzMurther);
            go.transform.SetParent(murther.transform, false);
            // Entre Murther y el fondo, cerca del muro: el shader de Murther no usa luces puntuales,
            // así que solo ilumina el dibujo a su alrededor y descubre el relieve al caminar.
            // Cálida, como una vela: en el nivel 1 no hay ninguna fuente de luz dibujada y Murther la lleva consigo.
            go.transform.localPosition = new Vector3(0f, 1.6f, fondoGO.transform.position.z - 2.5f - murther.transform.position.z);
            var colorLuz = new Color(1f, 0.82f, 0.6f);
            CrearLuzPuntual(go, colorLuz, 10f, 12f);
            // La luz de personaje del shader de Murther toma el mismo tono (un poco menos saturado,
            // para que el casco siga leyéndose como hueso).
            var ctrl = murther.GetComponent<MurtherController>();
            if (ctrl != null)
            {
                ctrl.colorLuzTinta = Color.Lerp(colorLuz, Color.white, 0.55f);
                ctrl.intensidadLuzTinta = 1f;
                EditorUtility.SetDirty(ctrl);
            }
        }

        // Enemigos: el mismo shader sin relieve, para que la penumbra y las luces también caigan sobre ellos.
        foreach (var gen in escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GeneradorCorredores>(true)))
        {
            gen.materialSprite = MaterialSprites();
            EditorUtility.SetDirty(gen);
        }

        AgregarResplandor();
        EditorSceneManager.MarkSceneDirty(escena);
    }

    static Material MaterialSprites()
    {
        string ruta = CarpetaMateriales + "/Sprite_Iluminado.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null)
        {
            m = new Material(Shader.Find(ShaderFondo));
            AssetDatabase.CreateAsset(m, ruta);
        }
        m.SetTexture("_NormalMap", null);
        m.SetFloat("_Relieve", 0f);
        m.SetFloat("_Ambiente", 0.55f);
        m.SetColor("_ColorAmbiente", new Color(0.8f, 0.84f, 1f));
        m.SetFloat("_LuzPrincipal", 0.4f);
        m.SetFloat("_LuzPuntual", 1f);
        m.SetFloat("_Halo", 0f);
        m.SetFloat("_Brillo", 0f);
        m.SetFloat("_Translucidez", 0.7f);   // la luz de Murther queda detrás de ellos (entre Murther y el muro)
        m.SetColor("_Lejania", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Resplandor (bloom) en el perfil de post procesado de los niveles: solo lo que pasa de 1 (cristales de
    // los faroles, vitral, zonas muy cerca de una luz) brilla hacia afuera. Murther no llega a ese valor.
    static void AgregarResplandor()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/Niveles_PostProcesado.asset");
        if (perfil == null) return;
        if (!perfil.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))
        {
            bloom = perfil.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            AssetDatabase.AddObjectToAsset(bloom, perfil);
        }
        bloom.active = true;
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.7f);
        if (perfil.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var vineta)) vineta.intensity.Override(0.38f);
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
    }

    static void CrearLuzPuntual(GameObject go, Color color, float intensidad, float rango)
    {
        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = color;
        luz.intensity = intensidad;
        luz.range = rango;
        luz.shadows = LightShadows.None;
    }

    static Texture2D Normal(string rutaIlustracion)
    {
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(GeneradorNormalesFondo.RutaNormal(rutaIlustracion));
        return existente != null ? existente : GeneradorNormalesFondo.Generar(rutaIlustracion);
    }

    static Material MaterialFondo(string nombre, Texture2D normal, bool lejano)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaMateriales)) AssetDatabase.CreateFolder("Assets/Fondos", "Materiales");
        string ruta = CarpetaMateriales + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null)
        {
            m = new Material(Shader.Find(ShaderFondo));
            AssetDatabase.CreateAsset(m, ruta);
        }
        m.shader = Shader.Find(ShaderFondo);
        m.SetTexture("_NormalMap", normal);
        // Penumbra: ambiente bajo y frío, y la luz principal casi apagada. Lejos de las luces el dibujo queda
        // a menos de un tercio de su brillo; cerca de ellas sube a más del doble y toma su color.
        m.SetColor("_ColorAmbiente", new Color(0.72f, 0.78f, 1f));
        m.SetFloat("_Translucidez", 0f);
        if (lejano)
        {
            // Más oscura, fría y con menos relieve: se lee como algo lejano detrás del muro.
            m.SetFloat("_Relieve", 0.8f);
            m.SetFloat("_Ambiente", 0.22f);
            m.SetFloat("_LuzPrincipal", 0.08f);
            m.SetFloat("_Brillo", 0.15f);
            m.SetFloat("_Halo", 0.03f);
            m.SetColor("_Lejania", new Color(0.72f, 0.76f, 0.95f));
        }
        else
        {
            m.SetFloat("_Relieve", 1f);
            m.SetFloat("_Ambiente", 0.2f);
            m.SetFloat("_LuzPrincipal", 0.22f);
            m.SetFloat("_Brillo", 0.35f);
            m.SetFloat("_Halo", 0.1f);
            m.SetColor("_Lejania", Color.white);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // Las capas traseras se importaron como textura normal; para el SpriteRenderer tienen que ser sprite.
    static Sprite PrepararSprite(string ruta)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f;
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            var ajustes = new TextureImporterSettings();
            imp.ReadTextureSettings(ajustes);
            ajustes.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(ajustes);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    static GameObject BuscarRaiz(Scene escena, string nombre)
    {
        return escena.GetRootGameObjects().FirstOrDefault(g => g.name == nombre);
    }
}
