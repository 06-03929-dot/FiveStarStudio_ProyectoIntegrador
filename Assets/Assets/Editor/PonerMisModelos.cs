using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Ponlo en Assets/Editor. Menú: Juego > Poner mis modelos 3D
// Reemplaza los cubos del jugador y los enemigos por tus modelos (el cubo sigue
// existiendo, invisible, porque ahí viven el collider y los scripts) y convierte
// tu escenario 3D en suelo/plataformas con colisión.
public class PonerMisModelos : EditorWindow
{
    [Serializable]
    public class Slot
    {
        public string etiqueta;
        public GameObject modelo;
        public float tamano;     // altura deseada en metros (o largo, si porLargo)
        public float giroY;      // para que el modelo mire hacia la derecha (+X)
        public bool porLargo;
        public System.Collections.Generic.List<GameObject> animaciones = new System.Collections.Generic.List<GameObject>();
    }

    public Slot jugador = new Slot { etiqueta = "Jugador (altura)", tamano = 1.8f, giroY = 90f };
    public Slot deslizador = new Slot { etiqueta = "Enemigo deslizador (altura)", tamano = 0.9f, giroY = 90f };
    public Slot arquero = new Slot { etiqueta = "Enemigo arquero (altura)", tamano = 0.9f, giroY = 90f };
    public Slot mordedor = new Slot { etiqueta = "Enemigo mordedor (altura)", tamano = 2.2f, giroY = 0f };
    public Slot flecha = new Slot { etiqueta = "Flecha (largo)", tamano = 0.6f, giroY = 90f, porLargo = true };

    public GameObject escenario;
    public float escalaEscenario = 1f;
    Vector2 scroll;

