using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Pantalla previa a cada nivel (imagen de "Pantallas de carga"): 2 segundos y pasa al nivel.
// Los textos son opcionales; si no se asignan, solo se muestra la imagen.
public class PantallaControles : MonoBehaviour
{
    [Range(1, 3)] public int nivel = 1;
    public string escenaSiguiente = "Nivel1";
    public float duracion = 2f;
    public Text textoTitulo;
    public Text textoControles;
    public Text textoFrase;

    IEnumerator Start()
    {
        if (textoTitulo != null) textoTitulo.text = "NIVEL " + nivel;
        if (textoControles != null) textoControles.text = TextosJuego.Controles;
        if (textoFrase != null) textoFrase.text = "“" + TextosJuego.FrasesNivel[nivel - 1] + "”";
        yield return new WaitForSeconds(duracion + FlujoFader.DuracionDefecto);
        FlujoFader.Instancia.IrAEscena(escenaSiguiente);
    }
}
