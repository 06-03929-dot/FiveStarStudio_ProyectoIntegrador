using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController3D))]
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 3;
    public float invulnerableTime = 1f;
    public Vector2 knockback = new Vector2(8f, 10f);
    public float knockbackLock = 0.25f;

    public int Health { get; private set; }
    public event Action Died;
    public event Action Hurt;

    PlayerController3D controller;
    float invulnerableUntil;

    public bool Invulnerable => Time.time < invulnerableUntil;

    void Awake()
    {
        controller = GetComponent<PlayerController3D>();
        Health = maxHealth;
    }

    public void TakeDamage(int amount, Vector3 sourcePosition)
    {
        if (Invulnerable || Health <= 0) return;

        Health = Mathf.Max(0, Health - amount);
        invulnerableUntil = Time.time + invulnerableTime;

        float dir = transform.position.x >= sourcePosition.x ? 1f : -1f;
        controller.Knockback(new Vector2(dir * knockback.x, knockback.y), knockbackLock);
        StartCoroutine(Blink());
        if (Health > 0) Hurt?.Invoke();

        if (Health <= 0) Died?.Invoke();
    }

    public void SetHealth(int value) => Health = Mathf.Clamp(value, 1, maxHealth);

    public void Heal(int amount) => Health = Mathf.Min(maxHealth, Health + amount);

    public void ResetHealth()
    {
        Health = maxHealth;
        invulnerableUntil = Time.time + 1f;
    }

    IEnumerator Blink()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        while (Invulnerable)
        {
            foreach (var r in renderers) r.enabled = !r.enabled;
            yield return new WaitForSeconds(0.08f);
        }
        foreach (var r in renderers) r.enabled = true;
    }
}
