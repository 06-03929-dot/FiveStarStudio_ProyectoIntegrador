using UnityEngine;

// Corredor: cruza el nivel a ras de piso de un lado al otro, con su animación 2D (Derecha / Izquierda).
// Vida (10 %), daño (5 %) y colisiones llegan con el combate (Fase 2-3).
[RequireComponent(typeof(Animator))]
public class EnemigoCorredor : MonoBehaviour
{
    public float velocidad = 6f;
    public float margenFuera = 3f;        // cuánto fuera de pantalla antes de desaparecer

    int direccion = 1;                    // +1 corre a la derecha, -1 a la izquierda
    Animator animator;
    Camera cam;

    public void Iniciar(int dir)
    {
        direccion = dir >= 0 ? 1 : -1;
        animator = GetComponent<Animator>();
        animator.Play(direccion > 0 ? "Derecha" : "Izquierda", 0, 0f);
    }

    void Awake() { cam = Camera.main; animator = GetComponent<Animator>(); }

    void Update()
    {
        transform.position += Vector3.right * (direccion * velocidad * Time.deltaTime);
        if (cam == null) return;
        float mitad = cam.orthographicSize * cam.aspect + margenFuera;
        if (Mathf.Abs(transform.position.x - cam.transform.position.x) > mitad + 1f
            && Mathf.Sign(transform.position.x - cam.transform.position.x) == direccion)
            Destroy(gameObject);
    }
}
