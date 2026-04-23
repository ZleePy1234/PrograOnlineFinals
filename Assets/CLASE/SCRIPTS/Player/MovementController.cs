using UnityEngine;
using Fusion;
using System;
using System.Reflection;
using Unity.VisualScripting;

public class MovementController : NetworkBehaviour
{
    private NetworkCharacterController fusionController;
    private Component simpleKccComponent;
    private MethodInfo simpleKccMoveMethodOneParam;
    private MethodInfo simpleKccMoveMethodTwoParams;

    [SerializeField] private Animator animator;

    private InputInfo input;

    [SerializeField] private float walkSpeed = 5.5f;
    [SerializeField] private float runSpeed = 7.7f;
    [SerializeField] private float crouchSpeed = 3.9f;

    private bool isDead;

    public override void Spawned()
    {
        fusionController = GetComponent<NetworkCharacterController>();
        CacheSimpleKccApi();
    }

    public override void FixedUpdateNetwork()
    {
        if (isDead) return;

        if (GetInput(out input))
        {
            Movement();
            Animation();
            BoxInteraction();
        }
    }

    public void OnDeath()
    {
        isGrabbing = false;
        DropBox();
        isDead = true;

    }

    public void OnRespawn()
    {
        isGrabbing = false;
        isDead = false;

    }

    private void Animation()
    {
        if (animator == null) return;

        animator.SetBool("IsWalking", input.isMoving);
        animator.SetBool("IsRunning", input.isRunInputPressed);
        animator.SetFloat("WalkingZ", input.playerPos.y);
        animator.SetFloat("WalkingX", input.playerPos.x);
    }

    private void Movement()
    {
        Vector3 inputDirection = new Vector3(input.playerPos.x, 0f, input.playerPos.y);
        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        float speed = Speed(input);

        CameraController cam = GetComponentInChildren<CameraController>();
        if (cam == null) cam = FindObjectOfType<CameraController>();

        Quaternion yawRotation = Quaternion.Euler(0f, cam != null ? cam.YawAngle : 0f, 0f);
        Vector3 worldDirection = yawRotation * inputDirection * speed;

        if (fusionController != null)
        {
            fusionController.Move(worldDirection);
            return;
        }

        if (simpleKccComponent != null)
        {
            if (simpleKccMoveMethodOneParam != null)
            {
                simpleKccMoveMethodOneParam.Invoke(simpleKccComponent, new object[] { worldDirection });
                return;
            }

            if (simpleKccMoveMethodTwoParams != null)
            {
                simpleKccMoveMethodTwoParams.Invoke(simpleKccComponent, new object[] { worldDirection, 0f });
            }
        }
    }

    private float Speed(InputInfo info)
    {
        return info.isMovingBackwards || info.isMovingOnXAxis ? walkSpeed :
            info.isRunInputPressed ? runSpeed : walkSpeed;
    }

    private void CacheSimpleKccApi()
    {
        simpleKccComponent = null;
        simpleKccMoveMethodOneParam = null;
        simpleKccMoveMethodTwoParams = null;

        Component[] components = GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null) continue;

            Type type = component.GetType();
            if (type.Name == "SimpleKCC")
            {
                simpleKccComponent = component;

                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                foreach (var method in methods)
                {
                    if (method.Name == "Move")
                    {
                        var parameters = method.GetParameters();

                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Vector3))
                            simpleKccMoveMethodOneParam = method;
                        else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Vector3))
                            simpleKccMoveMethodTwoParams = method;
                    }
                }
                break;
            }
        }
    }

    private Rigidbody grabbedObject;
    public Transform boxHoldPoint;
    public bool isGrabbing;

    private void BoxInteraction()
    {
        if(input.carryingBox && !isGrabbing)
        {
            Debug.Log("Trying to grab box...");
            GrabBox();
        }
        else if (!input.carryingBox && isGrabbing)
        {
            Debug.Log("Dropping box...");
            DropBox();
            isGrabbing = false;
        }
        if (isGrabbing)
        {
            Debug.Log("Updating box position...");
            UpdateBox();
        }
    }
    private void GrabBox()
    {
        PickupRange pickupRange = boxHoldPoint.GetComponent<PickupRange>();
        if (pickupRange != null && pickupRange.vaultRigidbody != null)
        {
            // Optionally, store reference to grabbed object for DropBox
            grabbedObject = pickupRange.vaultRigidbody;
            // Enable physics interaction
            grabbedObject.isKinematic = false;
            grabbedObject.useGravity = false;

            isGrabbing = true;
        }
    }

    private void UpdateBox()
    {
        Vector3 targetPos = boxHoldPoint.position;
        Vector3 forceDir = (targetPos - grabbedObject.position);

        // Apply force to move object towards shootpoint
        float forceStrength = 5f * forceDir.magnitude; // Adjust strength as needed
        grabbedObject.AddForce(forceDir * forceStrength);
    }

    private void DropBox()
    {
        if (grabbedObject != null)
        {
            // Optionally, reset any changes made to the object
            grabbedObject.isKinematic = false;
            grabbedObject.useGravity = true;

            // Clear reference to dropped object
            grabbedObject = null;
        }
    }
}