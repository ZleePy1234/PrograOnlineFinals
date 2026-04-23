using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    [SerializeField] private float distance;
    [SerializeField] private Transform origin;
    [SerializeField] private LayerMask groundLayers;
    
    public bool isGrounded;
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(CheckRay().origin,CheckRay().direction * distance);
    }

    public bool IsGrounded()
    {
        
        isGrounded = Physics.Raycast(CheckRay(), distance, groundLayers);
        
        return isGrounded;
    }

    public Ray CheckRay()
    {
        return new Ray(origin.position,Vector3.down);
    }
}
