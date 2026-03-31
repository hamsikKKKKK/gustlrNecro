using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 공격
    /// Q = 근거리 공격
    /// W = 원거리 공격
    /// 전직 전사일 경우 Q 공격 시 8방향 공격 스프라이트 애니메이션 재생
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        private enum AttackSpriteDirection8
        {
            Up,
            Down,
            Left,
            Right,
            UpLeft,
            UpRight,
            DownLeft,
            DownRight
        }

        [Header("근거리 공격 (Q)")]
        [SerializeField] private float meleeAttackDamage = 20f;
        [SerializeField] private Vector3 meleeAttackBoxSize = new Vector3(3, 3, 3);
        [SerializeField] private float meleeAttackOffset = 2f;

        [Header("원거리 공격 (W)")]
        [SerializeField] private Transform projectileSpawnPoint;

        [Header("공통 설정")]
        [SerializeField] private float attackCooldown = 0.3f;

        [Header("전직 전사 공격 스프라이트 (8방향 배열)")]
        [SerializeField] private Sprite[] warriorAttackUpSprites;
        [SerializeField] private Sprite[] warriorAttackDownSprites;
        [SerializeField] private Sprite[] warriorAttackLeftSprites;
        [SerializeField] private Sprite[] warriorAttackRightSprites;
        [SerializeField] private Sprite[] warriorAttackUpLeftSprites;
        [SerializeField] private Sprite[] warriorAttackUpRightSprites;
        [SerializeField] private Sprite[] warriorAttackDownLeftSprites;
        [SerializeField] private Sprite[] warriorAttackDownRightSprites;

        [Header("전직 전사 공격 애니메이션 설정")]
        [SerializeField] private float attackFrameRate = 12f;
        [SerializeField] private bool lockInputDuringAttackAnimation = false;

        private float lastAttackTime = float.NegativeInfinity;

        private SpriteRenderer spriteRenderer;
        private Coroutine attackAnimationCoroutine;
        private bool isPlayingAttackAnimation;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            HandleAttackInput();
        }

        private void HandleAttackInput()
        {
            if (lockInputDuringAttackAnimation && isPlayingAttackAnimation)
                return;

            var input = InputManager.Instance;
            if (input == null)
            {
                Debug.LogWarning("[PlayerAttack] InputManager.Instance가 null");
                return;
            }

            bool canAttack = Time.time >= lastAttackTime + attackCooldown;

            if (input.MeleeAttackAction.WasPressedThisFrame())
            {
                Debug.Log("[PlayerAttack] 근거리 입력 감지");
                MeleeAttack();
            }
            else if (input.RangedAttackAction.WasPressedThisFrame())
            {
                Debug.Log("[PlayerAttack] 원거리 입력 감지");

                if (!canAttack)
                {
                    Debug.Log("[PlayerAttack] 아직 쿨타임");
                    return;
                }

                lastAttackTime = Time.time;
                RangedAttack();
            }
        }

        private bool IsAdvancedWarrior()
        {
            return LevelUpManager.GetCurrentJob() == JobType.Warrior && LevelUpManager.IsAdvanced();
        }

        private Vector3 GetAttackDirection()
        {
            if (PlayerController.Instance == null)
                return Vector3.forward;

            switch (PlayerController.Instance.GetCurrentDirection())
            {
                case PlayerController.Direction.Up:
                    return Vector3.forward;
                case PlayerController.Direction.Down:
                    return Vector3.back;
                case PlayerController.Direction.Left:
                    return Vector3.left;
                case PlayerController.Direction.Right:
                    return Vector3.right;
                default:
                    return Vector3.forward;
            }
        }

        private void MeleeAttack()
        {
            if (IsAdvancedWarrior())
            {
                PlayAdvancedWarriorAttackAnimation8Dir();
            }

            Vector3 direction = GetAttackDirection();
            Vector3 boxCenter = transform.position + direction * meleeAttackOffset;
            Vector3 tallBoxSize = new Vector3(meleeAttackBoxSize.x, 20f, meleeAttackBoxSize.z);
            Quaternion rotation = Quaternion.LookRotation(direction);

            Collider[] hitColliders = Physics.OverlapBox(
                boxCenter,
                tallBoxSize / 2,
                rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            Debug.Log($"[Skill: Melee] 사용 | 방향={direction} | 히트수={hitColliders.Length}");

            foreach (var hitCollider in hitColliders)
            {
                EnemyController enemy = hitCollider.GetComponentInParent<EnemyController>();
                if (enemy != null && !enemy.IsDead)
                {
                    float damage = meleeAttackDamage;
                    if (PlayerStats.Instance != null)
                        damage = PlayerStats.Instance.GetAttack();

                    enemy.TakeDamage(damage);

                    if (IsAdvancedWarrior())
                        Debug.Log($"[Skill: Advanced Warrior Slash] 적중 | 대상={enemy.gameObject.name} | 데미지={damage}");
                    else
                        Debug.Log($"[Skill: Melee] 적중 | 대상={enemy.gameObject.name} | 데미지={damage}");
                }
            }
        }

        private void RangedAttack()
        {
            if (ObjectPooler.Instance == null)
            {
                Debug.LogWarning("[PlayerAttack] ObjectPooler.Instance가 null입니다.");
                return;
            }

            GameObject projectile = ObjectPooler.Instance.GetPooledObject();
            if (projectile == null)
            {
                Debug.LogWarning("[PlayerAttack] 풀에서 사용 가능한 투사체가 없습니다.");
                return;
            }

            Vector3 spawnPos = projectileSpawnPoint != null
                ? projectileSpawnPoint.position
                : transform.position;

            projectile.transform.position = spawnPos;
            projectile.SetActive(true);

            Projectile proj = projectile.GetComponent<Projectile>();
            if (proj != null)
            {
                float damage = 10f;
                if (PlayerStats.Instance != null)
                    damage = PlayerStats.Instance.GetAttack();

                Vector3 attackDirection = GetAttackDirection();
                Debug.Log($"[Skill: Spit] 발사 | 방향={attackDirection} | 데미지={damage}");

                proj.Launch(attackDirection, damage, gameObject);
            }
            else
            {
                Debug.LogWarning("[PlayerAttack] Projectile 컴포넌트가 없습니다.");
            }
        }

        private void PlayAdvancedWarriorAttackAnimation8Dir()
        {
            if (spriteRenderer == null)
                return;

            Sprite[] selectedSprites = GetAttackSpritesByDirection(GetAttackSpriteDirection8());
            if (selectedSprites == null || selectedSprites.Length == 0)
                return;

            if (attackAnimationCoroutine != null)
            {
                StopCoroutine(attackAnimationCoroutine);
            }

            attackAnimationCoroutine = StartCoroutine(PlayAttackSpriteSequence(selectedSprites));
        }

        private IEnumerator PlayAttackSpriteSequence(Sprite[] sprites)
        {
            if (sprites == null || sprites.Length == 0 || spriteRenderer == null)
                yield break;

            isPlayingAttackAnimation = true;

            float frameDuration = attackFrameRate > 0f ? 1f / attackFrameRate : 0.08f;

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    spriteRenderer.sprite = sprites[i];
                }

                yield return new WaitForSeconds(frameDuration);
            }

            isPlayingAttackAnimation = false;
            attackAnimationCoroutine = null;
        }

        private Sprite[] GetAttackSpritesByDirection(AttackSpriteDirection8 dir)
        {
            switch (dir)
            {
                case AttackSpriteDirection8.Up:
                    return warriorAttackUpSprites;
                case AttackSpriteDirection8.Down:
                    return warriorAttackDownSprites;
                case AttackSpriteDirection8.Left:
                    return warriorAttackLeftSprites;
                case AttackSpriteDirection8.Right:
                    return warriorAttackRightSprites;
                case AttackSpriteDirection8.UpLeft:
                    return warriorAttackUpLeftSprites;
                case AttackSpriteDirection8.UpRight:
                    return warriorAttackUpRightSprites;
                case AttackSpriteDirection8.DownLeft:
                    return warriorAttackDownLeftSprites;
                case AttackSpriteDirection8.DownRight:
                    return warriorAttackDownRightSprites;
                default:
                    return warriorAttackDownSprites;
            }
        }

        private AttackSpriteDirection8 GetAttackSpriteDirection8()
        {
            Vector2 inputVector = Vector2.zero;

            if (InputManager.Instance != null)
            {
                inputVector = InputManager.Instance.MoveAction.ReadValue<Vector2>();
            }

            if (inputVector.sqrMagnitude > 0.01f)
            {
                float x = inputVector.x;
                float y = inputVector.y;

                if (y > 0.1f && x > 0.1f) return AttackSpriteDirection8.UpRight;
                if (y > 0.1f && x < -0.1f) return AttackSpriteDirection8.UpLeft;
                if (y < -0.1f && x > 0.1f) return AttackSpriteDirection8.DownRight;
                if (y < -0.1f && x < -0.1f) return AttackSpriteDirection8.DownLeft;
                if (y > 0.1f) return AttackSpriteDirection8.Up;
                if (y < -0.1f) return AttackSpriteDirection8.Down;
                if (x > 0.1f) return AttackSpriteDirection8.Right;
                if (x < -0.1f) return AttackSpriteDirection8.Left;
            }

            if (PlayerController.Instance != null)
            {
                switch (PlayerController.Instance.GetCurrentDirection())
                {
                    case PlayerController.Direction.Up:
                        return AttackSpriteDirection8.Up;
                    case PlayerController.Direction.Down:
                        return AttackSpriteDirection8.Down;
                    case PlayerController.Direction.Left:
                        return AttackSpriteDirection8.Left;
                    case PlayerController.Direction.Right:
                        return AttackSpriteDirection8.Right;
                }
            }

            return AttackSpriteDirection8.Down;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 direction = GetAttackDirection();
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.TRS(
                transform.position + direction * meleeAttackOffset,
                Quaternion.LookRotation(direction),
                Vector3.one);

            Gizmos.DrawWireCube(Vector3.zero, new Vector3(meleeAttackBoxSize.x, 20f, meleeAttackBoxSize.z));
        }
    }
}