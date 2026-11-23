using JetBrains.Annotations;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum Locks
{
    DoubleJump,
    WallJump,
    Slide,
    Dash
}

[Serializable]
public struct BoxColliderSizes
{
    [SerializeField] private Vector2 _offset;
    public readonly Vector2 offset => _offset;
    [SerializeField] private Vector2 _size;
    public readonly Vector2 size => _size;

    public void ChangeCollider(BoxCollider2D coll)
    {
        coll.offset = _offset;
        coll.size = _size;
    }
}

[Serializable]
public struct Cooldown
{
    [SerializeField] private float cooldown;
    private float lastTriggerTime;

    // public constructor
    public Cooldown(float cooldown = 1f)
    {
        this.cooldown = cooldown;
        lastTriggerTime = -cooldown;
    }

    public void Trigger()
    {
        lastTriggerTime = Time.fixedTime;
    }

    public void Drain()
    {
        lastTriggerTime = Time.fixedTime - cooldown;
    }

    public bool IsReady()
    {
        if (lastTriggerTime < cooldown) return true; // UNLESS it's at the very start :sob:
        return Time.fixedTime - lastTriggerTime > cooldown;
    }

    public float Remaining()
    {
        return Mathf.Max((cooldown + lastTriggerTime - Time.fixedTime) / cooldown, 0);
    }

    public void InfoDebug()
    {
        Debug.Log(cooldown);
        Debug.Log(lastTriggerTime);
    }
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Ability Unlocks")]
    [HideInInspector]
    private bool canDoubleJump = false;
    [HideInInspector]
    private bool canWallJump = false;
    [HideInInspector]
    private bool canSlide = false;
    [HideInInspector]
    private bool canDash = false;

    [Header("Collider Changes")]
    [SerializeField] private BoxColliderSizes normalColl;
    [SerializeField] private BoxColliderSizes crouchColl;
    [SerializeField] private BoxColliderSizes slideColl;

    [Header("Base Stats")]
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Control QOL")]
    [Tooltip("In seconds. Time after leaving the jump window you are still able to jump.")][SerializeField] private Cooldown jumpCoyoteTime = new();
    [Tooltip("In seconds. Time after leaving the jump window you are still able to jump.")][SerializeField] private Cooldown wallCoyoteTime = new();
    [Tooltip("In seconds. Time before enter the jump window you are still able to jump.")][SerializeField] private Cooldown bufferTime = new();
    [SerializeField] private float resistanceFriction = 2;
    [SerializeField] private float normalFriction = 2;
    [SerializeField] private float slidingFriction = 1;

    [Header("Stat Buffs")]
    [SerializeField] private float airAccelerationMultiplier = 0.8f; 
    [SerializeField] private float speedMultiplier = 1f;

    [Header("other stats idk")]
    [HideInInspector]
    private bool canMove = true;
    private bool isGrounded = true;
    private bool isOnWall = false;
    private bool isResisting = false;
    private bool tryingToUncrouch = false;
    private bool isCrouching = false;
    private bool isSliding = false;
    private bool hasSecondJump = false;
    private Vector2 xForce;
    private float storedVelocity;
    private LayerMask ground;

    [Header("Components")]
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private BoxCollider2D playerCollider;
    private Animator animator;

    [Header("Animator Hashing")]
    private static readonly int xSpeedHash = Animator.StringToHash("xSpeed");
    private static readonly int yVelHash = Animator.StringToHash("yVel");
    private static readonly int jumpHash = Animator.StringToHash("jump");
    private static readonly int xResistingHash = Animator.StringToHash("xResisting");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int isCrouchingHash = Animator.StringToHash("isCrouching");
    private static readonly int isOnWallHash = Animator.StringToHash("isOnWall");
    private static readonly int isSlidingHash = Animator.StringToHash("isSliding");

    // -------------------------------------------------------------------------------------- //

