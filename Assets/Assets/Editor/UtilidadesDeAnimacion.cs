using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Ponlo en Assets/Editor. Lo usa la ventana "Juego > Poner mis modelos 3D".
public static class AnimadorUtil
{
    static readonly string[] Estados = { "Hurt", "Dash", "Fall", "Jump", "Attack", "Run", "Idle" };
    static readonly string[][] Claves =
    {
        new[] { "hurt", "damage", "dano", "daño", "reaccion", "reacción" },
        new[] { "dash", "roll", "dodge", "impulso", "esquiv" },
        new[] { "fall", "caid", "caíd", "cae" },
        new[] { "jump", "salt" },
        new[] { "attack", "ataque", "golpe", "punch", "bite", "muerd", "morder", "shoot", "dispar", "slash", "strike", "hit" },
        new[] { "run", "corr", "walk", "camin", "sprint", "move", "mov" },
        new[] { "idle", "reposo", "quiet", "parad", "espera", "stand", "breath" },
    };

    static bool Coincide(string key, string[] claves)
    {
        foreach (var c in claves) if (key.Contains(c)) return true;
        return false;
    }

    static string Clave(string ruta, string clip) =>
        (Path.GetFileNameWithoutExtension(ruta) + " " + clip).ToLowerInvariant();

    static void AgregarRuta(List<string> rutas, GameObject asset)
    {
        if (asset == null) return;
        string r = AssetDatabase.GetAssetPath(asset);
        if (!string.IsNullOrEmpty(r) && !rutas.Contains(r)) rutas.Add(r);
    }

    // Marca "Loop Time" en idle/correr/caer para que se repitan (reimporta el FBX).
    public static void PrepararLoops(GameObject asset, List<GameObject> extras)
    {
        var rutas = new List<string>();
        AgregarRuta(rutas, asset);
        if (extras != null) foreach (var e in extras) AgregarRuta(rutas, e);

        foreach (var ruta in rutas)
        {
            try
            {
                var imp = AssetImporter.GetAtPath(ruta) as ModelImporter;
                if (imp == null) continue;
                var arr = imp.clipAnimations;
                if (arr == null || arr.Length == 0) arr = imp.defaultClipAnimations;

                bool cambio = false;
                foreach (var c in arr)
                {
                    string key = Clave(ruta, c.name);
                    bool repetir = Coincide(key, Claves[Array.IndexOf(Estados, "Run")])
                                || Coincide(key, Claves[Array.IndexOf(Estados, "Idle")])
                                || Coincide(key, Claves[Array.IndexOf(Estados, "Fall")]);
                    if (repetir && !c.loopTime) { c.loopTime = true; cambio = true; }
                }
                if (cambio)
                {
                    imp.clipAnimations = arr;
                    imp.SaveAndReimport();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("No se pudo activar el loop en " + ruta + ": " + e.Message);
            }
        }
    }

    public static void Configurar(GameObject instancia, GameObject asset, List<GameObject> extras, string nombre, bool esJugador)
    {
        var rutas = new List<string>();
        AgregarRuta(rutas, asset);
        if (extras != null) foreach (var e in extras) AgregarRuta(rutas, e);

        var clips = new List<KeyValuePair<string, AnimationClip>>();
        foreach (var ruta in rutas)
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ruta))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                    clips.Add(new KeyValuePair<string, AnimationClip>(Clave(ruta, c.name), c));

        if (clips.Count == 0)
        {
            Debug.LogWarning("Animaciones: no encontré animaciones en el modelo de '" + nombre +
                             "'. Exporta el FBX con animaciones o agrega los archivos de animación en la ventana.");
            return;
        }

        // Elegir qué animación va en cada estado según el nombre
        var elegidos = new Dictionary<string, AnimationClip>();
        var usados = new HashSet<AnimationClip>();
        for (int i = 0; i < Estados.Length; i++)
        {
            foreach (var kv in clips)
            {
                if (usados.Contains(kv.Value)) continue;
                if (Coincide(kv.Key, Claves[i]))
                {
                    elegidos[Estados[i]] = kv.Value;
                    usados.Add(kv.Value);
                    break;
                }
            }
        }

        if (!elegidos.ContainsKey("Idle"))
            foreach (var kv in clips)
                if (!usados.Contains(kv.Value)) { elegidos["Idle"] = kv.Value; usados.Add(kv.Value); break; }
        if (!elegidos.ContainsKey("Fall") && elegidos.ContainsKey("Jump")) elegidos["Fall"] = elegidos["Jump"];
        if (!elegidos.ContainsKey("Jump") && elegidos.ContainsKey("Fall")) elegidos["Jump"] = elegidos["Fall"];

        string resumen = "";
        foreach (var kv in elegidos) resumen += kv.Key + " = " + kv.Value.name + "   ";
        Debug.Log("Animaciones de '" + nombre + "': " + resumen);

