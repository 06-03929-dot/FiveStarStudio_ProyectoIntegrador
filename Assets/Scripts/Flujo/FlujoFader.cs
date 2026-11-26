using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla negra persistente entre escenas. Se crea sola la primera vez que se usa.
public class FlujoFader : MonoBehaviour
{
    public const float DuracionDefecto = 0.8f;

    static FlujoFader instancia;
    CanvasGroup grupo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { instancia = null; }

    public static FlujoFader Instancia
    {
        get
        {
            if (instancia == null)
                instancia = new GameObject("FlujoFader").AddComponent<FlujoFader>();
            return instancia;
        }
    }

    void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;
        DontDestroyOnLoad(gameObject);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        grupo = gameObject.AddComponent<CanvasGroup>();

        var go = new GameObject("Negro", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = Color.black;

        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;
        grupo.interactable = false;
    }

    // Fundido a negro, carga la escena y vuelve a mostrar.
    public void IrAEscena(string escena, float duracion = DuracionDefecto)
    {
        StopAllCoroutines();
        StartCoroutine(RutinaIrAEscena(escena, duracion));
    }

    // Fundido a negro, ejecuta alMedio (cambiar paneles) y vuelve a mostrar.
    public void Transicion(Action alMedio, float duracion = DuracionDefecto)
    {
        StopAllCoroutines();
        StartCoroutine(RutinaTransicion(alMedio, duracion));
    }

    IEnumerator RutinaIrAEscena(string escena, float duracion)
    {
        // Si la escena no está en Build Settings, LoadScene fallaba y la pantalla se quedaba en negro
        // bloqueando los clics para siempre. Ahora se avisa en la consola y se vuelve a mostrar la escena actual.
        if (!Application.CanStreamedLevelBeLoaded(escena))
        {
            Debug.LogError("FlujoFader: la escena \"" + escena + "\" no está en Build Settings (Juego > Registrar escenas en Build Settings).");
            yield return Fundir(grupo.alpha, 0f, duracion * 0.5f);
            yield break;
        }

        yield return Fundir(grupo.alpha, 1f, duracion);
        yield return SceneManager.LoadSceneAsync(escena);
        yield return null;
        yield return Fundir(1f, 0f, duracion);
    }

    IEnumerator RutinaTransicion(Action alMedio, float duracion)
    {
        yield return Fundir(grupo.alpha, 1f, duracion);
        alMedio?.Invoke();
        yield return null;
        yield return Fundir(1f, 0f, duracion);
    }

    IEnumerator Fundir(float desde, float hasta, float duracion)
    {
        grupo.blocksRaycasts = true;
        // El primer cuadro después de cargar una escena puede durar medio segundo o más; sin este tope,
        // ese salto se comía el fundido de entrada y la escena aparecía de golpe.
        for (float t = 0f; t < duracion; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f))
        {
            grupo.alpha = Mathf.Lerp(desde, hasta, t / duracion);
            yield return null;
        }
        grupo.alpha = hasta;
        grupo.blocksRaycasts = hasta > 0.5f;
    }
}
