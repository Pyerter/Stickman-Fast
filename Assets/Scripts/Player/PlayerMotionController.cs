using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMotionController : MonoBehaviour
{
    #region References
    [Header("References")]
    protected PlayerController controller = null;
    public Rigidbody2D RB { get { return controller.RB; } }
    [SerializeField] protected Transform groundChecker;
    #endregion

    #region Pause Cache
    protected struct PauseCache
    {
        public readonly Vector2 velocity;
        public readonly float angularVelocity;
        public readonly bool valid;

        public PauseCache(Vector2 velocity, float angularVelocity)
        {
            this.velocity = velocity;
            this.angularVelocity = angularVelocity;
            valid = true;
        }
    }
    protected PauseCache pauseCache;
    #endregion

    #region Tweakable Variables
    [Header("Jump Parameters")]
    [SerializeField] protected int maxJumps = 4;
    [SerializeField] protected float jumpSpeed = 2f;
    [SerializeField] protected float aerialControl = 0.5f;
    [SerializeField] protected float plummetAcceleration = 0.25f;
    [Header("Grounded Parameters")]
    [SerializeField] protected float groundCheckRadius = 0.1f;
    [SerializeField] protected LayerMask groundMask;
    [Header("Motion Parameters")]
    [SerializeField] protected float horizontalAcceleration = 2f;
    [SerializeField] protected float snapSpeedThreshold = 10f;
    [SerializeField] protected float snapMultiplier = 40f;
    [SerializeField] protected float breakMultiplier = 20f;
    [SerializeField] protected float flatDragAccelerationPercent = 0.5f;
    [SerializeField] protected float boostSpeed = 20f;
    [SerializeField] protected float boostCooldownTimer = 5f;
    [SerializeField] protected Vector2 playerGravity = new Vector2(0f, 1f);
    #endregion

    #region Tracked Variables
    [Header("Tracked Variables")]
    [SerializeField] protected float previousBoostTime;
    #endregion

    #region Properties
    [Header("Properties")]
    protected bool _grounded;
    public bool Grounded { get { return _grounded; } }
    [SerializeField] protected bool breaking;
    public bool Breaking { get { return breaking; } }
    [SerializeField] protected float inputSpeed = 0f;
    public float InputSpeed { get { return inputSpeed; } set { inputSpeed = value; } }
    [SerializeField] protected bool plummeting = false;
    public bool Plummeting { get { return plummeting; } }
    [SerializeField] protected int jumpsLeft;
    public int JumpsLeft { get { return jumpsLeft; } }
    #endregion

    #region Events
    public event System.Action<PlayerMotionController> onLand;
    public event System.Action<PlayerMotionController> onLeaveGround;
    #endregion

    #region Init and Pausing
    public void Init(PlayerController controller)
    {
        if (this.controller != null)
            return;

        this.controller = controller;
        onLeaveGround += LoseJumpsOnLeaveGround;
        onLand += RegainJumps;
        onLand += (mc) => controller.UpdateJumpDisplay();
    }

    private void LoseJumpsOnLeaveGround(PlayerMotionController mc)
    {
        if (jumpsLeft == maxJumps)
            jumpsLeft--;
    }

    public void RegainJumps(PlayerMotionController mc)
    {
        jumpsLeft = maxJumps;
    }

    public void OnPause(GameManager gm, bool paused)
    {
        if (paused)
        {
            pauseCache = new PauseCache(RB.velocity, RB.angularVelocity);
            RB.velocity = Vector3.zero;
            RB.angularVelocity = 0f;
            RB.isKinematic = true;
        }
        else
        {
            RB.isKinematic = false;
            RB.velocity = pauseCache.velocity;
            RB.angularVelocity = pauseCache.angularVelocity;
            pauseCache = default;
        }
    }
    #endregion

    #region Normal Motion
    public void UpdateAsPaused()
    {
        previousBoostTime += Time.fixedDeltaTime;
    }

    public void UpdateMotion()
    {
        UpdateHorizontalDirection();
        UpdateVerticalDirection();
        AssertGrounded();
    }

    public void UpdateHorizontalDirection()
    {
        int hDirection = Utility.Sign(InputSpeed);
        float weightedDirection = Grounded ? hDirection : hDirection * aerialControl;

        Vector3 velocity = RB.velocity;
        int velocitySign = Utility.Sign(velocity.x);

        bool reversing = hDirection == -velocitySign;
        breaking = Grounded && reversing;
        bool notAccelerating = hDirection != velocitySign;

        // Calculate weighted direction based off of current control for not accelerating
        if (notAccelerating)
        {
            weightedDirection *= Grounded ? breakMultiplier : breakMultiplier * aerialControl * aerialControl;
        }
        // Calculate weighted direction for snapping to a minimum speed
        else if (Mathf.Abs(RB.velocity.x) < snapSpeedThreshold)
        {
            weightedDirection *= Grounded ? snapMultiplier : snapMultiplier * aerialControl;
        }

        // Calculate acceleration based on weighted direction and horizontal direction
        float velocityAddition = horizontalAcceleration * weightedDirection * GameManager.DeltaTime;
        velocity.x += velocityAddition;

        // If no input, grounded, and still has momentum, let player naturally slow down their run
        if (Grounded && hDirection == 0 && velocitySign != 0)
        {
            // Calculate slow
            float slowBy = horizontalAcceleration * flatDragAccelerationPercent * GameManager.DeltaTime;
            // Move towards 0 speed by slow amount
            velocity.x = Mathf.MoveTowards(velocity.x, 0, -velocitySign * slowBy);

            /*if (Mathf.Abs(velocity.x) < slowBy)
                velocity.x = 0;
            else
                velocity.x -= velocitySign * slowBy;*/
        }
        RB.velocity = velocity;
    }

    public void UpdateVerticalDirection()
    {
        Vector3 velocity = RB.velocity;
        velocity.y -= playerGravity.y * GameManager.DeltaTime;
        if (Plummeting)
        {
            velocity.y -= plummetAcceleration;
        }
        RB.velocity = velocity;
    }

    public void AssertGrounded()
    {
        bool nowGrounded = Physics2D.OverlapCircle(groundChecker.position, groundCheckRadius, (int)groundMask);
        if (nowGrounded && !Grounded)
        {
            onLand?.Invoke(this);
        } else if (!nowGrounded && Grounded)
        {
            onLeaveGround?.Invoke(this);
        }
        _grounded = nowGrounded;
    }
    #endregion

    #region Actions
    public bool CanJump()
    {
        return Grounded || jumpsLeft > 0;
    }

    public void Jump()
    {
        jumpsLeft--;
        Vector3 velocity = RB.velocity;
        velocity.y = jumpSpeed;
        RB.velocity = velocity;
    }

    public bool CanBoost()
    {
        return GameManager.Time - previousBoostTime >= boostCooldownTimer;
    }

    public float BoostPercentReady()
    {
        return (GameManager.Time - previousBoostTime) / boostCooldownTimer;
    }

    public void Boost()
    {
        previousBoostTime = GameManager.Time;
        Vector3 velocity = RB.velocity;
        float velAngle = Mathf.Atan2(velocity.y, velocity.x);
        Vector3 boostVelocity = new Vector3(Mathf.Cos(velAngle) * boostSpeed, Mathf.Sin(velAngle) * boostSpeed, 0);
        velocity += boostVelocity;
        RB.velocity = velocity;
    }

    public bool CanReverseMotion()
    {
        return Grounded || jumpsLeft > 0;
    }

    public void ReverseMotion()
    {
        if (!Grounded)
            jumpsLeft--;
        Vector3 velocity = RB.velocity;
        velocity.x *= -1;
        RB.velocity = velocity;
    }

    public bool CanPlummet()
    {
        return true;
    }

    public void Plummet()
    {
        Vector3 velocity = RB.velocity;
        if (velocity.y > 0)
            velocity.y = -plummetAcceleration;
        RB.velocity = velocity;
        plummeting = true;
    }
    
    public void StopPlummet()
    {
        plummeting = false;
    }
    #endregion
}
