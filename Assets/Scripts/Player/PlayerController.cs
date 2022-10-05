using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Cinemachine;
using TMPro;

public class PlayerController : MonoBehaviour, PlayerControls.IInPlayActions
{
    PlayerControls controls;

    [Header("References")]
    [SerializeField] public Animator animator;
    [SerializeField] public Rigidbody2D rigidbody;
    [SerializeField] public PlayerAttackController attackController;
    [SerializeField] public CinemachineVirtualCamera cam;
    [SerializeField] public Transform groundChecker;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] public Slider boostMeter;
    [SerializeField] public Slider healthMeter;
    [SerializeField] public Image jumpImage;
    [SerializeField] public Image jompImage;
    [SerializeField] public Image jampImage;
    [SerializeField] public Image jumpXImage;
    [SerializeField] public Image jompXImage;
    [SerializeField] public Image jampXImage;
    [SerializeField] public TMPro.TextMeshProUGUI comboText;
    [SerializeField] public TMPro.TextMeshProUGUI scoreText;
    [SerializeField] public TMPro.TextMeshProUGUI restartText;
    [SerializeField] public TMPro.TextMeshProUGUI helpText;
    [SerializeField] public Material mat;

    [Header("Tweakable Parameters")]
    [SerializeField] public float maxCamOrthoSize = 40f;
    [SerializeField] private float orthoSizeSpeedBase = 20f;
    [SerializeField] private float orthoSizeSpeedScale = 3f;
    [SerializeField] private float groundCheckRadius = 0.1f;
    [SerializeField] public float horizontalAcceleration = 2f;
    [SerializeField] public float snapSpeedThreshold = 10f;
    [SerializeField] public float snapMultiplier = 40f;
    [SerializeField] public float breakMultiplier = 20f;
    [SerializeField] public float flatDragAccelerationPercent = 0.5f;
    [SerializeField] public float aerialControl = 0.5f;
    [SerializeField] public float jumpSpeed = 2f;
    [SerializeField] public float jumpRequestBuffer = 0.5f;
    [SerializeField] public float boostSpeed = 20f;
    [SerializeField] public float boostRequestBuffer = 0.2f;
    [SerializeField] public float boostCooldownTimer = 5f;
    [SerializeField] public Vector2 playerGravity = new Vector2(0f, 1f);
    [SerializeField] public int maxJumps = 4;

    [Header("Points")]
    [SerializeField] public float comboSpeedThreshold = 50f;
    [SerializeField] public float comboPerSecond = 0.5f;
    [SerializeField] public float currentCombo = 1f;
    [SerializeField] public int currentScore = 0;
    [SerializeField] public int maxHealth = 20;
    [SerializeField] public int health = 20;

    [Header("Trackable Variables")]
    [SerializeField] private float inputSpeed = 0f;
    [SerializeField] public float jumpRequest = -5f;
    [SerializeField] public float boostRequest = -5f;
    [SerializeField] public float previousBoost = -10f;
    [SerializeField] private bool facingRight = true;
    [SerializeField] private bool isGrounded = true;
    [SerializeField] private int jumpsLeft = 2;
    [SerializeField] private float lastTimeCrossedCombo = 0f;

    [SerializeField] private float originalOrthoSize = 0f;

    void Awake()
    {
        controls = new PlayerControls();
        controls.InPlay.SetCallbacks(this);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (rigidbody == null)
        {
            rigidbody = GetComponent<Rigidbody2D>();
        }
        if (cam != null)
        {
            originalOrthoSize = cam.m_Lens.OrthographicSize;
        } else
        {
            Debug.Log("No camera - can't change camera size");
        }
        if (attackController == null)
        {
            attackController = GetComponentInChildren<PlayerAttackController>();
        }

        health = maxHealth;
    }

    private void OnEnable()
    {
        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Disable();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (health <= 0)
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.gravityScale = 0;
            healthMeter.value = 0;
            restartText.text = "Press Escape to Restart";
            return;
        } else
        {
            restartText.text = "";
        }
        AssertGrounded();
        UpdateVerticalDirection();
        UpdateHorizontalDirection();
        UpdateShaderDirection();
        UpdateCameraSize();
        UpdateCombo();
        CheckJump();
        CheckBoost();
        CheckFlip();
    }

    void AssertGrounded()
    {
        bool nowGrounded = Physics2D.OverlapCircle(groundChecker.position, groundCheckRadius, (int)groundMask);
        if (nowGrounded && !isGrounded)
        {
            jumpsLeft = maxJumps;
        } else if (!nowGrounded && jumpsLeft == maxJumps)
        {
            jumpsLeft--;
        }
        isGrounded = nowGrounded;
        animator.SetBool("Air", !isGrounded);
    }

    void UpdateHorizontalDirection()
    {
        int hDirection = Utility.Sign(inputSpeed);
        float weightedDirection = isGrounded ? hDirection : hDirection * aerialControl;
        Vector3 velocity = rigidbody.velocity;
        int velocitySign = Utility.Sign(velocity.x);
        if (hDirection != velocitySign)
            weightedDirection *= isGrounded ? breakMultiplier : breakMultiplier * aerialControl * aerialControl;
        else if (Mathf.Abs(rigidbody.velocity.x) < snapSpeedThreshold)
        {
            weightedDirection *= isGrounded ? snapMultiplier : snapMultiplier * aerialControl;
        }
        velocity.x += horizontalAcceleration * weightedDirection * Time.fixedDeltaTime;
        if (isGrounded && hDirection == 0 && velocitySign != 0)
        {
            float slowBy = horizontalAcceleration * flatDragAccelerationPercent * Time.fixedDeltaTime;
            if (Mathf.Abs(velocity.x) < slowBy)
                velocity.x = 0;
            else
                velocity.x -= velocitySign * slowBy;
        }
        rigidbody.velocity = velocity;
        animator.SetFloat("SpeedX", Mathf.Abs(velocity.x));
    }

    void UpdateVerticalDirection()
    {
        Vector3 velocity = rigidbody.velocity;
        velocity.y -= playerGravity.y * Time.fixedDeltaTime;
        rigidbody.velocity = velocity;
        animator.SetFloat("SpeedY", velocity.y);
    }

    void UpdateShaderDirection()
    {
        Vector2 direction = rigidbody.velocity;
        float mag = direction.magnitude;
        direction.Normalize();
        direction *= Mathf.Sqrt(2);
        float multiplier = mag / 10;
        if (multiplier > 1)
        {
            if (multiplier > 10)
                multiplier = 10;
            multiplier = 1 / multiplier;
            direction *= multiplier;
        }
        mat.SetVector("_Stretch", direction);
    }

    void UpdateCameraSize()
    {
        if (cam == null)
        {
            return;
        }
        if (originalOrthoSize == 0)
        {
            originalOrthoSize = cam.m_Lens.OrthographicSize;
            Debug.Log("Ortho size is 0, skipping camera scale");
            return;
        }
        float newSize = Mathf.Log(Mathf.Abs(rigidbody.velocity.x), orthoSizeSpeedBase) * originalOrthoSize * orthoSizeSpeedScale;
        newSize = Mathf.Lerp(cam.m_Lens.OrthographicSize, newSize, 0.3f);
        if (newSize < originalOrthoSize)
            newSize = originalOrthoSize;
        if (newSize > maxCamOrthoSize)
            newSize = maxCamOrthoSize;
        if (!float.IsNaN(newSize))
        {
            cam.m_Lens.OrthographicSize = newSize;
        }
    }

    void UpdateCombo()
    {
        if (Mathf.Abs(rigidbody.velocity.x) > comboSpeedThreshold)
        {
            if (Time.fixedTime - lastTimeCrossedCombo >= 1)
            {
                lastTimeCrossedCombo++;
                currentCombo += comboPerSecond;
                if (currentCombo >= 10)
                {
                    health += 1;
                    if (health > maxHealth)
                        health = maxHealth;
                }
            }
        } else
        {
            lastTimeCrossedCombo = Time.fixedTime;
            currentCombo -= comboPerSecond * Time.fixedDeltaTime;
            if (currentCombo < 1)
                currentCombo = 1;
        }
        comboText.text = "x" + ((int)(currentCombo*10)/10.0f);
        scoreText.text = "Score: " + currentScore;
        healthMeter.value = health * 1.0f / maxHealth;
    }

    void CheckJump()
    {
        if ((isGrounded || jumpsLeft > 0) && Time.fixedTime - jumpRequest < jumpRequestBuffer)
        {
            jumpsLeft--;
            Vector3 velocity = rigidbody.velocity;
            velocity.y = jumpSpeed;
            rigidbody.velocity = velocity;
            jumpRequest = Time.fixedTime - jumpRequestBuffer;
            animator.SetTrigger("Jump");
        }

        if (jumpsLeft >= 3)
        {
            jumpImage.enabled = true;
            jompImage.enabled = true;
            jampImage.enabled = true;
            jumpXImage.enabled = false;
            jompXImage.enabled = false;
            jampXImage.enabled = false;
        } else if (jumpsLeft == 2)
        {
            jumpImage.enabled = false;
            jompImage.enabled = true;
            jampImage.enabled = true;
            jumpXImage.enabled = true;
            jompXImage.enabled = false;
            jampXImage.enabled = false;
        } else if (jumpsLeft == 1)
        {
            jumpImage.enabled = false;
            jompImage.enabled = false;
            jampImage.enabled = true;
            jumpXImage.enabled = true;
            jompXImage.enabled = true;
            jampXImage.enabled = false;
        } else if (jumpsLeft == 0)
        {
            jumpImage.enabled = false;
            jompImage.enabled = false;
            jampImage.enabled = false;
            jumpXImage.enabled = true;
            jompXImage.enabled = true;
            jampXImage.enabled = true;
        }
    }

    void CheckBoost()
    {
        if (Time.fixedTime - previousBoost >= boostCooldownTimer && Time.fixedTime - boostRequest < boostRequestBuffer)
        {
            previousBoost = Time.fixedTime;

            Vector3 velocity = rigidbody.velocity;
            float angleDiff = Mathf.Atan2(velocity.y, velocity.x);
            Vector3 boostVelocity = new Vector3(Mathf.Cos(angleDiff) * boostSpeed, Mathf.Sin(angleDiff) * boostSpeed, 0);
            velocity += boostVelocity;
            rigidbody.velocity = velocity;
        }
        if (boostMeter != null)
        {
            boostMeter.value = (Time.fixedTime - previousBoost) / boostCooldownTimer;
        }
    }

    void CheckFlip()
    {
        float hSpeed = rigidbody.velocity.x;
        if (hSpeed < 0 && facingRight)
        {
            Flip();
        } else if (hSpeed > 0 && !facingRight)
        {
            Flip();
        }
    }

    void Flip(bool regenJumps = true)
    {
        if (regenJumps && Mathf.Abs(rigidbody.velocity.x) > horizontalAcceleration)
        {
            jumpsLeft = maxJumps - 1;
        }
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    public void OnDirectional(InputAction.CallbackContext context)
    {
        inputSpeed = context.ReadValue<float>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            jumpRequest = Time.fixedTime;
        }
    }

    public void OnBoost(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            boostRequest = Time.fixedTime;
        }
    }

    public void OnDodge(InputAction.CallbackContext context)
    {
        if (health <= 0)
            return;
        if (context.started && (isGrounded || jumpsLeft > 0))
        {
            if (!isGrounded)
                jumpsLeft--;
            Vector3 velocity = rigidbody.velocity;
            velocity.x *= -1;
            rigidbody.velocity = velocity;
            Flip(false);
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (health <= 0)
            return;
        if (context.performed)
        {
            attackController.gameObject.SetActive(true);
        }
    }

    public void OnDuck(InputAction.CallbackContext context)
    {
        if (health <= 0)
            return;
        if (context.performed)
        {
            Vector3 velocity = rigidbody.velocity;
            if (velocity.y > 0)
                velocity.y = -horizontalAcceleration;
            rigidbody.velocity = velocity;
        }
    }

    public void OnEscape(InputAction.CallbackContext context)
    {
        if (health <= 0)
        {
            SceneManager.LoadScene(0);
        } else
        {
            helpText.gameObject.SetActive(helpText.gameObject.activeInHierarchy);
        }
    }
}
