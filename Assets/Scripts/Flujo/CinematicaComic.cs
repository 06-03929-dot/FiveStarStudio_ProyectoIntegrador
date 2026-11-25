using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Cinemática de cómic de 1:30. Cada panel (imagen o texto temporal) ocupa la misma
// fracción de la duración total. Saltar con el botón o con Escape.
public class CinematicaComic : MonoBehaviour
{
    public float duracionTotal = 90f;
    public string escenaSiguiente = "Controles_Nivel1";
    public Image imagenPanel;
    public Text textoPanel;
    public Text textoTiempo;
    public Sprite[] paginas;           // opcional: páginas del cómic

    float t;
    bool terminando;

    void Update()
    {
        if (terminando) return;
        t += Time.deltaTime;

        bool conImagenes = paginas != null && paginas.Length > 0;
        int paneles = conImagenes ? paginas.Length : TextosJuego.PanelesCinematica.Length;
        int i = Mathf.Clamp((int)(t / duracionTotal * paneles), 0, paneles - 1);
        if (conImagenes)
        {
            imagenPanel.sprite = paginas[i];
            imagenPanel.enabled = true;
            textoPanel.text = "";
        }
        else
        {
            textoPanel.text = TextosJuego.PanelesCinematica[i];
        }

        int restante = Mathf.CeilToInt(Mathf.Max(0f, duracionTotal - t));
        textoTiempo.text = (restante / 60) + ":" + (restante % 60).ToString("00");

        // Con control no había forma de saltarla (el botón "Saltar" no queda seleccionado): Start o B.
        var k = Keyboard.current;
        var g = Gamepad.current;
        if ((k != null && k.escapeKey.wasPressedThisFrame)
            || (g != null && (g.startButton.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame))) Saltar();
        if (t >= duracionTotal) Saltar();
    }

    public void Saltar()
    {
        if (terminando) return;
        terminando = true;
        FlujoFader.Instancia.IrAEscena(escenaSiguiente);
    }
}