        if (!AssetDatabase.IsValidFolder("Assets/Animadores")) AssetDatabase.CreateFolder("Assets", "Animadores");
        var ctrl = Crear("Assets/Animadores/" + nombre + ".controller", elegidos, esJugador);

        var anim = instancia.GetComponentInChildren<Animator>();
        if (anim == null) anim = instancia.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
    }

    static AnimatorState Estado(AnimatorStateMachine sm, string nombre, Dictionary<string, AnimationClip> c)
    {
        if (!c.TryGetValue(nombre, out var clip) || clip == null) return null;
        var s = sm.AddState(nombre);
        s.motion = clip;
        return s;
    }

    static AnimatorStateTransition Trans(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.08f;
        return t;
    }

    static void Cualquiera(AnimatorStateMachine sm, AnimatorState to, string trigger)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static void Salida(AnimatorState from, AnimatorState idle)
    {
        var t = from.AddTransition(idle);
        t.hasExitTime = true;
        t.exitTime = 0.9f;
        t.duration = 0.1f;
    }

    static AnimatorController Crear(string ruta, Dictionary<string, AnimationClip> c, bool esJugador)
    {
        AssetDatabase.DeleteAsset(ruta);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ruta);

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("VelY", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        foreach (var t in new[] { "Jump", "Attack", "Hurt", "Dash" })
            ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        if (c.TryGetValue("Idle", out var idleClip)) idle.motion = idleClip;
        sm.defaultState = idle;

        var run = Estado(sm, "Run", c);
        var jump = Estado(sm, "Jump", c);
        var fall = Estado(sm, "Fall", c);
        var attack = Estado(sm, "Attack", c);
        var hurt = Estado(sm, "Hurt", c);
        var dash = Estado(sm, "Dash", c);

        float umbral = esJugador ? 0.3f : 0.1f;
        if (run != null)
        {
            var a = Trans(idle, run);
            a.AddCondition(AnimatorConditionMode.Greater, umbral, "Speed");
            if (esJugador) a.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            var b = Trans(run, idle);
            b.AddCondition(AnimatorConditionMode.Less, umbral, "Speed");
        }

        if (esJugador)
        {
            if (fall != null)
            {
                foreach (var from in new[] { idle, run })
                {
                    if (from == null) continue;
                    var t = Trans(from, fall);
                    t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                    t.AddCondition(AnimatorConditionMode.Less, -0.5f, "VelY");
                }
                var fi = Trans(fall, idle);
                fi.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            }
            if (jump != null)
            {
                Cualquiera(sm, jump, "Jump");
                if (fall != null)
                {
                    var jf = Trans(jump, fall);
                    jf.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                    jf.AddCondition(AnimatorConditionMode.Less, -0.1f, "VelY");
                }
                var ji = Trans(jump, idle);
                ji.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
                ji.AddCondition(AnimatorConditionMode.Less, 0.1f, "VelY");
            }
        }

        if (attack != null) { Cualquiera(sm, attack, "Attack"); Salida(attack, idle); }
        if (hurt != null) { Cualquiera(sm, hurt, "Hurt"); Salida(hurt, idle); }
        if (dash != null && esJugador) { Cualquiera(sm, dash, "Dash"); Salida(dash, idle); }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }
}

public static class TintaUtil
{
    [MenuItem("Juego/Agregar tinta al jugador")]
    static void Menu()
    {
        var pc = Object.FindFirstObjectByType<PlayerController3D>();
        if (pc == null)
        {
            EditorUtility.DisplayDialog("No hay jugador", "Abre NivelPrueba.unity (o crea la escena de prueba) y vuelve a intentar.", "OK");
            return;
        }
        Agregar(pc.gameObject);
        EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("Listo", "El jugador ya tiene el efecto de tinta. Presiona Play.", "OK");
    }

    public static void Agregar(GameObject player)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");

        Material gota = MaterialTinta("TintaGota", true);
        Material rastro = MaterialTinta("TintaRastro", false);

        var ink = player.GetComponent<InkTrail>();
        if (ink == null) ink = player.AddComponent<InkTrail>();
        ink.dropMaterial = gota;
        ink.trailMaterial = rastro;
        EditorUtility.SetDirty(ink);

        if (player.GetComponent<PlayerAnimator>() == null) player.AddComponent<PlayerAnimator>();
        AssetDatabase.SaveAssets();
    }

    static Material MaterialTinta(string nombre, bool conTextura)
    {
        string path = "Assets/Materiales/" + nombre + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default")) { name = nombre };
            AssetDatabase.CreateAsset(mat, path);
        }
        if (conTextura) mat.mainTexture = TexturaGota();
        mat.color = Color.white;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D TexturaGota()
    {
        string path = "Assets/Materiales/TintaGota.png";
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existente != null) return existente;

        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 5f)));
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
