using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class PlayerController3D : MonoBehaviour
{
    [Header("Movimiento (eje X)")]
    public float moveSpeed = 12f;
    public float acceleration = 100f;   // en m/s²
    public float deceleration = 80f;    // en m/s²

    [Header("Gravedad y salto")]
    public float gravity = 30f;         // gravedad propia (ya no usa Physics.gravity)
    public float jumpForce = 16f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    public float jumpBufferTime = 0.1f;
    public float coyoteTime = 0.1f;

    [Header("Pared")]
    public float wallSlideSpeed = 2f;
    public Vector2 wallJumpForce = new Vector2(12f, 16f);
    public float wallJumpLockTime = 0.15f;

    [Header("Dash")]
    public float dashSpeed = 25f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.4f;

    [Header("Ataque (golpe)")]
    public float attackDuration = 0.35f;
    public float attackHitDelay = 0.08f;
    public float attackHitWindow = 0.15f;
    public float attackCooldown = 0.45f;
    public float attackRange = 1.4f;
    public float attackHeight = 1.4f;
    public int attackDamage = 1;

    [Header("Modelo 3D")]
    public Transform visual;            // hijo "Visual" con tu modelo (lo pone la herramienta)
    public float turnSpeed = 15f;

    [Header("Detección (se calcula sola desde el collider)")]
    public LayerMask groundLayer;       // pon aquí la capa "Ground"
    public float checkThickness = 0.1f;
    public float skin = 0.05f;

    Rigidbody rb;
    Collider col;

    bool isGrounded, touchingWall, wallOnRight;
    bool isWallSliding, isWallJumping, isDashing, canDash = true;
    bool jumpHeld, dashRequested;
    bool attackRequested;
    float attackElapsed = -1f, attackCooldownCounter;
    readonly HashSet<EnemyBase> attackHit = new HashSet<EnemyBase>();

    float keyH, keyV, touchH, touchV;
    float jumpBufferCounter, coyoteCounter, wallJumpLockCounter;
    float dashTimeCounter, dashCooldownCounter;
    int facing = 1;
    Vector3 dashVelocity;

    float InputH => Mathf.Abs(keyH) > 0.01f ? keyH : touchH;
    float InputV => Mathf.Abs(keyV) > 0.01f ? keyV : touchV;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.useGravity = false; // la gravedad la aplicamos nosotros
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Sin fricción, para que no se quede pegado a las paredes
#if UNITY_6000_0_OR_NEWER
        var mat = new PhysicsMaterial("SinFriccion")
        {
            dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum
        };
#else
        var mat = new PhysicMaterial("SinFriccion")
        {
            dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
            frictionCombine = PhysicMaterialCombine.Minimum
        };
#endif
        col.sharedMaterial = mat;
    }

    // ---------- ENTRADA (solo lectura, la física va en FixedUpdate) ----------
    void Update()
    {
        // El proyecto usa el Input System nuevo (el Input Manager clásico está desactivado).
        var k = Keyboard.current;
        var g = Gamepad.current;
        keyH = Eje(k != null && (k.aKey.isPressed || k.leftArrowKey.isPressed), k != null && (k.dKey.isPressed || k.rightArrowKey.isPressed));
        keyV = Eje(k != null && (k.sKey.isPressed || k.downArrowKey.isPressed), k != null && (k.wKey.isPressed || k.upArrowKey.isPressed));
        if (g != null)
        {
            Vector2 s = g.leftStick.ReadValue();
            if (Mathf.Abs(s.x) > 0.2f) keyH = s.x;
            if (Mathf.Abs(s.y) > 0.2f) keyV = s.y;
        }

        if ((k != null && k.spaceKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame)) OnJumpPressed();
        if ((k != null && k.spaceKey.wasReleasedThisFrame) || (g != null && g.buttonSouth.wasReleasedThisFrame)) OnJumpReleased();
        if ((k != null && (k.leftShiftKey.wasPressedThisFrame || k.jKey.wasPressedThisFrame)) || (g != null && g.buttonEast.wasPressedThisFrame)) OnDashPressed();
        if ((k != null && (k.kKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame)) || (g != null && g.buttonWest.wasPressedThisFrame)) OnAttackPressed();

        // El modelo gira suavemente hacia donde caminas
        if (visual != null)
        {
            Quaternion target = Quaternion.Euler(0f, facing > 0 ? 0f : 180f, 0f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }
    }

    // ---------- FÍSICA ----------
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float h = InputH;
        float v = InputV;
        if (Mathf.Abs(h) > 0.01f) facing = h > 0f ? 1 : -1;

        UpdateSensors();
        HandleAttack(dt);

        bool wantsDash = dashRequested;
        dashRequested = false;
        if (dashCooldownCounter > 0f) dashCooldownCounter -= dt;

        // --- Dash en curso ---
        if (isDashing)
        {
            dashTimeCounter -= dt;
            rb.linearVelocity = dashVelocity;
            if (dashTimeCounter <= 0f)
            {
                isDashing = false;
                dashCooldownCounter = dashCooldown;
                rb.linearVelocity = dashVelocity * 0.5f;
            }
            return;
        }

        if (isGrounded) canDash = true;

        if (wantsDash && canDash && dashCooldownCounter <= 0f)
        {
            Vector3 dir = new Vector3(h, v, 0f);
            if (dir.sqrMagnitude < 0.01f) dir = new Vector3(facing, 0f, 0f);
            dashVelocity = dir.normalized * dashSpeed;
            canDash = false;
            isDashing = true;
            dashTimeCounter = dashDuration;
            rb.linearVelocity = dashVelocity;
            Dashed?.Invoke();
            return;
        }

        // --- Timers ---
        coyoteCounter = isGrounded ? coyoteTime : coyoteCounter - dt;
        jumpBufferCounter -= dt;

        Vector3 vel = rb.linearVelocity;
        int wallDir = wallOnRight ? 1 : -1;

        // --- Deslizar por la pared ---
        isWallSliding = touchingWall && !isGrounded && vel.y < 0f;
        if (isWallSliding) vel.y = -wallSlideSpeed;

        // --- Salto / salto de pared ---
        if (jumpBufferCounter > 0f)
        {
            if (coyoteCounter > 0f)
            {
                vel.y = jumpForce;
                jumpBufferCounter = 0f;
                coyoteCounter = 0f;
                isWallSliding = false;
                Jumped?.Invoke();
            }
            else if (touchingWall)
            {
                isWallJumping = true;
                wallJumpLockCounter = wallJumpLockTime;
                jumpBufferCounter = 0f;
                vel = new Vector3(-wallDir * wallJumpForce.x, wallJumpForce.y, 0f);
                isWallSliding = false;
                Jumped?.Invoke();
            }
        }

        // --- Gravedad propia (caída rápida, salto corto si sueltas el botón) ---
        if (!isWallSliding)
        {
            float mult = 1f;
            if (!isWallJumping)
            {
                if (vel.y < 0f) mult = fallMultiplier;
                else if (vel.y > 0f && !jumpHeld) mult = lowJumpMultiplier;
            }
            vel.y -= gravity * mult * dt;
        }

        // --- Movimiento horizontal ---
        if (isWallJumping)
        {
            wallJumpLockCounter -= dt;
            if (wallJumpLockCounter <= 0f) isWallJumping = false;
        }
        else
        {
            float target = h * moveSpeed;
            float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
            vel.x = Mathf.MoveTowards(vel.x, target, rate * dt);
        }

        vel.z = 0f;
        rb.linearVelocity = vel;
    }

    // ---------- DETECCIÓN (a partir del collider, sin objetos hijos) ----------
    void GetGroundBox(out Vector3 center, out Vector3 half)
    {
        Bounds b = col.bounds;
        float top = b.min.y + skin;
        float bottom = b.min.y - checkThickness;
        center = new Vector3(b.center.x, (top + bottom) * 0.5f, b.center.z);
        half = new Vector3(b.extents.x * 0.9f, (top - bottom) * 0.5f, b.extents.z * 0.9f);
    }

    void GetWallBox(int side, out Vector3 center, out Vector3 half)
    {
        Bounds b = col.bounds;
        float edge = b.center.x + side * b.extents.x;
        float inner = edge - side * skin;
        float outer = edge + side * checkThickness;
        center = new Vector3((inner + outer) * 0.5f, b.center.y, b.center.z);
        half = new Vector3((checkThickness + skin) * 0.5f, b.extents.y * 0.8f, b.extents.z * 0.9f);
    }

    void UpdateSensors()
    {
        Vector3 c, h;
        GetGroundBox(out c, out h);
        isGrounded = Physics.CheckBox(c, h, Quaternion.identity, groundLayer, QueryTriggerInteraction.Ignore);

        GetWallBox(1, out c, out h);
        bool right = Physics.CheckBox(c, h, Quaternion.identity, groundLayer, QueryTriggerInteraction.Ignore);
        GetWallBox(-1, out c, out h);
        bool left = Physics.CheckBox(c, h, Quaternion.identity, groundLayer, QueryTriggerInteraction.Ignore);

        touchingWall = right || left;
        if (right && left) wallOnRight = facing > 0;
        else wallOnRight = right;
    }

    // ---------- MÉTODOS PARA BOTONES TÁCTILES / UI ----------
    public void SetHorizontalInput(float input) => touchH = input;
    public void SetVerticalInput(float input) => touchV = input;

    public void OnJumpPressed()
    {
        jumpBufferCounter = jumpBufferTime;
        jumpHeld = true;
    }

    public void OnJumpReleased()
    {
        jumpHeld = false;
        Vector3 vel = rb.linearVelocity;
        if (vel.y > 0f && !isWallJumping)
        {
            vel.y *= 0.5f;
            rb.linearVelocity = vel;
        }
    }

    public void OnDashPressed() => dashRequested = true;

    // ---------- GOLPE, EVENTOS Y ESTADO (animaciones, tinta) ----------
    public event Action Jumped, Dashed, Attacked;

    public bool IsGrounded => isGrounded;
    public bool IsWallSliding => isWallSliding;
    public bool IsAttacking => attackElapsed >= 0f;
    public float AttackProgress => attackElapsed < 0f ? 0f : Mathf.Clamp01(attackElapsed / attackDuration);
    public int Facing => facing;
    public Vector3 Velocity => rb.linearVelocity;

    public void OnAttackPressed() => attackRequested = true;

    void HandleAttack(float dt)
    {
        if (attackCooldownCounter > 0f) attackCooldownCounter -= dt;

        bool want = attackRequested;
        attackRequested = false;

        if (want && attackCooldownCounter <= 0f && attackElapsed < 0f && !isDashing)
        {
            attackElapsed = 0f;
            attackCooldownCounter = attackCooldown;
            attackHit.Clear();
            Attacked?.Invoke();
        }

        if (attackElapsed < 0f) return;

        attackElapsed += dt;
        if (attackElapsed >= attackHitDelay && attackElapsed <= attackHitDelay + attackHitWindow)
            DoAttackHits();
        if (attackElapsed >= attackDuration) attackElapsed = -1f;
    }

    void DoAttackHits()
    {
        Bounds b = col.bounds;
        Vector3 center = new Vector3(b.center.x + facing * (b.extents.x + attackRange * 0.5f), b.center.y, b.center.z);
        Vector3 half = new Vector3(attackRange * 0.5f, attackHeight * 0.5f, 1.5f);

        foreach (var hit in Physics.OverlapBox(center, half, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Collide))
        {
            var enemy = hit.GetComponentInParent<EnemyBase>();
            if (enemy != null && attackHit.Add(enemy)) enemy.TakeDamage(attackDamage);
        }
    }

    // ---------- USADO POR ENEMIGOS / NIVELES ----------
    public bool IsDashing => isDashing;
    public bool IsFalling => rb.linearVelocity.y < -0.5f;

    // Rebote al pisar un enemigo
    public void Bounce(float force)
    {
        Vector3 vel = rb.linearVelocity;
        vel.y = force;
        rb.linearVelocity = vel;
        jumpBufferCounter = 0f;
    }

    // Empujón al recibir daño (bloquea el control un momento)
    public void Knockback(Vector2 velocity, float lockTime)
    {
        isDashing = false;
        isWallJumping = true;
        wallJumpLockCounter = lockTime;
        rb.linearVelocity = new Vector3(velocity.x, velocity.y, 0f);
    }

    public void Teleport(Vector3 position)
    {
        isDashing = false;
        isWallJumping = false;
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = Vector3.zero;
    }

    // ---------- GIZMOS ----------
    void OnDrawGizmosSelected()
    {
        Collider c = col != null ? col : GetComponent<Collider>();
        if (c == null) return;
        if (col == null) col = c;

        Vector3 center, half;
        GetGroundBox(out center, out half);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, half * 2f);

        Gizmos.color = Color.blue;
        GetWallBox(1, out center, out half);
        Gizmos.DrawWireCube(center, half * 2f);
        GetWallBox(-1, out center, out half);
        Gizmos.DrawWireCube(center, half * 2f);
    }

    static float Eje(bool negativo, bool positivo) { return (positivo ? 1f : 0f) - (negativo ? 1f : 0f); }
}
