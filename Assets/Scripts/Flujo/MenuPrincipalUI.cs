using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MenuPrincipalUI : MonoBehaviour
{
    public IntroLogos intro;
    public GameObject panelMenu;
    public GameObject panelCreditos;
    public GameObject panelSalir;
    public AudioSource musicaMenu;
    public string escenaCinematica = "Cinematica";

    bool iniciando;

    void Awake()
    {
        panelMenu.SetActive(false);
        panelCreditos.SetActive(false);
        panelSalir.SetActive(false);
        intro.alTerminar = () =>
        {
            panelMenu.SetActive(true);
            Seleccionar(panelMenu);
            if (musicaMenu != null && musicaMenu.clip != null) musicaMenu.Play();
        };
    }

    void Update()
    {
        // Esc (teclado) o B / Círculo (control): cerrar la ventana de salida o volver de Créditos.
        var k = Keyboard.current;
        var g = Gamepad.current;
        bool atras = (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
        if (atras)
        {
            if (panelSalir.activeSelf) CancelarSalir();
            else if (panelCreditos.activeSelf) VolverDeCreditos();
        }

        // Si el ratón quitó la selección, el primer movimiento con flechas o control la recupera.
        var es = EventSystem.current;
        bool navega = (g != null && (g.leftStick.ReadValue().sqrMagnitude > 0.25f || g.dpad.ReadValue().sqrMagnitude > 0.25f))
                      || (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame
                                        || k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame));
        if (navega && es != null && (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy))
            Seleccionar(panelSalir.activeSelf ? panelSalir : panelCreditos.activeSelf ? panelCreditos : panelMenu);
    }

    public void IniciarJuego()
    {
        if (iniciando) return;      // evita reiniciar el fundido con doble clic
        iniciando = true;
        FlujoFader.Instancia.IrAEscena(escenaCinematica);
    }

    public void AbrirCreditos()
    {
        FlujoFader.Instancia.Transicion(() => { panelMenu.SetActive(false); panelCreditos.SetActive(true); Seleccionar(panelCreditos); });
    }

    public void VolverDeCreditos()
    {
        FlujoFader.Instancia.Transicion(() => { panelCreditos.SetActive(false); panelMenu.SetActive(true); Seleccionar(panelMenu); });
    }

    public void PedirSalir()
    {
        panelSalir.SetActive(true);
        // Por seguridad, la opción marcada al abrir es "Cancelar".
        var no = panelSalir.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name.EndsWith(" No"));
        Seleccionar(panelSalir, no);
    }

    public void CancelarSalir()
    {
        panelSalir.SetActive(false);
        Seleccionar(panelMenu);
    }

    public void ConfirmarSalir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Sin un botón seleccionado, el EventSystem no puede navegar con el control ni con las flechas.
    static void Seleccionar(GameObject panel, Selectable preferido = null)
    {
        var es = EventSystem.current;
        if (es == null || panel == null) return;
        var destino = preferido != null ? preferido : panel.GetComponentsInChildren<Selectable>(false).FirstOrDefault(s => s.interactable);
        es.SetSelectedGameObject(null);
        if (destino != null) es.SetSelectedGameObject(destino.gameObject);
    }
}
