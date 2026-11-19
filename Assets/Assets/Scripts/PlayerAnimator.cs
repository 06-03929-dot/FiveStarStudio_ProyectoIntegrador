using UnityEngine;

// Conecta el jugador con el Animator de tu modelo 3D (correr, saltar, atacar, dash, daño).
[RequireComponent(typeof(PlayerController3D))]
public class PlayerAnimator : MonoBehaviour
{
    PlayerController3D pc;
    PlayerHealth health;
    AnimHelper anim;

    void Start()
    {
        pc = GetComponent<PlayerController3D>();
        health = GetComponent<PlayerHealth>();
        anim = new AnimHelper(GetComponentInChildren<Animator>());

        pc.Jumped += OnJump;
        pc.Dashed += OnDash;
        pc.Attacked += OnAttack;
        if (health != null) health.Hurt += OnHurt;
    }

    void OnDestroy()
    {
        if (pc != null)
        {
            pc.Jumped -= OnJump;
            pc.Dashed -= OnDash;
            pc.Attacked -= OnAttack;
        }
        if (health != null) health.Hurt -= OnHurt;
    }

    void Update()
    {
        if (anim == null || pc == null) return;
        Vector3 v = pc.Velocity;
        anim.Float("Speed", Mathf.Abs(v.x));
        anim.Float("VelY", v.y);
        anim.Bool("Grounded", pc.IsGrounded);
    }

    void OnJump() => anim?.Trigger("Jump");
    void OnDash() => anim?.Trigger("Dash");
    void OnAttack() => anim?.Trigger("Attack");
    void OnHurt() => anim?.Trigger("Hurt");
}
