using Fusion;
using UnityEngine;

public class CashoutScript : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Vault"))
        {
            Debug.Log("Vault Depositado");
            if (GameManager.Instance != null)
            {
                Destroy(other.gameObject);
                GameManager.Instance.CashoutComplete(0);
            }
        }
    }
}
