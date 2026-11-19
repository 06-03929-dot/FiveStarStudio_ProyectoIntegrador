using System.Collections;
using UnityEngine;

// Arquero que flota en el aire, patrulla un poco y dispara flechas al jugador.
public class FlyingArcher : EnemyBase
{
    [Header("Flotar y patrullar")]
    public float hoverAmplitude = 0.5f;
    public float hoverSpeed = 2f;
    public float patrolDistance = 3f;
    public float patrolSpeed = 1.5f;

    [Header("Disparo")]
    public Arrow arrowPrefab;
    public Transform shootPoint;        // opcional: si está vacío dispara desde el centro
    public float detectionRange = 14f;
    public float fireCooldown = 2.2f;
    public float aimTime = 0.5f;        // tiempo de aviso antes de disparar

    Vector3 startPos;
    float t;
    float nextShot;
    bool aiming;

    protected override void Awake()
    {
        base.Awake();
        startPos = transform.position;
        nextShot = Time.time + 1f;
    }

    void Update()
    {
        t += Time.deltaTime;

        // Movimiento: va y viene en X, y sube y baja suavemente
        float x = startPos.x + Mathf.Sin(t * patrolSpeed / Mathf.Max(0.1f, patrolDistance)) * patrolDistance;
        float y = startPos.y + Mathf.Sin(t * hoverSpeed) * hoverAmplitude;
        transform.position = new Vector3(x, y, startPos.z);

        if (player == null) return;

        // Mirar hacia el jugador
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (player.position.x >= transform.position.x ? 1f : -1f);
        transform.localScale = s;

        // Disparar si lo ve y ya pasó el cooldown
        float dist = Vector2.Distance(player.position, transform.position);
        if (!aiming && Time.time >= nextShot && dist <= detectionRange)
            StartCoroutine(Shoot());
    }

    IEnumerator Shoot()
    {
        aiming = true;
        anim?.Trigger("Attack");
        yield return new WaitForSeconds(aimTime);

        if (player != null && arrowPrefab != null)
        {
            Vector3 origin = shootPoint != null ? shootPoint.position : transform.position;
            Vector3 dir = player.position - origin;
            dir.z = 0f;

            Arrow arrow = Instantiate(arrowPrefab, origin, Quaternion.identity);
            arrow.Launch(dir);
        }

        nextShot = Time.time + fireCooldown;
        aiming = false;
    }
}
