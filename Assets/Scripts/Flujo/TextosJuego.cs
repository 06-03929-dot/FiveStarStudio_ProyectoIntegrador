// Todos los textos del flujo en un solo lugar. Los marcadores [PDF] hay que
// reemplazarlos por el texto exacto del documento de diseño.
public static class TextosJuego
{
    public const string TituloJuego = "MURTHER IN THE LIMBO";

    public const string Controles =
        "Moverse:  A / D  o  Flechas\n" +
        "Saltar:  Espacio\n" +
        "Esquivar:  Shift  o  J\n" +
        "Atacar:  K  o  X";

    public static readonly string[] FrasesNivel =
    {
        "El casco de Murther aún no es tan resistente como se cree, evita recibir el golpe directo para no ir al sueño eterno",
        "[PDF] Frase del Nivel 2",
        "[PDF] Frase del Nivel 3",
    };

    public const string TituloCreditos = "CRÉDITOS";
    public const string Creditos =
        "Five Star Studios\n\n" +
        "Murther Creations\n\n" +
        "[PDF] Integrantes del equipo y roles\n\n" +
        "Gracias por jugar";

    public const string PreguntaSalir = "¿Estás seguro de que quieres salir del juego?";

    public static readonly string[] PanelesCinematica =
    {
        "[Cómic] Página 1",
        "[Cómic] Página 2",
        "[Cómic] Página 3",
        "[Cómic] Página 4",
        "[Cómic] Página 5",
        "[Cómic] Página 6",
    };
}
