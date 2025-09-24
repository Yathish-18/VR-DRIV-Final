using UnityEngine;
using UnityEngine.InputSystem;

public class ForkliftHolderController : MonoBehaviour
{
    [Header("References")]
    public Transform holder;
    public Transform mast;
    public Transform lowerPoint;
    public Transform upperPoint;

    [Header("Input")]
    public InputActionAsset inputAsset;

    [Header("Movement Settings")]
    public float moveSpeed = 1f;
    public float sideSpeed = 0.5f;

    [Header("Tilting")]
    public float tiltSpeed = 20f;
    public float minTiltZ = -15f;
    public float maxTiltZ = 15f;

    [Header("Instruction Integration")]
    public ForkliftInstructionManager instructionManager;
    private bool holderMovedNotified = false;

    private float currentTiltZ = 0f;
    private float liftT = 0f;

    private InputAction moveUpAction;
    private InputAction moveDownAction;
    private InputAction moveLeftAction;
    private InputAction moveRightAction;
    private InputAction tiltForwardAction;
    private InputAction tiltBackwardAction;

    private float initialLiftT;

    void Start()
    {
        var map = inputAsset.FindActionMap("Driving");

        moveUpAction = map.FindAction("HolderUp");
        moveDownAction = map.FindAction("HolderDown");
        moveLeftAction = map.FindAction("HolderLeft");
        moveRightAction = map.FindAction("HolderRight");
        tiltForwardAction = map.FindAction("TiltForward");
        tiltBackwardAction = map.FindAction("TiltBackward");

        map.Enable();

        liftT = Mathf.InverseLerp(lowerPoint.position.y, upperPoint.position.y, holder.position.y);
        initialLiftT = liftT;
    }

    void Update()
    {
        HandleLiftMovement();
        HandleSideMovement();
        HandleTilting();
    }

    void HandleLiftMovement()
    {
        float input = 0f;

        if (moveUpAction.ReadValue<float>() > 0.5f) input += 1f;
        if (moveDownAction.ReadValue<float>() > 0.5f) input -= 1f;

        if (Mathf.Abs(input) > 0.01f)
        {
            liftT += input * moveSpeed * Time.deltaTime;
            liftT = Mathf.Clamp01(liftT);
            holder.position = Vector3.Lerp(lowerPoint.position, upperPoint.position, liftT);

            if (!holderMovedNotified && Mathf.Abs(liftT - initialLiftT) > 0.05f)
            {
                holderMovedNotified = true;

                if (instructionManager != null)
                    instructionManager.OnHolderMoved();
            }
        }
    }

    void HandleSideMovement()
    {
        Vector3 move = Vector3.zero;

        if (moveLeftAction.ReadValue<float>() > 0.5f)
            move += Vector3.left * sideSpeed * Time.deltaTime;

        if (moveRightAction.ReadValue<float>() > 0.5f)
            move += Vector3.right * sideSpeed * Time.deltaTime;

        holder.Translate(move, Space.World);
    }

    void HandleTilting()
    {
        float input = 0f;

        if (tiltForwardAction.ReadValue<float>() > 0.5f) input -= 1f;
        if (tiltBackwardAction.ReadValue<float>() > 0.5f) input += 1f;

        currentTiltZ += input * tiltSpeed * Time.deltaTime;
        currentTiltZ = Mathf.Clamp(currentTiltZ, minTiltZ, maxTiltZ);

        mast.localRotation = Quaternion.Euler(180f, 87.94f, currentTiltZ);
    }
}
