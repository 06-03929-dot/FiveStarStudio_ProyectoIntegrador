using UnityEngine;

// Enemigo 2D que solo reproduce una de sus animaciones (vista previa hasta tener su comportamiento).
[RequireComponent(typeof(Animator))]
public class EnemigoDecorativo : MonoBehaviour
{
    public string estado = "Derecha";

    void Start() { GetComponent<Animator>().Play(estado, 0, 0f); }
}
