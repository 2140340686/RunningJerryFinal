using UnityEngine;
using System.Collections.Generic;

public class PlayerScript : MonoBehaviour
{
    private Rigidbody rb;
    public float speed;
    public float laneSpeed;
    private int currentLane = 1;
    private bool jumping;
    private float jumpStart;
    public float jumpLength;
    public float jumpHeight;

    public float rayRadius;
    public LayerMask coinLayer;
    public LayerMask layer;
    public bool isDead;
    public Animator anim;

    [Header("Chase Tuning")]
    public float hitSlowMultiplier = 0.65f;
    public float hitSlowDuration = 4f;
    public float recoveryRate = 8f;
    public float minimumSpeed = 6f;

    [Header("Grounding")]
    public float groundHeightOffset = 0f;
    public float groundProbeHeight = 0.5f;
    public float groundProbeDistance = 2.2f;
    public float coyoteTime = 0.15f;

    private GameController gc;
    private Vector3 movementTarget;
    private float baseSpeed;
    private float currentSpeed;
    private float slowdownTimer;
    private bool runStarted;
    private float startZ;
    private float lastHitTime = -10f;
    private bool initialized;
    private float speedBoostTimer;
    private float activeSpeedBonus;
    private float magnetTimer;
    private float standingHeightOffset;
    private float lastGroundedTime = -1f;

    [Header("Power Ups")]
    public float magnetRadius = 10f;
    public float magnetPullSpeed = 110f;
    public float magnetCollectDistance = 4f;
    public float speedBoostAmount = 12f;

    public float CurrentSpeed => runStarted && !isDead ? currentSpeed : 0f;
    public float DistanceTravelled => Mathf.Max(0f, transform.position.z - startZ);
    public float BaseRunSpeed => baseSpeed > 0f ? baseSpeed : speed;

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void Start()
    {
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        rb = GetComponent<Rigidbody>();
        gc = FindFirstObjectByType<GameController>();
        baseSpeed = speed;
        currentSpeed = 0f;
        startZ = transform.position.z;
        movementTarget = new Vector3(transform.position.x, transform.position.y, 0f);
        standingHeightOffset = transform.position.y;
        magnetRadius = Mathf.Max(magnetRadius, 12f);
        magnetPullSpeed = Mathf.Max(magnetPullSpeed, 95f);
        magnetCollectDistance = Mathf.Max(magnetCollectDistance, 1.2f);
        if (anim == null || (anim.transform != transform && !anim.transform.IsChildOf(transform)))
        {
            anim = GetComponentInChildren<Animator>(true);
        }
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }

