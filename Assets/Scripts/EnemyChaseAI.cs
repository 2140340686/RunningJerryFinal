using UnityEngine;

public class EnemyChaseAI : MonoBehaviour
{
    private enum ChaseState
    {
        Idle,
        Chasing,
        BoostChasing,
        Attacking,
        AttackIdle
    }

    [Header("Chase Setup")]
    public float startGap = 15f;
    public float baseChaseSpeed = 20f;
    public float chaseBoostSpeed = 24f;
    public float lateralFollowSpeed = 8f;
    public float boostAmount = 4.5f;
    public float boostDuration = 1.6f;
    public float verticalOffset = -1.14f;
    public float accelerationGap = 5f;
    public float catchDistance = 0.15f;
    public float facingYawOffset = 180f;
    public float attackFrontGap = 0f;
    public float attackAnimationSpeed = 0.72f;

    private Transform player;
    private GameController controller;
    private Animator animator;
    private ChaseState state = ChaseState.Idle;
    private float currentChaseSpeed;
    private float boostTimer;
    private float attackTimer;
    private string currentAnimation = string.Empty;

    public float CurrentGap => player == null ? 0f : player.position.z - transform.position.z;

    public void SyncBaseSpeed(float playerBaseSpeed)
    {
        baseChaseSpeed = playerBaseSpeed;
        chaseBoostSpeed = Mathf.Max(baseChaseSpeed + boostAmount, baseChaseSpeed + 4f);
        currentChaseSpeed = Mathf.Max(currentChaseSpeed, baseChaseSpeed);
    }

    public void Initialize(
        Transform playerTarget,
        GameController gameController,
        Animator enemyAnimator,
        AnimationClip idleAnimation,
        AnimationClip runAnimation,
        AnimationClip attackAnimation)
    {
        player = playerTarget;
        controller = gameController;
        animator = enemyAnimator;

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 1f;
            animator.Rebind();
            animator.Update(0f);
        }

        currentChaseSpeed = baseChaseSpeed;
        PlayAnimation("Monster01_Idle", true);
    }

    public void ResetToStartPosition()
    {
        if (player == null)
        {
            return;
        }

        transform.position = new Vector3(player.position.x, player.position.y + verticalOffset, player.position.z - startGap);
        state = ChaseState.Idle;
        boostTimer = 0f;
        attackTimer = 0f;
        currentChaseSpeed = baseChaseSpeed;
        PlayAnimation("Monster01_Idle", true);
    }

    public void BeginChase()
    {
        if (player == null)
        {
            return;
        }

        state = ChaseState.Chasing;
        boostTimer = 0f;
        currentChaseSpeed = baseChaseSpeed;
        PlayAnimation("Monster01_Run_InPlace", true);
    }

    public void IncreaseBaseChaseSpeed(float amount)
    {
        baseChaseSpeed += amount;
        chaseBoostSpeed = Mathf.Max(baseChaseSpeed + boostAmount, baseChaseSpeed + 4f);
        currentChaseSpeed = Mathf.Max(currentChaseSpeed, baseChaseSpeed);
    }

    public void TriggerPressureBoost()
    {
        if (state == ChaseState.Attacking || state == ChaseState.AttackIdle)
        {
            return;
        }

        boostTimer = boostDuration;
        currentChaseSpeed = Mathf.Max(currentChaseSpeed, baseChaseSpeed + boostAmount);
        state = ChaseState.BoostChasing;
        PlayAnimation("Monster01_Run_InPlace", false);
    }

    public void CatchPlayer()
    {
        if (state == ChaseState.Attacking || state == ChaseState.AttackIdle)
        {
            return;
        }

        state = ChaseState.Attacking;
        currentChaseSpeed = 0f;
        boostTimer = 0f;
        attackTimer = 1.35f;

        if (player != null)
        {
            transform.position = new Vector3(player.position.x, player.position.y + verticalOffset, player.position.z + attackFrontGap);
        }

        if (animator != null)
        {
            animator.speed = attackAnimationSpeed;
        }

        FaceAttackDirection();
        PlayAnimation("Monster01_Attack02_InPlace", true);
    }

    public void CelebrateWinStop()
    {
        state = ChaseState.Idle;
        currentChaseSpeed = 0f;
        PlayAnimation("Monster01_Idle", true);
    }

    private void Update()
    {
        if (player == null || controller == null)
        {
            return;
        }

        switch (state)
        {
            case ChaseState.Idle:
                HoldBehindPlayer();
                break;
            case ChaseState.Chasing:
            case ChaseState.BoostChasing:
                ChasePlayer();
                break;
            case ChaseState.Attacking:
                HoldAttackPosition();
                break;
            case ChaseState.AttackIdle:
                HoldFrontIdlePosition();
                break;
        }
    }

    private void HoldBehindPlayer()
    {
        Vector3 targetPosition = new Vector3(player.position.x, player.position.y + verticalOffset, player.position.z - startGap);
        transform.position = Vector3.Lerp(transform.position, targetPosition, 6f * Time.deltaTime);
        FaceForward();
        PlayAnimation("Monster01_Idle", false);
    }

    private void ChasePlayer()
    {
        if (boostTimer > 0f)
        {
            boostTimer -= Time.deltaTime;
            if (boostTimer <= 0f)
            {
                state = ChaseState.Chasing;
            }
        }

        float targetSpeed = baseChaseSpeed;
        if (CurrentGap <= accelerationGap)
        {
            targetSpeed = chaseBoostSpeed;
        }

        if (boostTimer > 0f)
        {
            targetSpeed = Mathf.Max(targetSpeed, baseChaseSpeed + boostAmount);
        }

        currentChaseSpeed = Mathf.MoveTowards(currentChaseSpeed, targetSpeed, 10f * Time.deltaTime);

        Vector3 position = transform.position;
        position.z += currentChaseSpeed * Time.deltaTime;
        position.x = Mathf.MoveTowards(position.x, player.position.x, lateralFollowSpeed * Time.deltaTime);
        position.y = player.position.y + verticalOffset;
        transform.position = position;

        FaceForward();
        PlayAnimation("Monster01_Run_InPlace", false);

        if (CurrentGap <= catchDistance)
        {
            transform.position = new Vector3(player.position.x, player.position.y + verticalOffset, player.position.z + attackFrontGap);
            controller.ShowGameOver();
        }
    }

    private void HoldAttackPosition()
    {
        if (player == null)
        {
            return;
        }

        transform.position = new Vector3(player.position.x, player.position.y + verticalOffset, player.position.z + attackFrontGap);
        FaceAttackDirection();
        PlayAnimation("Monster01_Attack02_InPlace", false);

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            state = ChaseState.AttackIdle;
            if (animator != null)
            {
                animator.speed = 1f;
            }
            PlayAnimation("Monster01_Idle", true);
        }
    }

    private void HoldFrontIdlePosition()
    {
        if (player == null)
        {
            return;
        }

        transform.position = new Vector3(player.position.x, player.position.y + verticalOffset, player.position.z + attackFrontGap);
        FaceAttackDirection();
        PlayAnimation("Monster01_Idle", false);
    }

    private void FaceForward()
    {
        transform.rotation = Quaternion.Euler(0f, facingYawOffset, 0f);
    }

    private void FaceAttackDirection()
    {
        transform.rotation = Quaternion.Euler(0f, facingYawOffset + 180f, 0f);
    }

    private void PlayAnimation(string stateName, bool forceRestart)
    {
        if (animator == null)
        {
            return;
        }

        if (!forceRestart && currentAnimation == stateName)
        {
            return;
        }

        currentAnimation = stateName;
        animator.Play(stateName, 0, 0f);
    }
}
