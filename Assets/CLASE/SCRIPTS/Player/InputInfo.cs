using Fusion;
using UnityEngine;

public struct InputInfo : INetworkInput
{
    public Vector2 playerPos;
    public Vector2 lookDirection;

    public bool isMoving;
    public bool isMovingBackwards;
    public bool isRunInputPressed;
    public bool isMovingOnXAxis;
    public bool fire;
    public bool carryingBox;
}
