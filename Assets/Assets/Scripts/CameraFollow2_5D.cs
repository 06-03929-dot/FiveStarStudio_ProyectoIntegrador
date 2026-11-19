using UnityEngine;

public class CameraFollow2_5D : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3f, -20f);
    public float smooth = 6f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }
}
