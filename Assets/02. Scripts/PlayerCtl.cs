using UnityEngine;
using UnityEngine.InputSystem; // 필수 네임스페이스
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

    private Rigidbody rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 movement; // Input System에서 받아온 값을 저장할 변수
    private Vector3 lastMovement;

    [Header("Health Settings")]
    public int maxHealth = 100;
    private int currentHealth;

    public static event Action<int, int> OnHealthChanged;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb.freezeRotation = true;
        lastMovement = Vector3.forward;
        currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    // New Input System 함수 추가 (Send Messages 방식)
    // Move 액션이 발생할 때마다 호출됨 (방향키 누르거나 뗄 때)
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
    void Update()
    {
        if (dashCooldownTimer > 0)
            dashCooldownTimer -= Time.deltaTime;

        if (movement.x < 0)
        {
            spriteRenderer.flipX = true;
        }
        else if (movement.x > 0)
        {
            spriteRenderer.flipX = false;
        }

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
        
        // 이동 중이면 입력 방향으로 대쉬
        if (movement != Vector2.zero)
        {
             dashDirection = new Vector3(movement.x, 0, movement.y).normalized;
        }
        // [수정됨] 가만히 있을 때는 바라보는 방향(스프라이트 반전 상태)으로 대쉬
        else
        {
            // flipX가 true면 왼쪽(Vector3.left), false면 오른쪽(Vector3.right)
            dashDirection = spriteRenderer.flipX ? Vector3.left : Vector3.right;
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

}