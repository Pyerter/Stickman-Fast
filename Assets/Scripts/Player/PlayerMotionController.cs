using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class PlayerMotionController : MonoBehaviour
{
    #region References
    [Header("References")]
    protected PlayerController controller = null;
    public Rigidbody2D RB { get { return controller.RB; } }
    [SerializeField] protected Transform groundChecker;
    [SerializeField] protected Transform bottom;
    [SerializeField] protected Transform visualsTransform;
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
    [SerializeField] protected float jumpMagnetlessBuffer = 0.2f;
    [Header("Grounded Parameters")]
    [SerializeField] protected float groundCheckDistance = 0.1f;
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
    [SerializeField] protected float velocityZeroThreshold = 0.01f;
    #endregion

    #region Tracked Variables
    [Header("Tracked Variables")]
    [SerializeField] protected float previousBoostTime;
    [SerializeField] protected float previousJumpTime;
    RaycastHit2D groundHitInfo;
    bool haveSplinePointCached = false;
    float cachedSplinePoint = 0f;
    float initialGravityScale = 0f;
    bool wasGrounded = false;
    #endregion

    #region Properties
    [Header("Properties")]
    protected bool _grounded;
    public bool Grounded { get { return _grounded || SplineGround != null; } }
    [SerializeField] protected bool breaking;
    public bool Breaking { get { return breaking; } }
    [SerializeField] protected float inputSpeed = 0f;
    public float InputSpeed { get { return inputSpeed; } set { inputSpeed = value; } }
    [SerializeField] protected bool plummeting = false;
    public bool Plummeting { get { return plummeting; } }
    [SerializeField] protected int jumpsLeft;
    public int JumpsLeft { get { return jumpsLeft; } }
    public bool CanMagnet { get { return GameManager.Time >= previousJumpTime + jumpMagnetlessBuffer; } }
    SplineContainer _splineGround = null;
    public SplineContainer SplineGround { get { return _splineGround; } protected set { _splineGround = value; } }
    #endregion

    #region Events
    public event System.Action<PlayerMotionController> onLand;
    public event System.Action<PlayerMotionController> onLeaveGround;
    #endregion

    private void Awake()
    {
        initialGravityScale = RB.gravityScale;
    }

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
        AssertGrounded();
        UpdateHorizontalDirection();
        UpdateVerticalDirection();
        UpdateVisualsRotation();
    }

    public void UpdateVisualsRotation()
    {
        if (!visualsTransform)
            return;
        if (Grounded)
        {
            Quaternion slopeRotation = Quaternion.FromToRotation(Vector2.up, groundHitInfo.normal);
            visualsTransform.rotation = slopeRotation;
        } else
        {
            visualsTransform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }

    public Vector2 TranslateVelocityToNormal(Vector2 velocity)
    {
        if (!Grounded)
            return velocity;
        Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, groundHitInfo.normal);
        Vector2 adjustedVelocity = slopeRotation * velocity;
        return adjustedVelocity;
    }

    public Vector2 TranslateVelocityFromNormal(Vector2 velocity)
    {
        if (!Grounded)
            return velocity;
        Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, groundHitInfo.normal);
        Vector3 eulers = slopeRotation.eulerAngles;
        slopeRotation = Quaternion.Euler(eulers.x, eulers.y, eulers.z * -1);
        Vector2 adjustedVelocity = slopeRotation * velocity;
        return adjustedVelocity;
    }

    protected float ApproximatePercentAlongSpline(SplineContainer spline, float length, Vector3 collPoint, Vector3 nearestPoint, float t, int iterations, float addMult = 1f, float falloffMult = 0.9f, float goodEnough = 0.1f)
    {
        int previousMove = 0;
        bool applyFalloff = false;
        float first_t = t;
        float falloff = 1f;
        float previousDist = Vector3.Distance(collPoint, nearestPoint);
        for (int i = 0; i < iterations; i++)
        {
            float dist = Vector3.Distance(collPoint, nearestPoint);
            if (Utility.IsZero(dist, goodEnough))
                break;
            float approxDiff = (dist / length) * addMult * falloff;
            float approxAdd = t + approxDiff;
            float approxSub = t - approxDiff;
            Vector3 add = spline.EvaluatePosition(approxAdd);
            Vector3 sub = spline.EvaluatePosition(approxSub);
            float addDist = Vector3.Distance(add, collPoint);
            float subDist = Vector3.Distance(sub, collPoint);
            if (addDist < subDist)
            {
                if (previousMove < 0)
                    applyFalloff = true;
                previousMove = 1;
                t = approxAdd;
                nearestPoint = add;
                previousDist = addDist;
            } else
            {
                if (previousMove > 0)
                    applyFalloff = false;
                previousMove = -1;
                t = approxSub;
                nearestPoint = sub;
                previousDist = subDist;
            }
            if (applyFalloff)
                falloff *= falloffMult;
        }
        Debug.Log(string.Format("Approximating collision correction by {0}% to {1}", t-first_t, nearestPoint));
        return t;
    }

    protected void UpdateHorizontalAlongSpline(SplineContainer spline, Vector2 prenormalVelocity)
    {
        float t = cachedSplinePoint;
        float length = spline.CalculateLength();
        float timestepXVelocity = prenormalVelocity.x * GameManager.DeltaTime;
        if (!haveSplinePointCached)
        {
            Vector3 collidePoint3 = new Vector3(groundHitInfo.point.x, groundHitInfo.point.y, 0);
            SplineUtility.GetNearestPoint(spline.Spline, (float3)collidePoint3, out float3 nearestPoint, out t, 100, 4);
            Debug.Log(string.Format("Nearest point on spline from {0} is {1}", collidePoint3, nearestPoint));
            t = ApproximatePercentAlongSpline(spline, length, collidePoint3, nearestPoint, t, 100, 0.2f, 0.9f, 0.02f);
        }
        float diff = timestepXVelocity / length;
        float target_t = diff + t;
        if (target_t < 0 || target_t > 1)
        {
            SplineGround = null;
            haveSplinePointCached = false;
            UpdateHorizontalInWorld(prenormalVelocity);
            return;
        }
        float3 originalSplinePos = spline.EvaluatePosition(t);
        float3 splinePos = spline.EvaluatePosition(target_t);
        Vector3 moveBy = splinePos - originalSplinePos;
        cachedSplinePoint = target_t;
        haveSplinePointCached = true;
        // Vector2 targetPos = new Vector2(splinePos.x, splinePos.y);
        ClaimControlOverPhysics(true);
        prenormalVelocity.y = 0;
        RB.velocity = TranslateVelocityToNormal(prenormalVelocity);
        Vector3 pos = RB.transform.position;
        Vector3 diffToPos = (Vector3)splinePos - bottom.transform.position;
        pos += diffToPos;
        pos.y += 0.01f;
        pos.z = 0;
        RB.transform.position = pos;
        //Debug.Log(string.Format("Based on velocity {0}, moving from {5} by {1}% from {2} to {3}, landing at {4}", timestepXVelocity, diff, t, target_t, splinePos, pos));
    }

    protected void ClaimControlOverPhysics(bool controlling)
    {
        if (controlling)
        {
            RB.isKinematic = true;
            RB.gravityScale = 0;
        } else
        {
            RB.isKinematic = false;
            RB.gravityScale = initialGravityScale;
        }
    }

    protected void UpdateHorizontalInWorld(Vector2 prenormalVelocity)
    {
        ClaimControlOverPhysics(false);
        RB.velocity = TranslateVelocityToNormal(prenormalVelocity);
    }

    public void UpdateHorizontalDirection()
    {
        int hDirection = Utility.Sign(InputSpeed);
        float weightedDirection = Grounded ? hDirection : hDirection * aerialControl;

        Vector3 velocity = TranslateVelocityFromNormal(RB.velocity);
        int velocitySign = Utility.Sign(velocity.x);

        if (hDirection == 0 && Utility.IsZero(velocity.x, velocityZeroThreshold))
        {
            velocity.x = 0;
            RB.velocity = velocity;
            return;
        }

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
        if (Grounded && hDirection == 0 && !Utility.IsZero(velocity.x, velocityZeroThreshold))
        {
            // Calculate slow
            float slowBy = horizontalAcceleration * flatDragAccelerationPercent * GameManager.DeltaTime;
            // Move towards 0 speed by slow amount
            velocity.x = Mathf.MoveTowards(velocity.x, 0, slowBy);

            /*if (Mathf.Abs(velocity.x) < slowBy)
                velocity.x = 0;
            else
                velocity.x -= velocitySign * slowBy;*/
        }

        if (SplineGround == null)
            UpdateHorizontalInWorld(velocity);
        else
            UpdateHorizontalAlongSpline(SplineGround, velocity);
    }

    public void UpdateVerticalDirection()
    {
        Vector3 velocity = RB.velocity;
        if (!RB.isKinematic)
        {
            velocity.y -= playerGravity.y * GameManager.DeltaTime;
            if (Plummeting)
            {
                velocity.y -= plummetAcceleration;
            }
        }
        RB.velocity = velocity;
    }

    public void AssertGrounded()
    {
        wasGrounded = Grounded;
        //bool nowGrounded = Physics2D.OverlapCircle(groundChecker.position, groundCheckRadius, (int)groundMask);
        groundHitInfo = Physics2D.Raycast(groundChecker.position, Vector2.down, groundCheckDistance, groundMask);
        bool nowGrounded = groundHitInfo.collider != null;
        if (!CanMagnet)
            SplineGround = null;
        else if (nowGrounded)
            SplineGround = groundHitInfo.collider.GetComponent<SplineContainer>();
        if (SplineGround == null)
            haveSplinePointCached = false;
            
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
        previousJumpTime = GameManager.Time;
        SplineGround = null;
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
