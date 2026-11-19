using System.Collections.Generic;
using UnityEngine;

// Manda valores al Animator solo si el parámetro existe (evita avisos en la consola).
public class AnimHelper
{
    readonly Animator animator;
    readonly HashSet<string> names = new HashSet<string>();

    public AnimHelper(Animator a)
    {
        animator = a;
        if (a != null && a.runtimeAnimatorController != null)
            foreach (var p in a.parameters) names.Add(p.name);
    }

    public void Float(string n, float v) { if (names.Contains(n)) animator.SetFloat(n, v); }
    public void Bool(string n, bool v) { if (names.Contains(n)) animator.SetBool(n, v); }
    public void Trigger(string n) { if (names.Contains(n)) animator.SetTrigger(n); }
}
