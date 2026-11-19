using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Enemigo")]
    public int health = 1;
    public int contactDamage = 1;
    public float stompBounce = 14f;

    public event Action<EnemyBase> Died;

    protected Transform player;
    protected Collider col;
    protected AnimHelper anim;
    bool dead;

    // Cada enemigo puede cambiar estas reglas
    protected virtual bool Dangerous => true;   // ¿hace daño al tocarlo?
    protected virtual bool Stompable => true;   // ¿se le puede pisar?

    protected virtual void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    protected virtual void Start()
    {
        anim = new AnimHelper(GetComponentInChildren<Animator>());

        // Si lo pusiste a mano en la escena (sin LevelManager), busca al jugador solo
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController3D>();
            if (pc != null) player = pc.transform;
        }
    }

    public void Init(Transform playerTransform) => player = playerTransform;

    public void TakeDamage(int amount)
    {
        if (dead) return;
        health -= amount;
        if (health <= 0) Die();
    }

    void Die()
    {
        dead = true;
        Died?.Invoke(this);
        Destroy(gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        if (dead || !col.enabled) return;

        var pc = other.GetComponentInParent<PlayerController3D>();
        if (pc == null) return;

        // 1) El dash derrota al enemigo
        if (pc.IsDashing)
        {
            TakeDamage(999);
            return;
        }

        // 2) Pisotón: el jugador cae encima
        if (Stompable && pc.IsFalling && other.bounds.min.y > col.bounds.center.y)
        {
            TakeDamage(1);
            pc.Bounce(stompBounce);
            return;
        }

        // 3) Si no, hace daño
        if (Dangerous && pc.TryGetComponent(out PlayerHealth hp))
            hp.TakeDamage(contactDamage, transform.position);
    }
}
