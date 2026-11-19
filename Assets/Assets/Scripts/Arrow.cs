using UnityEngine;

[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class Arrow : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 1;
    public float lifeTime = 4f;
    public LayerMask groundLayer;   // la flecha desaparece al chocar con esta capa

    Vector3 direction = Vector3.left;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void Launch(Vector3 dir)
    {
        direction = new Vector3(dir.x, dir.y, 0f).normalized;
        transform.right = direction;    // el modelo debe apuntar hacia +X
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        var hp = other.GetComponentInParent<PlayerHealth>();
        if (hp != null)
        {
            hp.TakeDamage(damage, transform.position);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & groundLayer) != 0)
            Destroy(gameObject);
    }
}
