using UnityEngine;

public class MenuPrincipalUI : MonoBehaviour
{
    public IntroLogos intro;
    public GameObject panelMenu;
    public GameObject panelCreditos;
    public GameObject panelSalir;
    public AudioSource musicaMenu;
    public string escenaCinematica = "Cinematica";

    void Awake()
    {
        panelMenu.SetActive(false);
        panelCreditos.SetActive(false);
        panelSalir.SetActive(false);
        intro.alTerminar = () =>
        {
            panelMenu.SetActive(true);
            if (musicaMenu != null && musicaMenu.clip != null) musicaMenu.Play();
        };
    }

    public void IniciarJuego() { FlujoFader.Instancia.IrAEscena(escenaCinematica); }

    public void AbrirCreditos()
    {
        FlujoFader.Instancia.Transicion(() => { panelMenu.SetActive(false); panelCreditos.SetActive(true); });
    }

    public void VolverDeCreditos()
    {
        FlujoFader.Instancia.Transicion(() => { panelCreditos.SetActive(false); panelMenu.SetActive(true); });
    }

    public void PedirSalir() { panelSalir.SetActive(true); }
    public void CancelarSalir() { panelSalir.SetActive(false); }

    public void ConfirmarSalir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
