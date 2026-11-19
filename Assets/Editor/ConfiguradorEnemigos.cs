using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Enemigos 2D (secuencias de imágenes): configura los PNG como sprites, crea una animación por
// carpeta, un controlador y un prefab por enemigo (sin comportamiento todavía).
// Menú: Juego > Configurar enemigos 2D. Es repetible.
public static class ConfiguradorEnemigos
{
    const string Raiz = "Assets/Enemigos";
    const string Prefabs = "Assets/Prefabs/Enemigos";
    const float Fps = 24f;

    class Enemigo
    {
        public string nombre;
        public string[] estados;        // subcarpetas = animaciones
        public float altura;            // altura del personaje en el juego, en metros
        public int capa;                // orden de dibujo
    }

    static readonly Enemigo[] Enemigos =
    {
        new Enemigo { nombre = "Corredor", estados = new[] { "Derecha", "Izquierda" }, altura = 1.6f, capa = 5 },
        new Enemigo { nombre = "Arquero",  estados = new[] { "Derecha", "Izquierda" }, altura = 2.0f, capa = 5 },
        new Enemigo { nombre = "Wendigo",  estados = new[] { "Frontal", "Derecha", "Izquierda" }, altura = 3.6f, capa = 1 },
    };

    [MenuItem("Juego/Configurar enemigos 2D")]
    public static void Ejecutar()
    {
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Prefabs)) AssetDatabase.CreateFolder("Assets/Prefabs", "Enemigos");

        foreach (var e in Enemigos) Configurar(e);
        ConfigurarVideosGameOver();
        AssetDatabase.SaveAssets();
        Debug.Log("Enemigos 2D listos: prefabs en " + Prefabs + ". Cinemáticas de Game Over importadas.");
    }

    static void Configurar(Enemigo e)
    {
        // 1) Medir el recuadro del personaje (unión de todos los cuadros) para elegir escala y pivote.
        int ancho = 0, alto = 0;
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;       // y desde arriba
        foreach (var estado in e.estados)
        {
            foreach (var ruta in Cuadros(e.nombre, estado))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(ruta));
                ancho = tex.width; alto = tex.height;
                var px = tex.GetPixels32();
                for (int y = 0; y < alto; y++)
                    for (int x = 0; x < ancho; x++)
                        if (px[y * ancho + x].a > 80)          // ignora la bruma tenue de los renders
                        {
                            int fila = alto - 1 - y;                       // GetPixels32 empieza abajo
                            if (x < x0) x0 = x; if (x > x1) x1 = x;
                            if (fila < y0) y0 = fila; if (fila > y1) y1 = fila;
                        }
                Object.DestroyImmediate(tex);
            }
        }
        if (x1 < 0) { Debug.LogError("Sin imágenes para " + e.nombre); return; }

        float altoPx = y1 - y0 + 1;
        float ppu = altoPx / e.altura;
        var pivote = new Vector2(((x0 + x1) * 0.5f + 0.5f) / ancho, 1f - (y1 + 1f) / alto);   // centro-abajo

        // 2) Importar los cuadros como sprites con ese pivote.
        foreach (var estado in e.estados)
            foreach (var ruta in Cuadros(e.nombre, estado))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = ppu;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.maxTextureSize = 2048;
                imp.filterMode = FilterMode.Bilinear;
                var ajustes = new TextureImporterSettings();
                imp.ReadTextureSettings(ajustes);
                ajustes.spriteAlignment = (int)SpriteAlignment.Custom;
                ajustes.spritePivot = pivote;
                imp.SetTextureSettings(ajustes);
                imp.SaveAndReimport();
            }

        // 3) Una animación por carpeta + un controlador con un estado por animación.
        string rutaControlador = Raiz + "/" + e.nombre + "/" + e.nombre + ".controller";
        AssetDatabase.DeleteAsset(rutaControlador);
        var controlador = AnimatorController.CreateAnimatorControllerAtPath(rutaControlador);
        var sm = controlador.layers[0].stateMachine;
        bool primero = true;
        foreach (var estado in e.estados)
        {
            var sprites = Cuadros(e.nombre, estado).Select(r => AssetDatabase.LoadAssetAtPath<Sprite>(r)).Where(s => s != null).ToArray();
            if (sprites.Length == 0) continue;

            var clip = new AnimationClip { frameRate = Fps, name = e.nombre + "_" + estado };
            var enlace = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var claves = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) claves[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, enlace, claves);
            var cfg = AnimationUtility.GetAnimationClipSettings(clip);
            cfg.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, cfg);

            string rutaClip = Raiz + "/" + e.nombre + "/" + e.nombre + "_" + estado + ".anim";
            AssetDatabase.DeleteAsset(rutaClip);
            AssetDatabase.CreateAsset(clip, rutaClip);

            var st = sm.AddState(estado);
            st.motion = clip;
            if (primero) { sm.defaultState = st; primero = false; }
        }

        // 4) Prefab del enemigo (todavía sin comportamiento).
        var go = new GameObject(e.nombre);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Cuadros(e.nombre, e.estados[0]).First());
        sr.sortingOrder = e.capa;
        var anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controlador;
        string rutaPrefab = Prefabs + "/" + e.nombre + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(go, rutaPrefab);
        Object.DestroyImmediate(go);

        Debug.Log(e.nombre + ": recuadro " + (x1 - x0 + 1) + "x" + (int)altoPx + " px de " + ancho + "x" + alto
                  + " | escala " + ppu.ToString("0.0") + " px/m -> " + e.altura + " m de alto | animaciones: " + string.Join(", ", e.estados));
    }

    static string[] Cuadros(string enemigo, string estado)
    {
        string carpeta = Raiz + "/" + enemigo + "/" + estado;
        if (!Directory.Exists(carpeta)) return new string[0];
        return Directory.GetFiles(carpeta, "frame_*.png").Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToArray();
    }

    // Los videos de Game Over se importan tal cual; se usarán con la pantalla de derrota.
    static void ConfigurarVideosGameOver()
    {
        foreach (var f in Directory.GetFiles("Assets/Cinematicas GameOver", "*.mp4"))
            AssetDatabase.ImportAsset(f.Replace('\\', '/'), ImportAssetOptions.ForceSynchronousImport);
    }
}
