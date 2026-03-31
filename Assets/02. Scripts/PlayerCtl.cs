using UnityEngine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(Rigidbody), typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    // 상태 변수들
    private bool isDashing = false;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private Vector3 dashDirection;
    private Vector3 lastFacingDirection = Vector3.right; // 마지막으로 바라본 방향

    private Rigidbody rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 movement;

    [Header("Health Settings")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Attack Settings")]
    public float attackRange = 2f;
    public int attackDamage = 20;
    public float attackCooldown = 0.5f;
    private float attackTimer = 0f;

    public static event Action<int, int> OnHealthChanged;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb.freezeRotation = true;
        currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // New Input System 함수 추가 (Send Messages 방식)
    // Move 액션이 발생할 때마다 호출
    void OnMove(InputValue value)
    {
        movement = value.Get<Vector2>();
    }

    // 대쉬
    void OnDash(InputValue value)
    {
        // 버튼이 눌린 순간+ 대쉬 중이 아님 + 쿨타임 끝남
        if (value.isPressed && !isDashing && dashCooldownTimer <= 0)
        {
            StartDash();
        }
    }

    // 공격 (평타 attack - Q키)
    void OnAttack(InputValue value)
    {
        Debug.Log("Attack 버튼 눌림!"); // 이 로그가 나오는지 확인
        if (value.isPressed && attackTimer <= 0)
        {
            Attack();
            attackTimer = attackCooldown;
        }
    }

    void Update()
    {
        // 쿨다운 타이머 업데이트
        if (dashCooldownTimer > 0)
            dashCooldownTimer -= Time.deltaTime;

        if (attackTimer > 0)
            attackTimer -= Time.deltaTime;

        // 움직일 때만 바라보는 방향 업데이트
        if (movement != Vector2.zero)
        {
            lastFacingDirection = new Vector3(movement.x, 0, movement.y).normalized;

            // 스프라이트 방향 전환 (좌우만)
            if (movement.x < 0)
            {
                spriteRenderer.flipX = true;
            }
            else if (movement.x > 0)
            {
                spriteRenderer.flipX = false;
            }
        }

        // 대시 처리
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
        }
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector3(
                dashDirection.x * dashSpeed,
                rb.linearVelocity.y,
                dashDirection.z * dashSpeed
            );
        }
        else
        {
            // OnMove에서 갱신된 movement 값으로 이동
            Vector3 move = new Vector3(movement.x, 0, movement.y);
            rb.MovePosition(rb.position + move * moveSpeed * Time.fixedDeltaTime);
        }
    }

    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        // 이동 중이면 입력 방향으로, 아니면 마지막 방향으로 대쉬
        dashDirection = (movement != Vector2.zero)
            ? new Vector3(movement.x, 0, movement.y).normalized
            : lastFacingDirection;
    }

    void Attack()
    {
        Debug.Log("Attack 실행됨!"); // 이 로그가 나오는지 확인
        Vector3 attackDirection = lastFacingDirection;

        Collider[] enemies = Physics.OverlapSphere(
            transform.position + attackDirection * attackRange / 2,
            attackRange / 2
        );

        Debug.Log($"검색된 적 수: {enemies.Length}"); // 몇 개 찾았는지 확인

        foreach (Collider enemy in enemies)
        {
            Debug.Log($"충돌체 태그: {enemy.tag}"); // 태그 확인
            if (enemy.CompareTag("Enemy"))
            {
                EmptyController enemyController = enemy.GetComponent<EmptyController>();
                if (enemyController != null)
                {
                    enemyController.TakeDamage(attackDamage);
                    Debug.Log($"공격 성공! {attackDamage} 데미지!");
                }
                else
                {
                    Debug.Log("EmptyController가 없음!");
                }
            }
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("플레이어 사망");
        Destroy(gameObject);
        Time.timeScale = 0f;
    }

    // 기즈모로 공격 범위 시각화 (Scene 뷰에서 확인 가능)
    void OnDrawGizmosSelected()
    {
        Vector3 attackDirection = Application.isPlaying ? lastFacingDirection : Vector3.right;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + attackDirection * attackRange / 2, attackRange / 2);
    }
}