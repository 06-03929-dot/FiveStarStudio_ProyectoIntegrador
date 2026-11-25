using UnityEngine;

// Genera Corredores desde los lados de la pantalla: el primero a los 3 s de que Murther da su primer paso
// y luego uno cada 5 s, alternando izquierda y derecha.
public class GeneradorCorredores : MonoBehaviour
{
    public GameObject prefab;
    public MurtherController murther;
    public float esperaTrasPrimerPaso = 3f;
    public float intervalo = 5f;
    public int maximoVivos = 4;
    public float profundidadZ = 0.5f;     // detrás del plano de Murther

    bool iniciado;
    float temporizador;
    int lado = -1;                        // empieza por la izquierda
    Camera cam;

    void Start()
    {
        cam = Camera.main;
        temporizador = esperaTrasPrimerPaso;
        if (murther == null) murther = FindFirstObjectByType<MurtherController>();
    }

    void Update()
    {
        if (prefab == null || murther == null) return;
        if (!iniciado)
        {
            if (Mathf.Abs(murther.VelocidadX) > 0.5f) iniciado = true;
            return;
        }

        temporizador -= Time.deltaTime;
        if (temporizador > 0f) return;
        temporizador = intervalo;
        if (FindObjectsByType<EnemigoCorredor>(FindObjectsSortMode.None).Length >= maximoVivos) return;
        Generar();
    }

    void Generar()
    {
        if (cam == null) cam = Camera.main;
        float mitad = cam.orthographicSize * cam.aspect;
        float x = cam.transform.position.x + lado * (mitad + 2f);
        var go = Instantiate(prefab, new Vector3(x, 0f, profundidadZ), Quaternion.identity);
        go.name = "Corredor";
        var c = go.GetComponent<EnemigoCorredor>();
        if (c == null) c = go.AddComponent<EnemigoCorredor>();
        c.Iniciar(-lado);                 // sale del lado izquierdo corriendo a la derecha, y al revés
        lado = -lado;
    }
}
