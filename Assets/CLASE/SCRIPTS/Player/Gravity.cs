using System;
using UnityEngine;

public class Gravity : MonoBehaviour
{
    [SerializeField] private float gravityForce = 9.8f;
    [SerializeField] private bool usesGroundCheck;
    
    private Action ApplyGravity;

    private GroundCheck groundCheck;
    private Rigidbody rb;

    // Start is called before the first frame update
    void Start()
    {
        if (groundCheck == null && usesGroundCheck)
        {
            groundCheck = GetComponent<GroundCheck>();
            ApplyGravity = ApplyGravityByGroundCheck;
        }
        else
        {
            ApplyGravity = ApplyGravityWithoutGroundCheck;
        }

        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        ApplyGravity();
    }

    private float acceleration;
    private void ApplyGravityWithoutGroundCheck()
    { 
        acceleration += Time.deltaTime;
        rb.AddForce(Vector3.down * gravityForce * acceleration, ForceMode.Acceleration);
    }
    
    private void ApplyGravityByGroundCheck()
    {
        if (groundCheck.IsGrounded())
        {
            acceleration = 0;
            return;
        }

        acceleration += Time.deltaTime;
        rb.AddForce(Vector3.down * gravityForce * acceleration, ForceMode.Acceleration);
        
    }
}