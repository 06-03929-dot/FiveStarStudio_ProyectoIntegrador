using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Video;

// Genera las escenas del flujo (logos, menú, créditos, cinemática, controles, niveles)
// y las registra en Build Settings. Menú: Juego > Construir flujo de escenas.
// Se ejecuta sola una vez si todavía no existe Assets/Scenes/MenuPrincipal.unity.
public static class ConstruirFlujoDelJuego
{
    const string CarpetaEscenas = "Assets/Scenes";

    static readonly string[] Fondos =
    {
        "Assets/Fondos/Fondo Nivel 1/Fondo nivel 1.png",
        "Assets/Fondos/Fondo Nivel 2/Nivel 2.png",
        "Assets/Fondos/Fondo Nivel 3/Nivel 3.png",
    };

    const string RutaVideoLogo = "Assets/Animacion de logo/Animacion de logo.mp4";
    const string RutaRenderTexture = "Assets/Animacion de logo/LogoRT.renderTexture";
    const string RutaImagenMenu = "Assets/Menu principal/Murther menu.png";
    static readonly Color ColorFondoVideo = new Color32(13, 13, 18, 255);   // fondo del video del logo
    // true: "Iniciar Juego" entra directo al Nivel 1. false: cinemática y pantalla de controles antes.
    const bool IniciarVaAlNivel1 = false;
    const int VersionFlujo = 6; // súbela para que el generador se vuelva a ejecutar solo

    static readonly Color Dorado = new Color(1f, 0.82f, 0.35f);
    static readonly Color Gris = new Color(0.82f, 0.82f, 0.86f);
    static readonly Color Negro = new Color(0.02f, 0.02f, 0.04f);