    [MenuItem("Juego/Prototipo anterior/Poner mis modelos 3D")]
    static void Abrir() => GetWindow<PonerMisModelos>("Mis modelos 3D");

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "1) Arrastra tus modelos (FBX) desde la carpeta Assets.\n" +
            "2) Ajusta el tamaño y el giro si hace falta.\n" +
            "3) Pulsa 'Aplicar personajes'. Primero crea la escena con 'Juego > Crear escena de prueba'.",
            MessageType.Info);

        DibujarSlot(jugador);
        DibujarSlot(deslizador);
        DibujarSlot(arquero);
        DibujarSlot(mordedor);
        DibujarSlot(flecha);

        EditorGUILayout.Space();
        if (GUILayout.Button("Aplicar personajes", GUILayout.Height(32))) AplicarPersonajes();

        EditorGUILayout.Space(16);
        EditorGUILayout.LabelField("Escenario", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Los objetos de tu escenario cuyo nombre contenga 'deco' o 'fondo' NO tendrán colisión " +
            "(úsalo para adornos y fondo). Todo lo demás será suelo/pared.", MessageType.None);
        escenario = (GameObject)EditorGUILayout.ObjectField("Modelo del escenario", escenario, typeof(GameObject), false);
        escalaEscenario = EditorGUILayout.FloatField("Escala", escalaEscenario);
        if (GUILayout.Button("Aplicar escenario", GUILayout.Height(32))) AplicarEscenario();

        EditorGUILayout.EndScrollView();
    }

    static void DibujarSlot(Slot s)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(s.etiqueta, EditorStyles.boldLabel);
        s.modelo = (GameObject)EditorGUILayout.ObjectField("Modelo", s.modelo, typeof(GameObject), false);
        s.tamano = EditorGUILayout.FloatField(s.porLargo ? "Largo (m)" : "Altura (m)", s.tamano);
        s.giroY = EditorGUILayout.FloatField("Giro Y (grados)", s.giroY);

        if (!s.porLargo)
        {
            EditorGUILayout.LabelField("Archivos con animaciones sueltas (opcional)");
            for (int i = 0; i < s.animaciones.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                s.animaciones[i] = (GameObject)EditorGUILayout.ObjectField(s.animaciones[i], typeof(GameObject), false);
                if (GUILayout.Button("-", GUILayout.Width(24))) { s.animaciones.RemoveAt(i); i--; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Agregar archivo de animación")) s.animaciones.Add(null);
        }
    }

    // ---------- PERSONAJES ----------
    void AplicarPersonajes()
    {
        var pc = FindFirstObjectByType<PlayerController3D>();

        if (jugador.modelo != null)
        {
            if (pc == null)
                Debug.LogWarning("No hay Player en la escena abierta. Abre NivelPrueba.unity.");
            else
            {
                Aplicar(pc.gameObject, jugador, "Jugador", true);
                EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
            }
        }

        AplicarPrefab("Deslizador", deslizador);
        AplicarPrefab("Arquero", arquero);
        AplicarPrefab("Mordedor", mordedor);
        AplicarPrefab("Flecha", flecha);

        AssetDatabase.SaveAssets();
        if (pc != null) EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Listo",
            "Modelos aplicados. Si alguno mira hacia el lado equivocado, cambia 'Giro Y' (90, -90, 0 o 180) y vuelve a aplicar.", "OK");
    }

    static void AplicarPrefab(string nombre, Slot s)
    {
        if (s.modelo == null) return;
        string path = "Assets/Prefabs/" + nombre + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning("No existe " + path + ". Ejecuta primero 'Juego > Crear escena de prueba'.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Aplicar(root, s, nombre, false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void Aplicar(GameObject root, Slot s, string nombre, bool esJugador)
    {
        // Quitar un modelo aplicado antes (para poder repetirlo)
        var anterior = root.transform.Find("Visual");
        if (anterior != null) Object.DestroyImmediate(anterior.gameObject);

        // Antes de poner el modelo: que idle/correr/caer se repitan (reimporta el FBX si hace falta)
        if (nombre != "Flecha") AnimadorUtil.PrepararLoops(s.modelo, s.animaciones);

        // El cubo queda invisible pero conserva collider y scripts
        var mr = root.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
        root.transform.localScale = Vector3.one;

        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);

        var modelo = (GameObject)PrefabUtility.InstantiatePrefab(s.modelo, visual);
        modelo.name = "Modelo";

        // Sin colisiones propias del modelo
        foreach (var c in modelo.GetComponentsInChildren<Collider>()) c.enabled = false;

        modelo.transform.localPosition = Vector3.zero;
        modelo.transform.localRotation = Quaternion.Euler(0f, s.giroY, 0f);
        modelo.transform.localScale = Vector3.one;

        // Escalar al tamaño deseado
        Bounds b = Medir(modelo);
        float medida = s.porLargo ? b.size.x : b.size.y;
        if (medida > 0.0001f) modelo.transform.localScale = Vector3.one * (s.tamano / medida);

        // Centrar el modelo en el centro del objeto
        b = Medir(modelo);
        modelo.transform.position += root.transform.position - b.center;
        b = Medir(modelo);

        // Ajustar el collider a la forma del modelo
        var box = root.GetComponent<BoxCollider>();
        if (box == null) box = root.AddComponent<BoxCollider>();
        box.center = root.transform.InverseTransformPoint(b.center);
        box.size = b.size;

        // El jugador gira hacia donde camina
        var pc = root.GetComponent<PlayerController3D>();
        if (pc != null) pc.visual = visual;

        // El mordedor ajusta cuánto se esconde y sale según su altura
        var bite = root.GetComponent<BiterEnemy>();
        if (bite != null)
        {
            float h = b.size.y;
            bite.hideDepth = h * 0.545f;
            bite.peekHeight = h * 0.41f;
            bite.biteHeight = h * 0.77f;
        }

        // Animaciones (correr, saltar, atacar...) y tinta del jugador
        if (nombre != "Flecha")
            AnimadorUtil.Configurar(modelo, s.modelo, s.animaciones, nombre, esJugador);
        if (esJugador) TintaUtil.Agregar(root);

        EditorUtility.SetDirty(root);
    }

    static Bounds Medir(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // ---------- ESCENARIO ----------
    void AplicarEscenario()
    {
        if (escenario == null)
        {
            EditorUtility.DisplayDialog("Falta el modelo", "Arrastra primero tu modelo de escenario.", "OK");
            return;
        }

        int ground = LayerMask.NameToLayer("Ground");
        if (ground == -1) ground = CrearEscenaDePrueba.EnsureLayer("Ground");

        var nivel = GameObject.Find("Nivel");
        if (nivel != null) nivel.SetActive(false);   // oculta el nivel de prueba

        var viejo = GameObject.Find("MiEscenario");
        if (viejo != null) Object.DestroyImmediate(viejo);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(escenario);
        inst.name = "MiEscenario";
        inst.transform.position = Vector3.zero;
        inst.transform.localScale = Vector3.one * escalaEscenario;

        foreach (var tr in inst.GetComponentsInChildren<Transform>(true))
        {
            if (EsDeco(tr, inst.transform))
            {
                tr.gameObject.layer = 0;
                foreach (var c in tr.GetComponents<Collider>()) c.enabled = false;
                continue;
            }

            tr.gameObject.layer = ground;

            var mf = tr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && tr.GetComponent<Collider>() == null)
            {
                var mc = tr.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }

        EditorSceneManager.MarkSceneDirty(inst.scene);
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("Escenario listo",
            "Se creó 'MiEscenario' con colisiones y capa Ground.\n\n" +
            "Ahora mueve 'PlayerSpawn', el Player y los puntos de aparición (PuntosSuelo, PuntosAire, PuntosAgujero) a tu nivel.", "OK");
    }

    static bool EsDeco(Transform t, Transform raiz)
    {
        for (var x = t; x != null; x = x.parent)
        {
            string n = x.name.ToLowerInvariant();
            if (n.Contains("deco") || n.Contains("fondo")) return true;
            if (x == raiz) break;
        }
        return false;
    }
}
