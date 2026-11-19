using System.Collections;
using UnityEngine;

// Vive escondido en un agujero. Cuando el jugador se acerca: asoma la cabeza (aviso),
// da la mordida (aquí sí hace daño), se queda un instante y vuelve a esconderse.
public class BiterEnemy : EnemyBase
{
    [Header("Posiciones (relativas al punto de aparición)")]
    public float hideDepth = 1.5f;      // cuánto se hunde para esconderse
    public float peekHeight = 1.8f;     // altura al asomar (medida desde escondido)
    public float biteHeight = 2.6f;     // altura al morder

    [Header("Tiempos")]
    public float triggerRange = 5f;     // distancia a la que se activa
    public float hiddenTime = 1.5f;
    public float riseTime = 0.25f;
    public float peekTime = 0.5f;       // aviso antes de morder
    public float biteTime = 0.3f;
    public float holdTime = 0.3f;
    public float hideTime = 0.25f;

    Vector3 hiddenPos, peekPos, bitePos;
    bool biting;

    // Solo hace daño durante la mordida y no se puede pisar
    protected override bool Dangerous => biting;
    protected override bool Stompable => false;

    protected override void Awake()
    {
        base.Awake();
        hiddenPos = transform.position + Vector3.down * hideDepth;
        peekPos = hiddenPos + Vector3.up * peekHeight;
        bitePos = hiddenPos + Vector3.up * biteHeight;
        transform.position = hiddenPos;
        col.enabled = false;
    }

    protected override void Start()
    {
        base.Start();
        StartCoroutine(Routine());
    }

    IEnumerator Routine()
    {
        while (true)
        {
            yield return new WaitForSeconds(hiddenTime);

            // Espera a que el jugador esté cerca
            while (player == null || Vector2.Distance(player.position, hiddenPos) > triggerRange)
                yield return null;

            col.enabled = true;
            yield return Move(hiddenPos, peekPos, riseTime);   // asoma la cabeza
            yield return new WaitForSeconds(peekTime);         // aviso

            biting = true;
            anim?.Trigger("Attack");
            yield return Move(peekPos, bitePos, biteTime * 0.5f);   // ¡mordida!
            yield return Move(bitePos, peekPos, biteTime * 0.5f);
            biting = false;

            yield return new WaitForSeconds(holdTime);
            yield return Move(peekPos, hiddenPos, hideTime);   // se esconde
            col.enabled = false;
        }
    }

    IEnumerator Move(Vector3 from, Vector3 to, float time)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, time))
        {
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        transform.position = to;
    }
}