    [InitializeOnLoadMethod]
    static void AutoConstruir()
    {
        EditorApplication.delayCall += IntentarConstruir;
        // Si la recarga ocurrió durante Play, se reintenta al volver al modo edición.
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += IntentarConstruir;
        };
    }

    static void IntentarConstruir()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        string clave = "FlujoVersion_" + PlayerSettings.productGUID;
        if (File.Exists(CarpetaEscenas + "/MenuPrincipal.unity") && EditorPrefs.GetInt(clave, 1) >= VersionFlujo) return;
        if (Fondos.Any(f => !File.Exists(f)) || !File.Exists(RutaVideoLogo) || !File.Exists(RutaImagenMenu)) return;
        Construir();
        EditorPrefs.SetInt(clave, VersionFlujo);
    }

    [MenuItem("Juego/Construir flujo de escenas")]
    public static void Construir()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder(CarpetaEscenas)) AssetDatabase.CreateFolder("Assets", "Scenes");

        foreach (var f in Fondos) PrepararSprite(f);
        PrepararSprite(RutaImagenMenu);

        var rutas = new List<string>
        {
            ConstruirMenu(),
            ConstruirCinematica(),
        };
        for (int n = 1; n <= 3; n++)
        {
            rutas.Add(ConstruirControles(n));
            rutas.Add(ConstruirNivel(n));
        }

        EditorBuildSettings.scenes = rutas.Select(r => new EditorBuildSettingsScene(r, true)).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(rutas[0]);
        Debug.Log("Flujo construido: " + rutas.Count + " escenas en Build Settings. Abre MenuPrincipal y dale Play.");
    }

    // ───────────── Sprites ─────────────

    static void PrepararSprite(string ruta)
    {
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100f;
        imp.mipmapEnabled = false;
        imp.maxTextureSize = 4096;
        imp.SaveAndReimport();
    }

    // Sombra de profundidad: transparente al centro y oscura hacia los bordes.
    static Sprite GenerarVineta()
    {
        const string ruta = "Assets/Menu principal/SombraBordes.png";
        const int an = 512, al = 288;
        var tex = new Texture2D(an, al, TextureFormat.RGBA32, false);
        for (int y = 0; y < al; y++)
        {
            for (int x = 0; x < an; x++)
            {
                float nx = (x + 0.5f) / an * 2f - 1f;
                float ny = (y + 0.5f) / al * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Pow(Mathf.Clamp01((r - 0.5f) / 0.9f), 1.6f) * 0.95f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = TextureImporterType.Sprite;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    // ───────────── Escenas ─────────────

    static string ConstruirMenu()
    {
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CrearCamaraNegra();

        var musica = new GameObject("MusicaMenu").AddComponent<AudioSource>();
        musica.loop = true;               // asignar aquí el clip de la canción del menú
        musica.playOnAwake = false;
        musica.spatialBlend = 0f;

        var lienzo = CrearCanvas();
        var ctrl = new GameObject("Controlador");
        var menu = ctrl.AddComponent<MenuPrincipalUI>();
        var intro = ctrl.AddComponent<IntroLogos>();
        menu.intro = intro;
        menu.musicaMenu = musica;
        menu.escenaCinematica = IniciarVaAlNivel1 ? "Nivel1" : "Cinematica";

        Imagen("Fondo", lienzo, Negro).rectTransform.Estirar();
        var arte = Imagen("ImagenMenu", lienzo, Color.white);
        arte.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RutaImagenMenu);
        arte.raycastTarget = false;
        arte.rectTransform.Estirar();
        var ajuste = arte.gameObject.AddComponent<AspectRatioFitter>();   // cubre toda la pantalla
        ajuste.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        ajuste.aspectRatio = arte.sprite != null ? arte.sprite.rect.width / arte.sprite.rect.height : 16f / 9f;
        var velo = Imagen("Velo", lienzo, new Color(0f, 0f, 0f, 0.35f));
        velo.raycastTarget = false;
        velo.rectTransform.Estirar();
        var vineta = Imagen("SombraBordes", lienzo, Color.white);
        vineta.sprite = GenerarVineta();
        vineta.raycastTarget = false;
        vineta.rectTransform.Estirar();

        // Menú
        var panelMenu = Rect("PanelMenu", lienzo); panelMenu.Estirar();
        Texto("Titulo", panelMenu, TextosJuego.TituloJuego, 96, Dorado, TextAnchor.MiddleCenter, new Vector2(0, 300), new Vector2(1600, 140), FontStyle.Bold);
        var linea = Imagen("Linea", panelMenu, new Color(Dorado.r, Dorado.g, Dorado.b, 0.6f));
        linea.rectTransform.Fijar(new Vector2(0, 220), new Vector2(900, 3));
        BotonMenu(panelMenu, "Iniciar", "Iniciar Juego", new Vector2(0, 60), new Vector2(560, 90), 52, menu.IniciarJuego);
        BotonMenu(panelMenu, "Creditos", "Créditos", new Vector2(0, -60), new Vector2(560, 90), 52, menu.AbrirCreditos);
        BotonMenu(panelMenu, "Cerrar", "Cerrar Juego", new Vector2(0, -180), new Vector2(560, 90), 52, menu.PedirSalir);
        menu.panelMenu = panelMenu.gameObject;

        // Créditos
        var panelCred = Rect("PanelCreditos", lienzo); panelCred.Estirar();
        Texto("Titulo", panelCred, TextosJuego.TituloCreditos, 80, Dorado, TextAnchor.MiddleCenter, new Vector2(0, 360), new Vector2(1200, 120), FontStyle.Bold);
        Texto("Info", panelCred, TextosJuego.Creditos, 44, Gris, TextAnchor.MiddleCenter, new Vector2(0, 60), new Vector2(1400, 600));
        BotonMenu(panelCred, "Volver", "Volver", new Vector2(0, -400), new Vector2(420, 90), 52, menu.VolverDeCreditos);
        menu.panelCreditos = panelCred.gameObject;

        // Ventana emergente de salida
        var panelSalir = Rect("PanelSalir", lienzo); panelSalir.Estirar();
        Imagen("Oscurecer", panelSalir, new Color(0, 0, 0, 0.8f)).rectTransform.Estirar();
        var caja = Imagen("Caja", panelSalir, new Color(0.08f, 0.07f, 0.12f, 1f));
        caja.rectTransform.Fijar(Vector2.zero, new Vector2(1100, 460));
        Texto("Pregunta", caja.transform, TextosJuego.PreguntaSalir, 48, Gris, TextAnchor.MiddleCenter, new Vector2(0, 90), new Vector2(980, 160));
        BotonMenu(caja.transform, "Si", "Sí, salir", new Vector2(-260, -100), new Vector2(460, 90), 48, menu.ConfirmarSalir);
        BotonMenu(caja.transform, "No", "Cancelar", new Vector2(260, -100), new Vector2(460, 90), 48, menu.CancelarSalir);
        menu.panelSalir = panelSalir.gameObject;

        // Intro de logos (encima de todo)
        var panelIntro = Imagen("PanelIntro", lienzo, Color.black);
        panelIntro.rectTransform.Estirar();
        panelIntro.color = ColorFondoVideo;
        var grupoIntro = panelIntro.gameObject.AddComponent<CanvasGroup>();
        intro.panelNegro = grupoIntro;

        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RutaRenderTexture);
        if (rt == null)
        {
            rt = new RenderTexture(1280, 720, 0);
            AssetDatabase.CreateAsset(rt, RutaRenderTexture);
        }
        var pantalla = Rect("Video", panelIntro.transform);
        pantalla.Estirar();
        var raw = pantalla.gameObject.AddComponent<RawImage>();
        raw.texture = rt;
        raw.raycastTarget = false;
        pantalla.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        pantalla.GetComponent<AspectRatioFitter>().aspectRatio = 16f / 9f;
        intro.grupoVideo = pantalla.gameObject.AddComponent<CanvasGroup>();

        var vp = ctrl.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.isLooping = false;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        vp.audioOutputMode = VideoAudioOutputMode.Direct;
        vp.clip = AssetDatabase.LoadAssetAtPath<VideoClip>(RutaVideoLogo);
        intro.video = vp;

        return Guardar(escena, "MenuPrincipal");
    }

    static string ConstruirCinematica()
    {
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CrearCamaraNegra();
        var lienzo = CrearCanvas();

        var cine = new GameObject("Controlador").AddComponent<CinematicaComic>();
        cine.duracionTotal = 90f;
        cine.escenaSiguiente = "Controles_Nivel1";

        Imagen("Fondo", lienzo, new Color(0.06f, 0.06f, 0.08f)).rectTransform.Estirar();
        var panel = Imagen("ImagenPanel", lienzo, Color.white);
        panel.rectTransform.Estirar();
        panel.preserveAspect = true;
        panel.enabled = false;
        cine.imagenPanel = panel;
        cine.textoPanel = Texto("TextoPanel", lienzo, "", 80, Gris, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1600, 300));
        cine.textoTiempo = Texto("Tiempo", lienzo, "1:30", 36, new Color(0.6f, 0.6f, 0.66f), TextAnchor.MiddleLeft, new Vector2(-800, -480), new Vector2(300, 60));
        BotonMenu(lienzo, "Saltar", "Saltar >>", new Vector2(700, -470), new Vector2(420, 80), 44, cine.Saltar);

        return Guardar(escena, "Cinematica");
    }

    static string ConstruirControles(int nivel)
    {
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CrearCamaraNegra();
        var lienzo = CrearCanvas(false);

        Imagen("Fondo", lienzo, Color.black).rectTransform.Estirar();
        var pc = new GameObject("Controlador").AddComponent<PantallaControles>();
        pc.nivel = nivel;
        pc.escenaSiguiente = "Nivel" + nivel;
        pc.duracion = 2f;
        pc.textoTitulo = Texto("Nivel", lienzo, "", 90, Dorado, TextAnchor.MiddleCenter, new Vector2(0, 380), new Vector2(1200, 140), FontStyle.Bold);
        pc.textoControles = Texto("Controles", lienzo, "", 48, Gris, TextAnchor.MiddleCenter, new Vector2(0, 90), new Vector2(1200, 340));
        pc.textoFrase = Texto("Frase", lienzo, "", 44, Color.white, TextAnchor.MiddleCenter, new Vector2(0, -290), new Vector2(1500, 260), FontStyle.Italic);

        return Guardar(escena, "Controles_Nivel" + nivel);
    }

    static string ConstruirNivel(int nivel)
    {
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var fondoGO = new GameObject("Fondo");
        fondoGO.transform.position = new Vector3(0f, 0f, 10f);     // detrás en Z
        var sr = fondoGO.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Fondos[nivel - 1]);
        sr.sortingOrder = -100;

        var cam = CrearCamaraNegra();
        var ajuste = cam.gameObject.AddComponent<AjustarCamaraAFondo>();
        ajuste.fondo = sr;
        ajuste.Encuadrar();

        return Guardar(escena, "Nivel" + nivel);
    }

    static string Guardar(UnityEngine.SceneManagement.Scene escena, string nombre)
    {
        string ruta = CarpetaEscenas + "/" + nombre + ".unity";
        EditorSceneManager.SaveScene(escena, ruta);
        return ruta;
    }

    // ───────────── Ayudantes de UI ─────────────

    static Camera CrearCamaraNegra()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        go.tag = "MainCamera";
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        go.transform.position = new Vector3(0f, 0f, -10f);
        return cam;
    }

    static Transform CrearCanvas(bool eventSystem = true)
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (eventSystem)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        return go.transform;
    }

    static RectTransform Rect(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    static Image Imagen(string nombre, Transform padre, Color color)
    {
        var img = Rect(nombre, padre).gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static Text Texto(string nombre, Transform padre, string contenido, int tamano, Color color,
                      TextAnchor alineacion, Vector2 pos, Vector2 tam, FontStyle estilo = FontStyle.Normal)
    {
        var r = Rect(nombre, padre);
        r.Fijar(pos, tam);
        var t = r.gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = contenido;
        t.fontSize = tamano;
        t.fontStyle = estilo;
        t.color = color;
        t.alignment = alineacion;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Button BotonMenu(Transform padre, string nombre, string etiqueta, Vector2 pos, Vector2 tam, int fuente, UnityAction accion)
    {
        // La imagen raíz es el borde dorado; el relleno oscuro queda inset 4 px encima.
        var img = Imagen("Boton " + nombre, padre, new Color(Dorado.r, Dorado.g, Dorado.b, 0.6f));
        img.rectTransform.Fijar(pos, tam);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.None;

        var sombra = img.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.7f);
        sombra.effectDistance = new Vector2(6, -6);

        var relleno = Imagen("Relleno", img.transform, new Color(0.03f, 0.03f, 0.06f, 0.78f));
        relleno.raycastTarget = false;
        relleno.rectTransform.Estirar();
        relleno.rectTransform.offsetMin = new Vector2(4, 4);
        relleno.rectTransform.offsetMax = new Vector2(-4, -4);

        var texto = Texto("Texto", img.transform, etiqueta, fuente, Color.white, TextAnchor.MiddleCenter, Vector2.zero, tam, FontStyle.Bold);
        var contorno = texto.gameObject.AddComponent<Outline>();
        contorno.effectColor = Color.black;
        contorno.effectDistance = new Vector2(2, -2);

        var res = img.gameObject.AddComponent<BotonResaltado>();
        res.texto = texto;
        res.borde = img;
        res.relleno = relleno;

        UnityEventTools.AddPersistentListener(b.onClick, accion);
        return b;
    }

    static void Estirar(this RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void Fijar(this RectTransform r, Vector2 pos, Vector2 tam)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = tam;
    }
}
