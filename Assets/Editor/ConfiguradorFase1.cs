using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fase 1: escala de los fondos de Nivel1-3, pantallas de carga (2 s) antes de cada nivel y
// Murther en Nivel1 (modelo con huesos, animaciones Idle/Salto, movimiento, doble salto y tinta).
// Menú: Juego > Fase 1 - Murther y escala de niveles (solo manual, pide confirmación).
public static class ConfiguradorFase1
{
    const string Carpeta = "Assets/Murther";
    const string ModeloRig = Carpeta + "/Modelos/Muther_rig_IK.fbx";
    const string AnimIdle = Carpeta + "/Modelos/Iddle.fbx";
    const string AnimSalto = Carpeta + "/Modelos/Jump.fbx";
    const string PantallasCarga = "Assets/Pantallas de carga/PantallaCarga";

    const float AlturaMundo = 12f;      // alto visible del nivel, en metros
    const float AlturaMurther = 1.95f;     // altura visible (silueta real) de Murther, en metros
    static readonly float[] SueloFrac = { 0.03f, 0.03f, 0.03f };   // suelo sobre el borde inferior del fondo

    // Antes se ejecutaba sola ([InitializeOnLoadMethod]) la primera vez que el proyecto se abría en cada
    // computadora (la marca vivía en EditorPrefs, que es local). Cada vez borraba y volvía a crear el
    // Animator Controller, los materiales y a Murther con GUIDs nuevos, y rehacía las pantallas de carga:
    // se perdían los ajustes hechos a mano y cada integrante subía cambios distintos. Ahora solo desde el menú.
    [MenuItem("Juego/Fase 1 - Murther y escala de niveles")]
    public static void Ejecutar()
    {
        if (!File.Exists(ModeloRig) || !File.Exists(AnimIdle) || !File.Exists(AnimSalto)
            || !File.Exists("Assets/Scenes/Nivel1.unity") || !File.Exists(PantallasCarga + "1.png"))
        {
            Debug.LogError("Fase 1: faltan el modelo, las animaciones, las pantallas de carga o Assets/Scenes/Nivel1.unity.");
            return;
        }
        if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Fase 1 - Murther y escala de niveles",
                "Se vuelven a crear Murther (Nivel1), el suelo y las paredes de los tres niveles, las pantallas de carga, " +
                "el Animator Controller y los materiales de Murther y de la tinta.\n\nSe pierden los ajustes hechos a mano en todo eso.",
                "Rehacer", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (var r in new[] { ModeloRig, AnimIdle, AnimSalto })
            AssetDatabase.ImportAsset(r, ImportAssetOptions.ForceSynchronousImport);
        // El FBX del personaje (Muther_rig_IK.fbx) no se modifica: se usa con su importación original.
        ConfigurarModelo(AnimIdle, "Idle", true);
        ConfigurarModelo(AnimSalto, "Salto", false);

        var controlador = CrearControlador();
        var matGota = CrearMaterialTinta("TintaGota", false);
        var matMancha = CrearMaterialTinta("TintaMancha", true);
        var fisica = CrearMaterialFisico();
        AssetDatabase.SaveAssets();

        for (int n = 1; n <= 3; n++)
        {
            var escena = EditorSceneManager.OpenScene("Assets/Scenes/Nivel" + n + ".unity", OpenSceneMode.Single);
            AjustarEscenario(escena, n);
            if (n == 1) ColocarMurther(escena, controlador, matGota, matMancha, fisica);
            IluminacionFondos.Aplicar(escena, n);    // después de Murther: la luz que lo acompaña es hija suya
            EditorSceneManager.SaveScene(escena);
        }

        for (int n = 1; n <= 3; n++) ReconstruirPantallaCarga(n);
        ConectarIniciarJuego();

        EditorSceneManager.OpenScene("Assets/Scenes/Nivel1.unity", OpenSceneMode.Single);
        Debug.Log("Fase 1 lista: abre Nivel1 y dale Play. Mover: A/D, flechas o stick. Saltar: Espacio o botón A (doble salto en el aire).");
    }

