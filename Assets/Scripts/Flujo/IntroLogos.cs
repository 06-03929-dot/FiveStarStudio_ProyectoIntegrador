using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

// Intro: reproduce el video de la animación del logo sobre negro, con fundido de
// entrada y salida, y al terminar avisa al menú.
public class IntroLogos : MonoBehaviour
{
    public VideoPlayer video;
    public CanvasGroup grupoVideo;
    public CanvasGroup panelNegro;
    public float fundido = 0.6f;
    public float esperaPreparar = 5f;
    public float tiempoMaximo = 20f;
    public Action alTerminar;

    IEnumerator Start()
    {
        grupoVideo.alpha = 0f;
        panelNegro.alpha = 1f;
        yield return null;

        if (video != null && video.clip != null)
        {
            video.Prepare();
            for (float e = 0f; !video.isPrepared && e < esperaPreparar; e += Time.deltaTime)
                yield return null;

            if (video.isPrepared)
            {
                bool terminado = false;
                video.loopPointReached += _ => terminado = true;
                video.Play();
                yield return Alfa(grupoVideo, 0f, 1f, fundido * 0.3f);   // el video arranca casi de inmediato
                // Cualquier tecla, clic o botón del control salta el logo (antes había que esperarlo completo).
                yield return null;
                for (float t = 0f; !terminado && t < tiempoMaximo && !Saltar(); t += Time.deltaTime)
                    yield return null;
                yield return Alfa(grupoVideo, 1f, 0f, fundido);
                video.Stop();
            }
        }

        // El menú (con su imagen de fondo) aparece debajo mientras el negro se desvanece.
        alTerminar?.Invoke();
        yield return Alfa(panelNegro, 1f, 0f, fundido);
        panelNegro.gameObject.SetActive(false);
    }

    static bool Saltar()
    {
        var k = Keyboard.current;
        var m = Mouse.current;
        var g = Gamepad.current;
        return (k != null && k.anyKey.wasPressedThisFrame)
            || (m != null && m.leftButton.wasPressedThisFrame)
            || (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame));
    }

    static IEnumerator Alfa(CanvasGroup g, float a, float b, float dur)
    {
        // Tope por cuadro: si la carga inicial produce un cuadro muy largo, el fundido no se salta.
        for (float t = 0f; t < dur; t += Mathf.Min(Time.deltaTime, 1f / 30f))
        {
            g.alpha = Mathf.Lerp(a, b, t / dur);
            yield return null;
        }
        g.alpha = b;
    }
}
