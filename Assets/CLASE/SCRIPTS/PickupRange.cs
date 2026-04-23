using UnityEngine;

public class PickupRange : MonoBehaviour
{   
    public bool isInRange;

    public Rigidbody vaultRigidbody;
    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Vault"))
        {
            Debug.Log("Vault entered pickup range");
            isInRange = true;
            vaultRigidbody = other.attachedRigidbody;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Vault"))
        {
            Debug.Log("Vault exited pickup range");
            vaultRigidbody = null;
            isInRange = false;
        }
    }
}
