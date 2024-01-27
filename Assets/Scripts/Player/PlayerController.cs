using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Cinemachine;
using TMPro;

[RequireComponent(typeof(PlayerMotionController))]
public class PlayerController : MonoBehaviour, PlayerControls.IInPlayActions
{
    PlayerControls controls;

    [Header("References")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected Rigidbody2D _rb;
    public Rigidbody2D RB { get { if (_rb == null) _rb = GetComponent<Rigidbody2D>(); return _rb; } }
    [SerializeField] protected PlayerMotionController _motionController;
    public PlayerMotionController MotionController { get { if (_motionController == null) _motionController = GetComponent<PlayerMotionController>(); return _motionController; } }
    [SerializeField] protected PlayerAttackController attackController;
    [SerializeField] protected Slider boostMeter;
    [SerializeField] protected Slider healthMeter;
    [SerializeField] protected Image jumpImage;
    [SerializeField] protected Image jompImage;
    [SerializeField] protected Image jampImage;
    [SerializeField] protected Image jumpXImage;
    [SerializeField] protected Image jompXImage;
    [SerializeField] protected Image jampXImage;
    [SerializeField] protected TMPro.TextMeshProUGUI comboText;
    [SerializeField] protected TMPro.TextMeshProUGUI scoreText;
    [SerializeField] protected TMPro.TextMeshProUGUI restartText;
    [SerializeField] protected TMPro.TextMeshProUGUI helpText;
    [SerializeField] protected GameObject uiFadePanel;
    [SerializeField] protected TextMeshProUGUI pauseText;
    [SerializeField] protected Material mat;
    [SerializeField] protected CameraSpeedZoomer cameraController;

    [Header("Tweakable Parameters")]
    [SerializeField] public float jumpRequestBuffer = 0.5f;
    [SerializeField] public float boostRequestBuffer = 0.2f;

    [Header("Points")]
    [SerializeField] public float comboSpeedThreshold = 50f;
    [SerializeField] public float comboPerSecond = 0.5f;
    [SerializeField] public float currentCombo = 1f;
    [SerializeField] public int currentScore = 0;
    [SerializeField] public int maxHealth = 20;
    [SerializeField] public int health = 20;

    [Header("Trackable Variables")]
    [SerializeField] public float jumpRequest = -5f;
    [SerializeField] public float boostRequest = -5f;
    [SerializeField] private bool facingRight = true;
    [SerializeField] private float lastTimeCrossedCombo = 0f;

    void Awake()
    {
        /*controls = new PlayerControls();
        controls.InPlay.SetCallbacks(this);*/

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (_rb == null)
        {
            _rb = GetComponent<Rigidbody2D>();
        }
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraSpeedZoomer>();
        }
        if (attackController == null)
        {
            attackController = GetComponentInChildren<PlayerAttackController>();
        }

        MotionController.Init(this);

        health = maxHealth;
    }

    private void Start()
    {
        GameManager.Instance.OnPause += OnPause;
        PauseGame(true);
    }

    protected void OnPause(GameManager gm, bool paused)
    {
        MotionController.OnPause(gm, paused);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (GameManager.Instance.Paused)
        {
            jumpRequest += Time.fixedDeltaTime;
            boostRequest += Time.fixedDeltaTime;
            MotionController.UpdateAsPaused();
            return;
        }
        if (health <= 0)
        {
            healthMeter.value = 0;
            restartText.text = "Press Escape to Restart";
            pauseText.text = "You've ran your last run";
            pauseText.color = Color.red;
            PauseGame(true);
            return;
        } else
        {
            restartText.text = "";
        }

        CheckJump();
        CheckBoost();
        CheckFlip();
        MotionController.UpdateMotion();
        UpdateShaderDirection();
        UpdateCameraSize();
        UpdateAnimator();
        UpdateCombo();
    }

    void UpdateShaderDirection()
    {
        Vector2 direction = RB.velocity;
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
        cameraController?.UpdateVelocity(RB.velocity);
    }

