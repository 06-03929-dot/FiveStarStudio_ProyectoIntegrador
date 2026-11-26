using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Ponlo en una carpeta llamada "Editor" (Assets/Editor).
// Menú: Juego > Crear escena de prueba
public static class CrearEscenaDePrueba
{
    [MenuItem("Juego/Prototipo anterior/Crear escena de prueba")]
    public static void Crear()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Materiales");

        int ground = EnsureLayer("Ground");
        LayerMask groundMask = 1 << ground;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ---------- Materiales ----------
        Material mSuelo = MakeMat("Suelo", new Color(0.45f, 0.35f, 0.25f));
        Material mPared = MakeMat("Pared", new Color(0.6f, 0.65f, 0.8f));
        Material mPlat = MakeMat("Plataforma", new Color(0.95f, 0.8f, 0.4f));
        Material mJugador = MakeMat("Jugador", new Color(0.3f, 0.7f, 1f));
        Material mRojo = MakeMat("Deslizador", new Color(0.9f, 0.3f, 0.35f));
        Material mMorado = MakeMat("Arquero", new Color(0.6f, 0.35f, 0.75f));
        Material mVerde = MakeMat("Mordedor", new Color(0.3f, 0.7f, 0.3f));
        Material mAmarillo = MakeMat("Flecha", new Color(1f, 0.9f, 0.4f));

        // ---------- Nivel (x, y base, ancho, alto) ----------
        float[][] rects =
        {
            new[] { -12f, -3f, 2f, 40f },   // pared izquierda
            new[] { -10f, -3f, 34f, 3f },   // suelo 1
            new[] { 6f, 2.5f, 4f, .6f },
            new[] { 12f, 5f, 4f, .6f },
            new[] { 17f, 3f, 4f, .6f },
            new[] { 31f, 1.5f, 3f, .6f },   // plataforma sobre el vacío
            new[] { 40f, -3f, 42f, 3f },    // suelo 2
            new[] { 50f, 0f, 2f, 15f },     // pozo de paredes
            new[] { 58f, 0f, 2f, 15f },
            new[] { 62f, 12f, 10f, 1f },
        };

        var nivel = new GameObject("Nivel").transform;
        for (int i = 0; i < rects.Length; i++)
        {
            float[] r = rects[i];
            Material m = r[3] >= 10f ? mPared : (Mathf.Approximately(r[3], 3f) ? mSuelo : mPlat);
            var go = Box("Bloque" + (i + 1),
                new Vector3(r[0] + r[2] / 2f, r[1] + r[3] / 2f, 0f),
                new Vector3(r[2], r[3], 4f), m, nivel);
            go.layer = ground;
        }

        // ---------- Prefabs de enemigos ----------
        var sGo = Box("Deslizador", Vector3.zero, new Vector3(1f, .9f, .8f), mRojo, null);
        var slider = sGo.AddComponent<SlidingEnemy>();
        slider.groundLayer = groundMask;
        var sliderPrefab = SavePrefab<SlidingEnemy>(sGo, "Deslizador");

        var fGo = Box("Flecha", Vector3.zero, new Vector3(.6f, .12f, .12f), mAmarillo, null);
        var arrow = fGo.AddComponent<Arrow>();
        arrow.groundLayer = groundMask;
        var arrowPrefab = SavePrefab<Arrow>(fGo, "Flecha");

        var aGo = Box("Arquero", Vector3.zero, new Vector3(.9f, .9f, .8f), mMorado, null);
        var archer = aGo.AddComponent<FlyingArcher>();
        archer.arrowPrefab = arrowPrefab;
        var archerPrefab = SavePrefab<FlyingArcher>(aGo, "Arquero");

        var bGo = Box("Mordedor", Vector3.zero, new Vector3(.9f, 2.2f, .5f), mVerde, null);
        var biter = bGo.AddComponent<BiterEnemy>();
        biter.hideDepth = 1.2f;
        biter.peekHeight = 0.9f;
        biter.biteHeight = 1.7f;
        var biterPrefab = SavePrefab<BiterEnemy>(bGo, "Mordedor");

