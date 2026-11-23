using UnityEngine;

// Hace temblar la intensidad de una luz (faroles, vitral) con ruido suave, como una llama.
// La intensidad puesta en el Inspector es el promedio.
[RequireComponent(typeof(Light))]
public class LuzParpadeo : MonoBehaviour
{
    [Range(0f, 1f)] public float variacion = 0.15f;   // cuánto sube y baja, en fracción de la intensidad
    public float velocidad = 2.5f;                     // cambios por segundo, aproximado

    Light luz;
    float intensidadBase;
    float semilla;

    void OnEnable()
    {
        luz = GetComponent<Light>();
        intensidadBase = luz.intensity;
        semilla = Random.value * 100f;
    }

    void Update()
    {
        // Dos octavas de ruido: un vaivén lento y un temblor más rápido encima.
        float lento = Mathf.PerlinNoise(semilla, Time.time * velocidad) - 0.5f;
        float rapido = Mathf.PerlinNoise(semilla + 37f, Time.time * velocidad * 3.7f) - 0.5f;
        luz.intensity = intensidadBase * (1f + (lento * 1.4f + rapido * 0.6f) * variacion);
    }

    void OnDisable()
    {
        if (luz != null) luz.intensity = intensidadBase;
    }
}
