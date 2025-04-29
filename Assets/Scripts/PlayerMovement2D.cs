using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Cinemachine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement2D : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("References")]
    [Tooltip("Arraste o GameObject do Joystick Virtual da sua UI aqui")]
    [SerializeField] private Joystick virtualJoystick;

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private Collider2D playerCollider;

    private Vector2 moveInputFromAction = Vector2.zero;

    private CinemachineVirtualCamera virtualCamera;

    private PlayerVisualAnimation visualAnimator;

    [SyncVar(hook = nameof(OnIsMovingChanged))]
    private bool isMoving = false;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
        playerCollider = GetComponent<Collider2D>();

        visualAnimator = GetComponent<PlayerVisualAnimation>();
        if(visualAnimator == null) Debug.LogWarning("PlayerVisualAnimation script not found on player prefab!");


        if (rb.bodyType != RigidbodyType2D.Dynamic)
        {
            Debug.LogWarning("Rigidbody2D BodyType is not Dynamic. Changing to Dynamic.", this);
            rb.bodyType = RigidbodyType2D.Dynamic;
        }
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (virtualJoystick == null)
        {
            virtualJoystick = FindObjectOfType<Joystick>();
             #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (virtualJoystick == null) Debug.LogWarning("Virtual Joystick not found in scene.");
             #endif
        }
    }

    void FixedUpdate()
    {

        Vector2 currentMoveInput = Vector2.zero;

        if (isLocalPlayer && playerInput != null && playerInput.enabled)
        {
            if (virtualJoystick != null && virtualJoystick.Direction.magnitude > virtualJoystick.DeadZone)
            {
                 currentMoveInput = virtualJoystick.Direction.normalized;
            }
            else
            {
                 currentMoveInput = moveInputFromAction.normalized;
            }
        }

        if (isLocalPlayer)
        {
            rb.velocity = currentMoveInput * moveSpeed;

            bool currentlyMoving = currentMoveInput.magnitude > 0.1f;
            if (currentlyMoving != isMoving)
            {
                 CmdSetIsMoving(currentlyMoving);
            }

        }

    }

    [Command]
    void CmdSetIsMoving(bool moving)
    {
        isMoving = moving;
    }


    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLocalPlayer) return;

        TrashItem trashItem = other.GetComponent<TrashItem>();
        if (trashItem != null)
        {
            Debug.Log("Local player collided with TrashItem trigger. Sending CmdCollectTrash.");
            CmdCollectTrash(trashItem.GetComponent<NetworkIdentity>());
        }
    }

    [Command]
    void CmdCollectTrash(NetworkIdentity trashNetId)
    {
        if (NetworkServer.spawned.TryGetValue(trashNetId.netId, out NetworkIdentity trashIdentity))
        {
            GameObject trashObject = trashIdentity.gameObject;
            if (trashObject != null)
            {
                TrashItem trashItem = trashObject.GetComponent<TrashItem>();
                if (trashItem != null)
                {
                    trashItem.RpcAnimateCollection();

                    NetworkServer.Destroy(trashObject);

                    GameManager.Instance?.IncrementCollectedCount();
                } else { Debug.LogWarning($"Server: Object {trashNetId.netId} missing TrashItem."); }
            } else { Debug.LogWarning($"Server: Object {trashNetId.netId} gameObject is null after TryGetValue."); }
        } else { Debug.LogWarning($"Server: Collection failed for {trashNetId.netId}. Not found on server."); }
    }


    void OnMove(InputValue value)
    {
        moveInputFromAction = value.Get<Vector2>();
    }

    void OnIsMovingChanged(bool oldIsMoving, bool newIsMoving)
    {

        if (visualAnimator != null)
        {
            if (newIsMoving)
            {
                 visualAnimator.StartMoveAnimation();
            }
            else
            {
                 visualAnimator.StopMoveAnimation();
            }
        } else {
             Debug.LogWarning("PlayerVisualAnimation script reference is null. Cannot control animation.");
        }
    }


    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer)
        {
        }
    }

     public override void OnStartLocalPlayer()
     {
         base.OnStartLocalPlayer();
         Debug.Log("OnStartLocalPlayer called. This is the local player. Enabling input and camera.");

         playerInput = GetComponent<PlayerInput>();
         if (playerInput != null)
         {
             playerInput.enabled = true;
             Debug.Log("PlayerInput component enabled for local player.");
         } else {
              Debug.LogError("PlayerInput component NOT FOUND on local player prefab! Cannot enable input.");
         }

         CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
         if (virtualCamera != null)
         {
             virtualCamera.Follow = this.transform;
             Debug.Log("Cinemachine Camera Follow target set to local player.");
         } else { Debug.LogWarning("Cinemachine Virtual Camera not found!"); }

         if (virtualJoystick != null)
         {
         }
     }

    public override void OnStopClient()
    {
        base.OnStopClient();
    }

     void OnDestroy()
     {
         if (isLocalPlayer)
         {
            CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            if (virtualCamera != null && virtualCamera.Follow == this.transform)
            {
                virtualCamera.Follow = null;
                Debug.Log("Cinemachine Camera Follow target cleared on player destroy.");
            }
         }
     }
}