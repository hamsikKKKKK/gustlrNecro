using UnityEngine;

public class EmptyController : MonoBehaviour
{
    public float moveSpeed = 1f;
    public int damage = 10;
    public int health = 50;
    private Transform player;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void FixedUpdate()
    {
        if (player != null)
        {
            // Y축무시
            Vector3 direction = player.position - transform.position;
            direction.y = 0; // 높이 차이 무시함
            direction.Normalize();

            rb.MovePosition(rb.position + direction * moveSpeed * Time.fixedDeltaTime);
        }
    }

    void OnCollisionEnter(Collision collision) 
    {
        Debug.Log("충돌 감지됨: " + collision.gameObject.name);

        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("플레이어와 충돌");
            PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamage(damage);
                Debug.Log("플레이어에게" + damage + "데미지 적용");
            }
            else
            {
                Debug.Log("PlayerController를 찾을 수 없음!");
            }
        }
        else
        {
            Debug.Log("플레이어가 아닌 오브젝트: " + collision.gameObject.tag);
        }
    }

    public void Die()
    {
        PlayerController player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>();
        if (player != null)
            Destroy(gameObject);
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        Debug.Log("적을 공격함. 남은 체력: " + health);

        if (health <= 0)
        {
            Die();
        }
    }
}