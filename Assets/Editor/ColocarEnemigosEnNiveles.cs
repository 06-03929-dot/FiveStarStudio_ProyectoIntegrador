using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pone a los enemigos 2D a la vista en sus niveles:
//  Nivel1: generador de Corredores (3 s tras el primer paso de Murther y luego cada 5 s, desde los lados).
//  Nivel2: un Arquero flotando.  Nivel3: el Wendigo asomando en la ventana rota.
// Menú: Juego > Poner enemigos en los niveles. Es repetible.
public static class ColocarEnemigosEnNiveles
{
    const string Prefabs = "Assets/Prefabs/Enemigos/";

    [MenuItem("Juego/Poner enemigos en los niveles")]
    public static void Ejecutar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Nivel(1, escena =>
        {
            var viejo = Raiz(escena, "Generador de Corredores");
            if (viejo != null) Object.DestroyImmediate(viejo);
            var go = new GameObject("Generador de Corredores");
            var gen = go.AddComponent<GeneradorCorredores>();
            gen.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Corredor.prefab");
            gen.murther = Object.FindFirstObjectByType<MurtherController>();
        });

        Nivel(2, escena =>
        {
            Poner(escena, "Arquero", new Vector3(5f, 4.2f, 0.5f), "Izquierda");
        });

        Nivel(3, escena =>
        {
            Poner(escena, "Wendigo", new Vector3(0f, 3.0f, 3f), "Frontal");
        });

        EditorSceneManager.OpenScene("Assets/Scenes/Nivel1.unity", OpenSceneMode.Single);
        Debug.Log("Enemigos colocados: Nivel1 (Corredores desde los lados), Nivel2 (Arquero), Nivel3 (Wendigo).");
    }

    static void Nivel(int n, System.Action<Scene> accion)
    {
        var escena = EditorSceneManager.OpenScene("Assets/Scenes/Nivel" + n + ".unity", OpenSceneMode.Single);
        accion(escena);
        EditorSceneManager.SaveScene(escena);
    }

    static void Poner(Scene escena, string enemigo, Vector3 pos, string estado)
    {
        string nombre = enemigo + " (vista previa)";
        var viejo = Raiz(escena, nombre);
        if (viejo != null) Object.DestroyImmediate(viejo);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + enemigo + ".prefab");
        if (prefab == null) { Debug.LogError("Falta el prefab de " + enemigo + ". Ejecuta Juego > Configurar enemigos 2D."); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = nombre;
        go.transform.position = pos;
        var dec = go.AddComponent<EnemigoDecorativo>();
        dec.estado = estado;
    }

    static GameObject Raiz(Scene escena, string nombre)
    {
        return escena.GetRootGameObjects().FirstOrDefault(g => g.name == nombre);
    }
}
