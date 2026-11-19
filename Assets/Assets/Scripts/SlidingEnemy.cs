using UnityEngine;

// Enemigo que se desliza por el suelo, da la vuelta en bordes y paredes,
// y acelera hacia el jugador cuando lo ve cerca.
[RequireComponent(typeof(Rigidbody))]
public class SlidingEnemy : EnemyBase
{
    [Header("Deslizamiento")]
    public float speed = 4f;
    public float chargeRange = 6f;
    public float chargeMultiplier = 2f;
    public LayerMask groundLayer;

    Rigidbody rb;
    int dir = -1;
    bool charging;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void FixedUpdate()
    {
        Bounds b = col.bounds;
        Vector3 pos = rb.position;
        float current = speed;
        bool chargingNow = false;

        // Si el jugador está cerca y a la misma altura, se lanza hacia él
        if (player != null)
        {
            float dx = player.position.x - pos.x;
            if (Mathf.Abs(dx) < chargeRange && Mathf.Abs(player.position.y - pos.y) < 1.5f)
            {
                SetDirection(dx > 0f ? 1 : -1);
                current = speed * chargeMultiplier;
                chargingNow = true;
            }
        }

        if (chargingNow && !charging) anim?.Trigger("Attack");
        charging = chargingNow;
        anim?.Float("Speed", current);

        // Pared adelante
        if (Physics.Raycast(b.center, Vector3.right * dir, b.extents.x + 0.1f, groundLayer, QueryTriggerInteraction.Ignore))
            SetDirection(-dir);

        // Borde adelante (no hay suelo)
        Vector3 front = new Vector3(b.center.x + dir * (b.extents.x + 0.1f), b.min.y + 0.1f, b.center.z);
        if (!Physics.Raycast(front, Vector3.down, 0.6f, groundLayer, QueryTriggerInteraction.Ignore))
            SetDirection(-dir);

        pos.x += dir * current * Time.fixedDeltaTime;

        // Pegarse al suelo
        if (Physics.Raycast(new Vector3(pos.x, b.min.y + 0.5f, pos.z), Vector3.down, out RaycastHit hit, 2f, groundLayer, QueryTriggerInteraction.Ignore))
            pos.y = hit.point.y + b.extents.y;

        rb.MovePosition(pos);
    }

    void SetDirection(int newDir)
    {
        if (newDir == dir) return;
        dir = newDir;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        transform.localScale = s;
    }
}