    public void UpdateAnimator()
    {
        float speedMultiplier = cameraController.SpeedMultiplier;
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.IsName("Player_Run") || currentState.IsName("Player_Super_Run"))
        {
            if (speedMultiplier < 0.5f)
                speedMultiplier = 0.5f;
            else if (speedMultiplier > 1f)
                speedMultiplier *= speedMultiplier;
            animator.speed = speedMultiplier;
        }
        else
        {
            animator.speed = 1f;
        }

        animator.SetFloat("SpeedX", Mathf.Abs(RB.velocity.x));
        animator.SetFloat("SpeedY", RB.velocity.y);
        animator.SetBool("Breaking", MotionController.Breaking);
        animator.SetBool("Air", !MotionController.Grounded);
    }

    void UpdateCombo()
    {
        if (Mathf.Abs(RB.velocity.x) > comboSpeedThreshold)
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
        if (MotionController.CanJump() && Time.fixedTime - jumpRequest < jumpRequestBuffer)
        {
            MotionController.Jump();
            jumpRequest = Time.fixedTime - jumpRequestBuffer;
            animator.SetTrigger("Jump");
        }

        UpdateJumpDisplay();
    }

    public void UpdateJumpDisplay()
    {
        switch (MotionController.JumpsLeft)
        {
            case 4:
            case 3:
                jumpImage.enabled = true;
                jompImage.enabled = true;
                jampImage.enabled = true;
                jumpXImage.enabled = false;
                jompXImage.enabled = false;
                jampXImage.enabled = false;
                break;
            case 2:
                jumpImage.enabled = false;
                jompImage.enabled = true;
                jampImage.enabled = true;
                jumpXImage.enabled = true;
                jompXImage.enabled = false;
                jampXImage.enabled = false;
                break;
            case 1:
                jumpImage.enabled = false;
                jompImage.enabled = false;
                jampImage.enabled = true;
                jumpXImage.enabled = true;
                jompXImage.enabled = true;
                jampXImage.enabled = false;
                break;
            case 0:
                jumpImage.enabled = false;
                jompImage.enabled = false;
                jampImage.enabled = false;
                jumpXImage.enabled = true;
                jompXImage.enabled = true;
                jampXImage.enabled = true;
                break;
        }
    }

    void CheckBoost()
    {
        if (MotionController.CanBoost() && Time.fixedTime - boostRequest < boostRequestBuffer)
        {
            MotionController.Boost();
        }
        if (boostMeter != null)
        {
            boostMeter.value = MotionController.BoostPercentReady();
        }
    }

    void CheckFlip()
    {
        int hSpeed = Utility.Sign(RB.velocity.x);
        if (hSpeed == 1 && !facingRight)
        {
            Flip();
        } else if (hSpeed == -1 && facingRight)
        {
            Flip();
        }
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    public void OnDirectional(InputAction.CallbackContext context)
    {
        MotionController.InputSpeed = context.ReadValue<float>();
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
        if (context.started || context.performed)
        {
            boostRequest = Time.fixedTime;
        }
    }

    public void OnDodge(InputAction.CallbackContext context)
    {
        if (health <= 0)
            return;

        if (context.started && MotionController.CanReverseMotion())
        {
            MotionController.ReverseMotion();
            Flip();
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
        if ((context.started || context.performed) && MotionController.CanPlummet())
        {
            MotionController.Plummet();
        } else if (context.canceled)
        {
            MotionController.StopPlummet();
        }
    }

    public void OnEscape(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (health <= 0)
            {
                SceneManager.LoadScene(0);
            }
            else
            {
                TogglePauseGame();
            }
        }
    }

    public void TogglePauseGame()
    {
        GameManager.Instance.Paused = !GameManager.Instance.Paused;
        helpText.gameObject.SetActive(!helpText.gameObject.activeInHierarchy);
        uiFadePanel.gameObject.SetActive(!uiFadePanel.gameObject.activeInHierarchy);
        pauseText.gameObject.SetActive(!pauseText.gameObject.activeInHierarchy);
    }

    public void PauseGame(bool paused)
    {
        GameManager.Instance.Paused = paused;
        helpText.gameObject.SetActive(!helpText.gameObject.activeInHierarchy);
        uiFadePanel.gameObject.SetActive(!uiFadePanel.gameObject.activeInHierarchy);
        pauseText.gameObject.SetActive(!pauseText.gameObject.activeInHierarchy);
    }
}