    // after everything is done loading
    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        ground = LayerMask.GetMask("Ground");
    }

    // Update is called once per frame
    void Update()
    {
        // flip sprite
        if (rb.linearVelocityX != 0)
        {
            sr.flipX = rb.linearVelocityX < 0;
        }

        // collider changes
        if (isSliding)
        {
            slideColl.ChangeCollider(playerCollider);
        }
        else if (isCrouching)
        {
            crouchColl.ChangeCollider(playerCollider);
        }
        else
        {
            normalColl.ChangeCollider(playerCollider);
        }
    }

    private void LateUpdate()
    {
        // if it's already resisting...
        if (isResisting)
        {
            // ...continue resisting until acceleration stops
            isResisting = Mathf.Abs(rb.linearVelocityX - xForce.x) > Mathf.Abs(rb.linearVelocityX);
            rb.linearDamping = resistanceFriction;
        }
        else
        {
            // ...else consider whether the player was already in motion
            isResisting = Mathf.Abs(rb.linearVelocityX - xForce.x) > Mathf.Abs(rb.linearVelocityX) && Mathf.Abs(rb.linearVelocityX) > acceleration;

            if (isSliding)
            {
                isResisting = false; // never resist while sliding
            }

            // friction
            if (xForce.x == 0 && isGrounded) // isgrounded test ??
            {
                rb.linearDamping = normalFriction;

                // less friction while sliding
                if (isSliding)
                {
                    // if sliding, reduced friction
                    if (CanUncrouch())
                    {
                        rb.linearDamping = slidingFriction;
                    }
                    // if they cannot uncrouch while sliding, remove friction to keep them sliding until they can uncrouch
                    else
                    {
                        rb.linearDamping = 0;
                    }
                }
            }
            else
            {
                rb.linearDamping = 0;
            }
        }

        // animator stuff
        animator.SetFloat(xSpeedHash, Mathf.Abs(rb.linearVelocityX));
        animator.SetFloat(yVelHash, rb.linearVelocityY);
        animator.SetBool(xResistingHash, isResisting);
        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetBool(isCrouchingHash, isCrouching);
        animator.SetBool(isOnWallHash, isOnWall);
        animator.SetBool(isSlidingHash, isSliding);
    }

    private void FixedUpdate()
    {
        isGrounded = GroundCheck();
        isOnWall = WallCheck();

        if (!canMove) return;

        // --------------------------------------------- //
        // give player coyote time for jumping
        if (isGrounded)
        {
            hasSecondJump = true;
            jumpCoyoteTime.Trigger();
        }
        if (isOnWall)
        {
            wallCoyoteTime.Trigger();
        }

        // acceleration modifiers
        float appliedAcceleration = acceleration * Time.fixedDeltaTime * 100;
        if (!isGrounded)
        {
            appliedAcceleration *= airAccelerationMultiplier;
        }
        if (isSliding)
        {
            appliedAcceleration = 0; // no you cant change direction while sliding
        }

        rb.AddForce(appliedAcceleration * xForce); // movement acceleration
        rb.linearVelocityX = Mathf.Clamp(rb.linearVelocityX, -maxSpeed, maxSpeed); // clamp speed

        if (isCrouching && !isSliding)
        {
            rb.linearVelocityX = 0; // no you can't move while crouching
        }

        // --------------------------------------------- //
        // when attempting to uncrouch, if they can't, wait for the next available frame
        if (tryingToUncrouch && CanUncrouch())
        {
            isCrouching = false;
            isSliding = false;
        }
        
        // if unable to jump or not on the wall, store velocities
        if (!isOnWall && wallCoyoteTime.IsReady())
        {
            storedVelocity = rb.linearVelocityX;
        }

        // if within the buffer window
        if (!bufferTime.IsReady())
        {
            // only when touching ground or wall
            if (!wallCoyoteTime.IsReady() || !jumpCoyoteTime.IsReady())
            {
                rb.linearVelocityY = jumpForce;
                isSliding = false; // cancel slide

                wallCoyoteTime.InfoDebug();
                Debug.Log("coyote time remaining: " + jumpCoyoteTime.Remaining() + " | " + "on the ground: " + isGrounded + " | " + "buffer remaining: " + bufferTime.Remaining());

                // boost off wall
                if (!wallCoyoteTime.IsReady() && !isGrounded && canWallJump)
                {
                    Debug.Log("boost");

                    float minBoost = maxSpeed / 2f;
                    float boost = -storedVelocity - Mathf.Clamp(-storedVelocity, -minBoost, minBoost) + minBoost * Mathf.Sign(-storedVelocity);
                    rb.linearVelocityX = boost;

                    wallCoyoteTime.Drain();
                }

                jumpCoyoteTime.Drain();
                bufferTime.Drain();

                // trigger animator
                animator.SetTrigger(jumpHash);
            }

            // in air basically (unlocks under double jump lock)
            else if (hasSecondJump && canDoubleJump)
            {
                rb.linearVelocityY = jumpForce;

                hasSecondJump = false;

                // trigger animator
                animator.SetTrigger(jumpHash);
            }
        }
    }

    // -------------------------------------------------------------------------------------- //

    // ability unlock function
    public void UnlockAbility(Locks name)
    {
        switch (name)
        {
            // not evil cases
            case Locks.DoubleJump:
                canDoubleJump = true;
                return;
            case Locks.WallJump:
                canWallJump = true;
                return;
            case Locks.Slide:
                canSlide = true;
                return;
            case Locks.Dash:
                canDash = true;
                return;

            // evil case
            default:
                return;
        }
    }

    [ContextMenu("Enable Movement")]
    public void EnableMovement()
    {
        canMove = true;
    }

    [ContextMenu("Disable Movement")]
    public void DisableMovement()
    {
        canMove = false;
    }

    private bool GroundCheck()
    {
        // cast thin box at the player's feet and verify the hit normal points upwards
        Vector2 origin = (Vector2)transform.position + playerCollider.offset + playerCollider.bounds.size.y / 2f * Vector2.down;
        Vector2 boxSize = new(playerCollider.bounds.size.x * 0.95f, 0.05f);

        RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0, Vector2.down, 0, ground);
        return hit.collider != null && hit.normal.y > 0.65f;
    }

    private bool WallCheck()
    {
        // always return false if unable to wall jump
        if (!canWallJump) return false;

        // cast box slightly from player's feet
        Vector2 origin = (Vector2)transform.position + playerCollider.offset + playerCollider.bounds.size.x / 2f * Vector2.left;
        Vector2 boxSize = new(0.05f, 0.95f * playerCollider.bounds.size.y);

        RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0, Vector2.right, playerCollider.bounds.size.x, ground);
        return hit.collider != null && Mathf.Abs(hit.normal.x) > 0.65f;
    }

    private bool CanUncrouch()
    {
        // cast thin box at the player's feet and verify the hit normal points upwards
        Vector2 origin = (Vector2)transform.position + normalColl.offset;

        RaycastHit2D hit = Physics2D.BoxCast(origin, normalColl.size, 0, Vector2.down, 0, ground);
        return hit.collider == null;
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 input = ctx.ReadValue<Vector2>();
        float xInput = input.x;

        xForce = new Vector2(xInput, 0);

        // crouching (below a certain threshold so controllers crouch on slight imperfections)
        if (Vector2.Dot(Vector2.up, input) < -0.5f && isGrounded)
        {
            // trigger sliding if the player is going fast enough (unlocks under slide lock)
            if (Math.Abs(rb.linearVelocityX) > 1f && xInput != 0 && canSlide)
            {
                isSliding = true;
            }
            else
            {
                isCrouching = true;
            }

            xForce = Vector2.zero;
            tryingToUncrouch = false;

            // state debugging
            Debug.Log("slide: " + isSliding + " | " + "crouch: " + isCrouching);
        }
        else if (CanUncrouch())
        {
            isCrouching = false;
            isSliding = false;
        }
        else
        {
            tryingToUncrouch = true;
        }
    }

    public void OnCrouch(InputAction.CallbackContext ctx)
    {
        if (!canSlide) return; // locked behind slide lock

        // press
        if (ctx.performed)
        {
            // must be moving at a certain speed
            if (Mathf.Abs(rb.linearVelocityX) > 1f && xForce.x != 0)
            {
                isSliding = true;
            }
            else
            {
                isCrouching = true;
            }

            xForce = Vector2.zero;
            tryingToUncrouch = false;

            // state debugging
            Debug.Log("slide: " + isSliding + " | " + "crouch: " + isCrouching);
        }

        // release
        else if (ctx.canceled)
        {

        }
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        // short circuits
        if (!ctx.performed) return;

        bufferTime.Trigger();
    }

    public void OnDash(InputAction.CallbackContext ctx)
    {
        
    }

    private void OnDrawGizmos()
    {
        if (playerCollider == null) return;

        // cast thin box at the player's feet and verify the hit normal points upwards
        Vector2 origin = (Vector2)transform.position + playerCollider.offset + playerCollider.bounds.size.y / 2f * Vector2.down;
        Vector2 boxSize = new(playerCollider.bounds.size.x * 0.9f, 0.02f);

        // draw the boxcast for debugging purposes
        Gizmos.DrawCube(origin, boxSize);
    }
}