        // ---------- Puntos de aparición ----------
        Transform[] groundPts = MakePoints("PuntosSuelo", "Suelo", new[]
            { new[] { 16f, .5f }, new[] { 6f, .5f }, new[] { 70f, .5f }, new[] { 44f, .5f } }, 3);
        Transform[] airPts = MakePoints("PuntosAire", "Aire", new[]
            { new[] { 14f, 7f }, new[] { 30f, 4.2f }, new[] { 44f, 3.5f }, new[] { 68f, 4.5f } }, 5);
        Transform[] holePts = MakePoints("PuntosAgujero", "Agujero", new[]
            { new[] { 20f, 0f }, new[] { 46f, 0f }, new[] { 66f, 0f }, new[] { 10f, 0f } }, 1);

        var spawn = new GameObject("PlayerSpawn");
        spawn.transform.position = new Vector3(0f, 1f, 0f);

        // ---------- Jugador ----------
        var player = Box("Player", new Vector3(0f, 1f, 0f), new Vector3(.8f, 1.8f, .8f), mJugador, null);
        player.tag = "Player";
        var pc = player.AddComponent<PlayerController3D>();
        pc.groundLayer = groundMask;
        player.AddComponent<PlayerHealth>();
        TintaUtil.Agregar(player);

        // ---------- Cámara ----------
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 4f, -20f);
            cam.transform.rotation = Quaternion.identity;
            cam.fieldOfView = 40f;
            var follow = cam.gameObject.AddComponent<CameraFollow2_5D>();
            follow.target = player.transform;
        }

        // ---------- GameManager ----------
        var gm = new GameObject("GameManager");
        var lm = gm.AddComponent<LevelManager>();
        lm.player = pc;
        lm.playerSpawn = spawn.transform;
        lm.sliderPrefab = sliderPrefab;
        lm.archerPrefab = archerPrefab;
        lm.biterPrefab = biterPrefab;
        lm.groundPoints = groundPts;
        lm.airPoints = airPts;
        lm.holePoints = holePts;

        // ---------- Guardar ----------
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/NivelPrueba.unity");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;

        EditorUtility.DisplayDialog("Escena lista",
            "Se creó Assets/Scenes/NivelPrueba.unity con el nivel, el jugador, los 3 enemigos y el LevelManager.\n\n" +
            "Presiona Play.\n\nControles: flechas o A/D mover, Espacio saltar, Shift o J dash, K o X golpear.", "OK");

        CheckInputHandling();
    }

    [MenuItem("Juego/Prototipo anterior/Borrar progreso guardado")]
    public static void BorrarProgreso()
    {
        foreach (string k in new[] { "NivelGuardado", "VidaGuardada", "PosXGuardada", "PosYGuardada", "EnemigosVivos" })
            PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("Progreso borrado", "La próxima vez empezarás desde el nivel 1.", "OK");
    }

    // ---------- Ayudas ----------
    static GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static T SavePrefab<T>(GameObject go, string name) where T : Component
    {
        string path = "Assets/Prefabs/" + name + ".prefab";
        var asset = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return asset.GetComponent<T>();
    }

    static Transform[] MakePoints(string parentName, string prefix, float[][] pts, int icon)
    {
        var parent = new GameObject(parentName).transform;
        var result = new Transform[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            var go = new GameObject(prefix + (i + 1));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pts[i][0], pts[i][1], 0f);
            var tex = EditorGUIUtility.IconContent("sv_label_" + icon).image as Texture2D;
            if (tex != null) EditorGUIUtility.SetIconForObject(go, tex);
            result[i] = go.transform;
        }
        return result;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    internal static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing != -1) return existing;

        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        var tagManager = new SerializedObject(assets[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            var el = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(el.stringValue))
            {
                el.stringValue = name;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }
        throw new Exception("No hay capas libres para crear 'Ground'.");
    }

    static Material MakeMat(string name, Color color)
    {
        string path = "Assets/Materiales/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader sh = null;
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null) sh = rp.defaultMaterial.shader;
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(sh) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Unity 6 suele traer SOLO el Input System nuevo, y los scripts usan el Input clásico.
    static void CheckInputHandling()
    {
        try
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue != 1) return;   // 1 = solo el nuevo

            if (EditorUtility.DisplayDialog("Entrada (Input)",
                "Tu proyecto usa solo el Input System nuevo y el juego usa el clásico: daría errores al jugar.\n\n" +
                "¿Cambiar a 'Both' (ambos)? Después cierra y vuelve a abrir Unity.", "Sí, cambiar", "No"))
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }
        catch (Exception) { }
    }
}
