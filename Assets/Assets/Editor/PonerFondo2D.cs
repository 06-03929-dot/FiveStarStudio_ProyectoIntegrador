using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Ponlo en Assets/Editor. Menú: Juego > Poner fondo 2D
public class PonerFondo2D : EditorWindow
{
    [Serializable]
    public class Capa
    {
        public string etiqueta;
        public Texture2D imagen;
        public float distancia;
        public float parallax;
    }

    public Capa lejos = new Capa { etiqueta = "Fondo lejano (cielo)", distancia = 110f, parallax = 0.02f };
    public Capa medio = new Capa { etiqueta = "Fondo medio (montañas, ciudad)", distancia = 85f, parallax = 0.08f };
    public Capa cerca = new Capa { etiqueta = "Fondo cercano (árboles, detalles)", distancia = 65f, parallax = 0.18f };
    Vector2 scroll;

    [MenuItem("Juego/Prototipo anterior/Poner fondo 2D")]
    static void Abrir() => GetWindow<PonerFondo2D>("Fondo 2D");

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(
            "Usa solo las capas que necesites (con una basta). Las capas media y cercana deberían ser PNG con " +
            "transparencia para que se vea la capa de atrás. La altura de la imagen llena la pantalla.",
            MessageType.Info);

        Dibujar(lejos);
        Dibujar(medio);
        Dibujar(cerca);

        EditorGUILayout.Space();
        if (GUILayout.Button("Aplicar fondo", GUILayout.Height(32))) Aplicar();
        EditorGUILayout.EndScrollView();
    }

    static void Dibujar(Capa c)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(c.etiqueta, EditorStyles.boldLabel);
        c.imagen = (Texture2D)EditorGUILayout.ObjectField("Imagen", c.imagen, typeof(Texture2D), false);
        c.distancia = EditorGUILayout.FloatField("Distancia a la cámara", c.distancia);
        c.parallax = EditorGUILayout.Slider("Movimiento (0 = fijo)", c.parallax, 0f, 1f);
    }

    void Aplicar()
    {
        if (Camera.main == null)
        {
            EditorUtility.DisplayDialog("Falta la cámara", "Abre NivelPrueba.unity (necesita una Main Camera).", "OK");
            return;
        }

        var viejo = GameObject.Find("Fondo2D");
        if (viejo != null) Object.DestroyImmediate(viejo);

        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");

        var raiz = new GameObject("Fondo2D").transform;
        int n = 0;
        foreach (var c in new[] { lejos, medio, cerca })
        {
            if (c.imagen == null) continue;
            n++;
            Preparar(c.imagen);

            string path = "Assets/Materiales/Fondo_" + n + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default")) { name = "Fondo_" + n };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = c.imagen;
            mat.color = Color.white;
            EditorUtility.SetDirty(mat);

            var go = new GameObject("Capa" + n + "_" + c.imagen.name);
            go.transform.SetParent(raiz, false);
            var f = go.AddComponent<FondoParalaje>();
            f.material = mat;
            f.distancia = c.distancia;
            f.parallaxX = c.parallax;
        }

        if (n == 0)
        {
            Object.DestroyImmediate(raiz.gameObject);
            EditorUtility.DisplayDialog("Sin imágenes", "Arrastra al menos una imagen.", "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(raiz.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Fondo listo",
            "Se creó 'Fondo2D'. Presiona Play para verlo moverse con la cámara.\n\n" +
            "Para ajustar una capa, selecciónala y cambia 'Distancia' y 'Parallax X' en el componente Fondo Paralaje.", "OK");
    }

    // Que la imagen se pueda repetir y no se reduzca de tamaño
    static void Preparar(Texture2D t)
    {
        string path = AssetDatabase.GetAssetPath(t);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;

        bool cambio = false;
        if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; cambio = true; }
        if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; cambio = true; }
        if (imp.mipmapEnabled) { imp.mipmapEnabled = false; cambio = true; }
        if (imp.npotScale != TextureImporterNPOTScale.None) { imp.npotScale = TextureImporterNPOTScale.None; cambio = true; }
        if (imp.maxTextureSize < 4096) { imp.maxTextureSize = 4096; cambio = true; }
        if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; cambio = true; }
        if (cambio) imp.SaveAndReimport();
    }
}
