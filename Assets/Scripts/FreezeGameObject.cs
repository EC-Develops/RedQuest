using UnityEngine;

public class FreezeGameObject: MonoBehaviour
{
    Rigidbody rb;
    public bool isFrozen = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FreezePosition()
    {
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
        isFrozen = true;
    }

    void UnfreezPosition()
    {
        rb.constraints = RigidbodyConstraints.None;
        isFrozen = false;
    }
}