        initialized = true;
        CaptureStandingHeight();
    }

    private void Update()
    {
        if (isDead || (gc != null && (gc.IsGameOver || gc.IsPaused)))
        {
            return;
        }

        if (!runStarted || gc == null)
        {
            return;
        }

        HandleInput();
        UpdateJump();
        UpdateSpeed();
        UpdatePowerUps();
    }

    private void FixedUpdate()
    {
        if (rb == null || rb.isKinematic)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;

        if (runStarted && !isDead && gc != null && gc.IsPlaying)
        {
            velocity.z = jumping ? GetJumpForwardSpeed() : currentSpeed;
            float nextX = Mathf.MoveTowards(rb.position.x, movementTarget.x, laneSpeed * Time.fixedDeltaTime);
            float nextY = rb.position.y;

            if (!jumping && TryGetGroundHit(out RaycastHit hit))
            {
                nextY = hit.point.y + standingHeightOffset + groundHeightOffset;

                if (velocity.y < 0f)
                {
                    velocity.y = 0f;
                }
            }

            rb.MovePosition(new Vector3(nextX, nextY, rb.position.z));
        }
        else
        {
            velocity.z = 0f;
        }

        rb.linearVelocity = velocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead)
        {
            return;
        }

        if (collision.gameObject.CompareTag("Death"))
        {
            ApplyHitPenalty();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Coin"))
        {
            gc?.AddCoin();
            other.gameObject.SetActive(false);
        }
    }

    public void BeginRun()
    {
        InitializeIfNeeded();

        if (rb == null)
        {
            return;
        }

        rb.isKinematic = false;
        runStarted = true;
        isDead = false;
        currentSpeed = baseSpeed;
        startZ = transform.position.z;
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }
    }

    public void StopRun()
    {
        StopRunInternal(true);
    }

    public void TriggerDeath()
    {
        StopRunInternal(true);
    }

    public void FreezeForEnemyAttack()
    {
        StopRunInternal(false);
    }

    public void FreezeRun()
    {
        StopRunInternal(false);
    }

    private void StopRunInternal(bool playDieAnimation)
    {
        runStarted = false;
        isDead = true;
        jumping = false;
        currentSpeed = 0f;
        slowdownTimer = 0f;
        movementTarget = new Vector3(transform.position.x, transform.position.y, 0f);
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        if (playDieAnimation && anim != null)
        {
            anim.applyRootMotion = false;
            anim.SetTrigger("die");
        }
    }

    public void IncreaseBaseSpeed(float amount)
    {
        baseSpeed += amount;
        if (runStarted && slowdownTimer <= 0f)
        {
            currentSpeed = Mathf.Max(currentSpeed, baseSpeed);
        }
    }

    public void ApplyHitPenalty()
    {
        if (!runStarted || isDead)
        {
            return;
        }

        if (Time.time - lastHitTime < 0.35f)
        {
            return;
        }

        lastHitTime = Time.time;
        slowdownTimer = hitSlowDuration;
        currentSpeed = Mathf.Max(minimumSpeed, currentSpeed * hitSlowMultiplier);
        gc?.OnPlayerHitObstacle();
    }

    public void ActivateMagnet(float duration)
    {
        magnetTimer = Mathf.Max(magnetTimer, duration);
    }

    public void ActivateSpeedBoost(float duration)
    {
        speedBoostTimer = Mathf.Max(speedBoostTimer, duration);
        activeSpeedBonus = speedBoostAmount;
        currentSpeed = Mathf.Max(currentSpeed, baseSpeed + activeSpeedBonus);
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            ChangeLane(-5);
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            ChangeLane(5);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Jump();
        }
    }

    private void UpdateJump()
    {
        if (IsGrounded())
        {
            lastGroundedTime = Time.time;
        }

        if (jumping)
        {
            if (IsGrounded() && rb != null && rb.linearVelocity.y <= 0.05f)
            {
                jumping = false;
            }
        }
    }

    private void UpdateSpeed()
    {
        if (slowdownTimer > 0f)
        {
            slowdownTimer -= Time.deltaTime;
            if (slowdownTimer <= 0f)
            {
                slowdownTimer = 0f;
                currentSpeed = baseSpeed + activeSpeedBonus;
            }
        }
        else
        {
            currentSpeed = baseSpeed + activeSpeedBonus;
        }
    }

    private void UpdatePowerUps()
    {
        if (speedBoostTimer > 0f)
        {
            speedBoostTimer -= Time.deltaTime;
            if (speedBoostTimer <= 0f)
            {
                speedBoostTimer = 0f;
                activeSpeedBonus = 0f;
                currentSpeed = baseSpeed;
            }
        }

        if (magnetTimer > 0f)
        {
            magnetTimer -= Time.deltaTime;
            PullNearbyCoins();
            if (magnetTimer < 0f)
            {
                magnetTimer = 0f;
            }
        }
    }

    private void PullNearbyCoins()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, magnetRadius);
        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Coin"))
            {
                continue;
            }

            Transform coin = hit.transform;
            Vector3 target = transform.position + Vector3.up * 0.6f;
            coin.position = Vector3.MoveTowards(coin.position, target, magnetPullSpeed * Time.deltaTime);

            if (Vector3.Distance(coin.position, target) <= magnetCollectDistance)
            {
                gc?.AddCoin();
                coin.gameObject.SetActive(false);
            }
        }
    }

    private bool IsGrounded()
    {
        return TryGetGroundHit(out _);
    }

    private float GetJumpForwardSpeed()
    {
        if (rb == null)
        {
            return currentSpeed;
        }

        float gravity = Mathf.Abs(Physics.gravity.y);
        if (gravity <= 0.001f)
        {
            return currentSpeed;
        }

        float jumpVelocity = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, jumpHeight));
        float jumpDuration = (jumpVelocity * 2f) / gravity;
        if (jumpDuration <= 0.001f)
        {
            return currentSpeed;
        }

        float targetDistance = jumpLength > 0f ? jumpLength : currentSpeed * 0.8f;
        float rawJumpSpeed = targetDistance / jumpDuration;
        float minimumSmoothSpeed = currentSpeed * 0.82f;
        float jumpSpeed = Mathf.Max(minimumSmoothSpeed, rawJumpSpeed);
        return Mathf.Min(currentSpeed * 0.95f, jumpSpeed);
    }

    private bool TryGetGroundHit(out RaycastHit hit)
    {
        Vector3 origin = transform.position + Vector3.up * groundProbeHeight;
        return Physics.Raycast(origin, Vector3.down, out hit, groundProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private void CaptureStandingHeight()
    {
        if (TryGetGroundHit(out RaycastHit hit))
        {
            standingHeightOffset = transform.position.y - hit.point.y;
        }
    }

    private void ChangeLane(int direction)
    {
        int targetLane = currentLane + direction;
        if (targetLane < -4 || targetLane > 6)
        {
            return;
        }

        currentLane = targetLane;
        movementTarget = new Vector3(currentLane - 1, movementTarget.y, 0f);
    }

    private void Jump()
    {
        bool canJump = IsGrounded() || (Time.time - lastGroundedTime) <= coyoteTime;
        if (jumping || rb == null || !canJump)
        {
            return;
        }

        jumpStart = transform.position.z;
        jumping = true;
        float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
        Vector3 velocity = rb.linearVelocity;
        velocity.y = jumpVelocity;
        rb.linearVelocity = velocity;
    }
}