    // ───────────── Modelos y animaciones ─────────────

    static void ConfigurarModelo(string ruta, string nombreClip, bool bucle)
    {
        var imp = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (imp == null) { Debug.LogError("No se pudo importar " + ruta); return; }
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.importAnimation = nombreClip != null;
        if (nombreClip != null)
        {
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips) { c.name = nombreClip; c.loopTime = bucle; c.loopPose = bucle; }
            imp.clipAnimations = clips;
        }
        imp.SaveAndReimport();
    }

    static AnimationClip Clip(string ruta)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static AnimatorController CrearControlador()
    {
        var idle = Clip(AnimIdle);
        var salto = Clip(AnimSalto);
        if (idle == null || salto == null) { Debug.LogError("Faltan las animaciones Idle/Salto en los FBX."); return null; }

        string ruta = Carpeta + "/Murther.controller";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.DeleteAsset(Carpeta + "/Idle.controller");
        AssetDatabase.DeleteAsset(Carpeta + "/Salto.controller");
        var c = AnimatorController.CreateAnimatorControllerAtPath(ruta);
        c.AddParameter("Aire", AnimatorControllerParameterType.Bool);
        var sm = c.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle");
        sIdle.motion = idle;
        var sSalto = sm.AddState("Salto");
        sSalto.motion = salto;
        sm.defaultState = sIdle;

        var ida = sIdle.AddTransition(sSalto);
        ida.AddCondition(AnimatorConditionMode.If, 0f, "Aire");
        ida.hasExitTime = false;
        ida.duration = 0.05f;
        var vuelta = sSalto.AddTransition(sIdle);
        vuelta.AddCondition(AnimatorConditionMode.IfNot, 0f, "Aire");
        vuelta.hasExitTime = false;
        vuelta.duration = 0.08f;
        return c;
    }

    // ───────────── Tinta: texturas y materiales ─────────────

    static Material CrearMaterialTinta(string nombre, bool mancha)
    {
        string rutaTex = Carpeta + "/" + nombre + ".png";
        GenerarTextura(rutaTex, mancha);

        string rutaMat = Carpeta + "/" + nombre + ".mat";
        AssetDatabase.DeleteAsset(rutaMat);
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex);
        AssetDatabase.CreateAsset(mat, rutaMat);
        return mat;
    }

    // Gota: círculo con borde suave. Mancha: charco irregular. Núcleo casi negro, borde apenas más claro.
    static void GenerarTextura(string ruta, bool mancha)
    {
        int an = mancha ? 128 : 64, al = 64;
        var tex = new Texture2D(an, al, TextureFormat.RGBA32, false);
        var rng = new System.Random(mancha ? 7 : 3);
        float[] fase = new float[6];
        for (int i = 0; i < fase.Length; i++) fase[i] = (float)rng.NextDouble() * 6.28f;

        for (int y = 0; y < al; y++)
        {
            for (int x = 0; x < an; x++)
            {
                float nx = (x + 0.5f) / an * 2f - 1f;
                float ny = (y + 0.5f) / al * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float limite = 0.86f;
                if (mancha)
                {
                    float ang = Mathf.Atan2(ny, nx);
                    limite += 0.10f * Mathf.Sin(ang * 3f + fase[0]) + 0.07f * Mathf.Sin(ang * 5f + fase[1])
                              + 0.04f * Mathf.Sin(ang * 9f + fase[2]);
                }
                float a = Mathf.Clamp01((limite - r) / 0.12f);
                float borde = Mathf.Clamp01(1f - (limite - r) / 0.3f);
                var c = Color.Lerp(new Color(0.02f, 0.02f, 0.05f), new Color(0.16f, 0.16f, 0.22f), borde * borde);
                // Reflejo de tinta húmeda (arriba a la izquierda): sin él, las gotas negras desaparecían
                // sobre las zonas negras del fondo. El color va en la textura (MurtherTinta emite en blanco).
                float hx = (nx + (mancha ? 0.25f : 0.3f)) / (mancha ? 0.34f : 0.2f);
                float hy = (ny - (mancha ? 0.22f : 0.32f)) / (mancha ? 0.1f : 0.16f);
                float reflejo = Mathf.Clamp01((1f - Mathf.Sqrt(hx * hx + hy * hy)) / 0.45f) * (mancha ? 0.5f : 0.9f);
                c = Color.Lerp(c, new Color(0.86f, 0.87f, 0.95f), reflejo);
                c.a = a;
                tex.SetPixel(x, y, c);
            }
        }
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = TextureImporterType.Default;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }

    // Elipse suave y oscura (sombra de contacto).
    static Sprite GenerarSombra()
    {
        const string ruta = Carpeta + "/SombraPies.png";
        const int an = 128, al = 32;
        var tex = new Texture2D(an, al, TextureFormat.RGBA32, false);
        for (int y = 0; y < al; y++)
            for (int x = 0; x < an; x++)
            {
                float nx = (x + 0.5f) / an * 2f - 1f, ny = (y + 0.5f) / al * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Clamp01(1f - r), 0.8f) * 0.7f));
            }
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = TextureImporterType.Sprite;
        imp.spritePixelsPerUnit = 100f;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    static PhysicsMaterial2D CrearMaterialFisico()
    {
        string ruta = Carpeta + "/SinFriccion.physicsMaterial2D";
        AssetDatabase.DeleteAsset(ruta);
        var m = new PhysicsMaterial2D("SinFriccion") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ───────────── Escenario: escala, cámara, suelo ─────────────

    static void AjustarEscenario(Scene escena, int nivel)
    {
        var fondoGO = BuscarRaiz(escena, "Fondo");
        var camGO = BuscarRaiz(escena, "Main Camera");
        if (fondoGO == null || camGO == null) { Debug.LogError("Nivel" + nivel + ": falta Fondo o Main Camera."); return; }

        var sr = fondoGO.GetComponent<SpriteRenderer>();
        float escala = AlturaMundo / sr.sprite.bounds.size.y;
        fondoGO.transform.localScale = new Vector3(escala, escala, 1f);
        float suelo = SueloFrac[nivel - 1] * AlturaMundo;
        fondoGO.transform.position = new Vector3(0f, AlturaMundo * 0.5f - suelo, 10f);
        sr.sortingOrder = -100;

        var viejo = camGO.GetComponent<AjustarCamaraAFondo>();
        if (viejo != null) Object.DestroyImmediate(viejo);
        var cam = camGO.GetComponent<CamaraNivel>();
        if (cam == null) cam = camGO.AddComponent<CamaraNivel>();
        cam.fondo = sr;
        cam.alturaMundo = AlturaMundo;
        cam.Actualizar(true);

        // Suelo y paredes invisibles
        foreach (var n in new[] { "Suelo", "ParedIzquierda", "ParedDerecha" })
        {
            var existente = BuscarRaiz(escena, n);
            if (existente != null) Object.DestroyImmediate(existente);
        }
        var b = sr.bounds;
        CrearCaja("Suelo", new Vector3(b.center.x, -1f, 0f), new Vector2(b.size.x + 6f, 2f));
        // Las paredes quedan 0.7 m dentro del borde del fondo: el casco de Murther es más ancho que su
        // collider y, con la pared justo en el borde, la mitad de él salía del cuadro al llegar a la orilla.
        const float margen = 0.7f;
        CrearCaja("ParedIzquierda", new Vector3(b.min.x - 0.5f + margen, AlturaMundo * 0.5f, 0f), new Vector2(1f, AlturaMundo * 3f));
        CrearCaja("ParedDerecha", new Vector3(b.max.x + 0.5f - margen, AlturaMundo * 0.5f, 0f), new Vector2(1f, AlturaMundo * 3f));

        AplicarPostProcesado(escena);
    }

    // ───────────── Post procesado de los niveles ─────────────

    const string RutaPerfilPost = "Assets/Settings/Niveles_PostProcesado.asset";

    // Viñeta y grano de papel: el grano cae igual sobre el dibujo y sobre Murther (3D), así los dos
    // parecen impresos en la misma hoja; la viñeta oscurece las orillas como en el menú.
    // No se usa tonemapping ni color grading: cambiarían los colores del dibujo de los fondos.
    public static void AplicarPostProcesado(Scene escena)
    {
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaPerfilPost);
        if (perfil == null)
        {
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, RutaPerfilPost);
            var vineta = perfil.Add<Vignette>(true);
            vineta.intensity.value = 0.3f;
            vineta.smoothness.value = 0.5f;
            vineta.color.value = Color.black;
            var grano = perfil.Add<FilmGrain>(true);
            grano.type.value = FilmGrainLookup.Thin2;
            grano.intensity.value = 0.18f;
            grano.response.value = 0.75f;
            foreach (var c in perfil.components) AssetDatabase.AddObjectToAsset(c, perfil);
            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();
        }

        var volGO = BuscarRaiz(escena, "Volumen Global");
        if (volGO == null) { volGO = new GameObject("Volumen Global"); SceneManager.MoveGameObjectToScene(volGO, escena); }
        var vol = volGO.GetComponent<Volume>();
        if (vol == null) vol = volGO.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = perfil;

        var camGO = BuscarRaiz(escena, "Main Camera");
        if (camGO != null)
        {
            var datos = camGO.GetComponent<Camera>().GetUniversalAdditionalCameraData();
            datos.renderPostProcessing = true;    // sin esto la cámara ignora cualquier Volume
            EditorUtility.SetDirty(datos);
        }
    }

    static void CrearCaja(string nombre, Vector3 pos, Vector2 tam)
    {
        var go = new GameObject(nombre);
        go.transform.position = pos;
        go.AddComponent<BoxCollider2D>().size = tam;
    }

    static GameObject BuscarRaiz(Scene escena, string nombre)
    {
        return escena.GetRootGameObjects().FirstOrDefault(g => g.name == nombre);
    }

    // ───────────── Murther ─────────────

    static void ColocarMurther(Scene escena, AnimatorController controlador,
                               Material matGota, Material matMancha, PhysicsMaterial2D fisica)
    {
        foreach (var n in new[] { "Murther", "Luz Direccional" })
        {
            var viejo = BuscarRaiz(escena, n);
            if (viejo != null) Object.DestroyImmediate(viejo);
        }

        // Luz para el modelo 3D
        var luzGO = new GameObject("Luz Direccional");
        // Desde el frente y arriba (simétrica): Murther se ve igual hacia los dos lados. Con 1.3 de intensidad,
        // luz lateral y ambiente plano de 0.55 el casco se quemaba en blanco puro y perdía todo el volumen.
        luzGO.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
        var luz = luzGO.AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 1.05f;
        luz.color = new Color(1f, 0.98f, 0.95f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.4f, 0.4f, 0.432f);
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.28f, 0.302f);
        RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.108f);

        var fondo = BuscarRaiz(escena, "Fondo").GetComponent<SpriteRenderer>();

        var raiz = new GameObject("Murther");
        raiz.transform.position = new Vector3(fondo.bounds.min.x + 3f, 0.3f, 0f);   // pivote en los pies

        var rb = raiz.AddComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var cap = raiz.AddComponent<CapsuleCollider2D>();
        cap.direction = CapsuleDirection2D.Vertical;
        cap.size = new Vector2(1.2f, AlturaMurther * 0.95f);
        cap.offset = new Vector2(0f, cap.size.y * 0.5f);      // la base del collider queda en los pies
        cap.sharedMaterial = fisica;

        var visual = new GameObject("Visual").transform;
        visual.SetParent(raiz.transform, false);

        // Modelo con huesos + animaciones de Iddle/Jump + copia de controles a huesos
        GameObject modelo = null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloRig);
        if (prefab != null)
        {
            modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, visual);
            modelo.name = "Modelo";
            modelo.transform.localPosition = Vector3.zero;
            modelo.transform.localRotation = Quaternion.identity;
            foreach (var c in modelo.GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        bool tieneMalla = modelo != null && modelo.GetComponentsInChildren<Renderer>(true).Length > 0;
        if (!tieneMalla)
        {
            Debug.LogWarning("Muther_rig_IK.fbx no tiene malla visible. Uso una cápsula provisional.");
            if (modelo != null) Object.DestroyImmediate(modelo);
            modelo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            modelo.name = "Modelo";
            Object.DestroyImmediate(modelo.GetComponent<Collider>());
            modelo.transform.SetParent(visual, false);
            modelo.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
        }

        Animator animator = null;
        Transform cabeza = null;
        if (tieneMalla)
        {
            animator = modelo.GetComponent<Animator>();
            if (animator == null) animator = modelo.AddComponent<Animator>();
            animator.runtimeAnimatorController = controlador;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            modelo.AddComponent<RigControlesAHuesos>();
            cabeza = BuscarCabeza(modelo.transform);
        }

        if (tieneMalla)
        {
            AplicarMateriales(modelo);
            foreach (var smr in modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        }

        // Escalar a AlturaMurther y apoyar los pies en y = 0, centrado en x/z
        var b = Medir(modelo);
        float escalaModelo = b.size.y > 0.0001f ? AlturaMurther / b.size.y : 1f;
        modelo.transform.localScale = modelo.transform.localScale * escalaModelo;
        b = Medir(modelo);
        modelo.transform.position += new Vector3(raiz.transform.position.x - b.center.x,
                                                 raiz.transform.position.y - b.min.y,
                                                 raiz.transform.position.z - b.center.z);

        // Los límites de las mallas con huesos están inflados: se afina con la silueta real renderizada,
        // para que la altura visible sea AlturaMurther y la planta de los pies quede justo en el piso.
        if (tieneMalla && MedirSilueta(escena, raiz, out float abajo, out float arriba) && arriba - abajo > 0.1f)
        {
            modelo.transform.localScale *= AlturaMurther / (arriba - abajo);
            if (MedirSilueta(escena, raiz, out abajo, out arriba))
                modelo.transform.position += Vector3.up * (raiz.transform.position.y - abajo + 0.03f);   // +3 cm: la pose de reposo baja un poco
        }

        var ctrl = raiz.AddComponent<MurtherController>();
        ctrl.visual = visual;
        ctrl.giroBase = 0f;     // el giro se calibra solo (ojos respecto a la cabeza)
        ctrl.giroHaciaCamara = 35f;   // 3/4: de perfil estricto la cara no se ve
        ctrl.animator = animator;
        ctrl.cabezaIdle = cabeza;
        ctrl.cabezaSalto = cabeza;

        // Sombra de contacto bajo los pies: refuerza que Murther está apoyado en el piso.
        var sombra = new GameObject("SombraPies");
        sombra.transform.SetParent(raiz.transform, false);
        sombra.transform.localPosition = new Vector3(0f, 0.03f, 0.15f);
        sombra.transform.localScale = new Vector3(1.7f, 1f, 1f);
        var sombraSr = sombra.AddComponent<SpriteRenderer>();
        sombraSr.sprite = GenerarSombra();
        sombraSr.sortingOrder = 1;

        var tinta = raiz.AddComponent<MurtherTinta>();
        tinta.materialGota = matGota;
        tinta.materialMancha = matMancha;

        var cam = BuscarRaiz(escena, "Main Camera").GetComponent<CamaraNivel>();
        cam.objetivo = raiz.transform;
        cam.Actualizar(true);

        Debug.Log("Murther colocado. Cabeza: " + (cabeza != null ? cabeza.name : "no encontrada (se estira el cuerpo)")
                  + " | Animator: " + (animator != null ? "sí" : "no") + " | Altura medida: " + b.size.y.ToString("0.00")
                  + "\nPartes: " + ListarPartes(modelo));
    }

    // El FBX trae los materiales con color base blanco; se reemplazan por materiales de tinta según su nombre.
    static void AplicarMateriales(GameObject modelo)
    {
        if (!AssetDatabase.IsValidFolder(Carpeta + "/Materiales")) AssetDatabase.CreateFolder(Carpeta, "Materiales");
        var cache = new System.Collections.Generic.Dictionary<string, Material>();
        foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string nombre = mats[i] != null ? mats[i].name : "blanco";
                if (!cache.TryGetValue(nombre, out var m)) cache[nombre] = m = MaterialMurther(nombre);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    static Material MaterialMurther(string nombre)
    {
        string ruta = Carpeta + "/Materiales/Murther_" + nombre + ".mat";
        AssetDatabase.DeleteAsset(ruta);
        var m = new Material(Shader.Find(ShaderTinta));
        ConfigurarMaterialTinta(m, nombre);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    public const string ShaderTinta = "Five Star/Murther Tinta";

    // Valores del shader de tinta según la parte del modelo (el nombre del material del FBX).
    // Antes se usaba URP/Lit: un sombreado suave "de plástico" que no combinaba con los fondos dibujados.
    public static void ConfigurarMaterialTinta(Material m, string nombre)
    {
        string l = nombre.ToLowerInvariant();
        bool negro = l.Contains("black") || l.Contains("negro");
        m.shader = Shader.Find(ShaderTinta);
        if (l.Contains("shi"))
        {
            // Brillos de los ojos: blancos planos, sin sombra ni contorno.
            m.SetColor("_BaseColor", new Color(0.97f, 0.97f, 0.95f));
            m.SetColor("_ColorSombra", Color.white);
            m.SetFloat("_GrosorContorno", 0f);
        }
        else if (negro && l.Contains("abs"))
        {
            // Cuencas de los ojos: negro absoluto, sin contorno (ya están hundidas en el casco).
            m.SetColor("_BaseColor", new Color(0.015f, 0.015f, 0.02f));
            m.SetColor("_ColorSombra", Color.white);
            m.SetFloat("_GrosorContorno", 0f);
        }
        else if (negro)
        {
            // Cuerpo, manos y pies: tinta negra con borde de luz y un brillo duro de tinta húmeda,
            // para que la silueta no se pierda contra las zonas negras del fondo.
            m.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.12f));
            m.SetFloat("_Umbral", 0.7f);
            m.SetColor("_ColorSombra", new Color(0.45f, 0.45f, 0.5f));
            m.SetColor("_ColorBorde", new Color(0.3f, 0.31f, 0.4f));
            m.SetFloat("_AnchoBorde", 0.25f);
            m.SetColor("_ColorBrillo", new Color(0.5f, 0.5f, 0.58f));
            m.SetFloat("_TamBrillo", 0.025f);
            m.SetFloat("_GrosorContorno", 0.025f);
        }
        else
        {
            // Casco y dientes: hueso claro, sombra malva como la del escenario y trama de tinta.
            m.SetColor("_BaseColor", new Color(0.84f, 0.85f, 0.87f));
            m.SetColor("_ColorSombra", new Color(0.5f, 0.42f, 0.48f));   // malva, como las sombras del fondo
            m.SetFloat("_Umbral", 0.7f);
            m.SetFloat("_Trama", 0.75f);
            m.SetFloat("_TramaEscala", 7f);      // ~13 px entre líneas en 1080p: sin moiré
            m.SetFloat("_TramaGrosor", 0.3f);
            m.SetFloat("_TramaCruzada", 0.6f);
            m.SetFloat("_UmbralProfundo", 0.42f);
            m.SetFloat("_GrosorContorno", 0.025f);
        }
    }

    // Renderiza solo a Murther sobre fondo magenta con una cámara ortográfica de frente y devuelve
    // la altura (y de mundo) más baja y más alta de la silueta.
    static bool MedirSilueta(Scene escena, GameObject raiz, out float abajo, out float arriba)
    {
        abajo = arriba = 0f;
        var ocultos = new System.Collections.Generic.List<GameObject>();
        foreach (var g in escena.GetRootGameObjects())
            if (g != raiz && g.activeSelf) { g.SetActive(false); ocultos.Add(g); }

        var camGO = new GameObject("CamaraSilueta");
        RenderTexture rt = null; Texture2D tex = null; bool ok = false;
        try
        {
            var b = Medir(raiz);
            float mitad = Mathf.Max(b.size.y, b.size.x) * 0.75f;
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = mitad;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(1f, 0f, 1f, 1f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
            cam.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - 50f);
            cam.transform.rotation = Quaternion.identity;

            const int N = 768;
            rt = new RenderTexture(N, N, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            var previo = RenderTexture.active;
            RenderTexture.active = rt;
            tex = new Texture2D(N, N, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, N, N), 0, 0);
            RenderTexture.active = previo;

            var px = tex.GetPixels32();
            int minFila = N, maxFila = -1;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var c = px[y * N + x];
                    if (c.r > 250 && c.g < 5 && c.b > 250) continue;     // magenta = fondo
                    if (y < minFila) minFila = y;
                    if (y > maxFila) maxFila = y;
                }
            if (maxFila >= 0)
            {
                float porMetro = N / (2f * mitad);
                float baseY = cam.transform.position.y - mitad;
                abajo = baseY + minFila / porMetro;
                arriba = baseY + (maxFila + 1) / porMetro;
                ok = true;
            }
        }
        finally
        {
            if (camGO != null) camGO.GetComponent<Camera>().targetTexture = null;
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (tex != null) Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
            foreach (var g in ocultos) g.SetActive(true);
        }
        return ok;
    }

    static Bounds Medir(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Hueso de la cabeza (cabeza_jnt): la malla de la cabeza va ligada a él.
    static Transform BuscarCabeza(Transform modelo)
    {
        Transform candidato = null;
        foreach (var t in modelo.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (!(n.Contains("cabeza") || n.Contains("head") || n.Contains("casco") || n.Contains("helmet"))) continue;
            if (t.GetComponent<SkinnedMeshRenderer>() != null) continue;
            if (n.EndsWith("_jnt") && !n.Contains("fin")) return t;
            if (candidato == null && t.GetComponentInChildren<Renderer>(true) != null) candidato = t;
        }
        return candidato;
    }

    static string ListarPartes(GameObject modelo)
    {
        var sb = new StringBuilder();
        foreach (var t in modelo.GetComponentsInChildren<Transform>(true).Take(45))
            sb.Append(t.name).Append(t.GetComponent<SkinnedMeshRenderer>() != null ? "[skin]" : "").Append(", ");
        return sb.ToString();
    }

    // ───────────── Pantallas de carga y flujo ─────────────

    static void ReconstruirPantallaCarga(int nivel)
    {
        string rutaImg = PantallasCarga + nivel + ".png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(rutaImg);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        var lienzo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        lienzo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = lienzo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var imgGO = new GameObject("Pantalla", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(lienzo.transform, false);
        var rt = (RectTransform)imgGO.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = imgGO.GetComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(rutaImg);
        img.preserveAspect = true;
        img.color = Color.white;

        var pc = new GameObject("Controlador").AddComponent<PantallaControles>();
        pc.nivel = nivel;
        pc.escenaSiguiente = "Nivel" + nivel;
        pc.duracion = 2f;

        EditorSceneManager.SaveScene(escena, "Assets/Scenes/Controles_Nivel" + nivel + ".unity");
    }

    // Iniciar Juego: fundido a la cinemática de cómic (1:30, con botón de saltar), luego pantalla de carga y Nivel 1.
    static void ConectarIniciarJuego()
    {
        var escena = EditorSceneManager.OpenScene("Assets/Scenes/MenuPrincipal.unity", OpenSceneMode.Single);
        var menu = Object.FindFirstObjectByType<MenuPrincipalUI>();
        if (menu != null)
        {
            menu.escenaCinematica = "Cinematica";
            EditorUtility.SetDirty(menu);
            EditorSceneManager.SaveScene(escena);
        }
    }
}
