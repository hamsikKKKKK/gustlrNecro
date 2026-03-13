using UnityEngine;

namespace Necrocis
{
    public class EnemyAttackState : IEnemyState
    {
        public static readonly EnemyAttackState Instance = new EnemyAttackState();

        public void Enter(EnemyController enemy)
        {
            enemy.SetIdleAnimation();
        }

        public void Update(EnemyController enemy, float deltaTime)
        {
            // 체력 0 → Dead
            if (enemy.IsDead)
            {
                enemy.ChangeState(EnemyDeadState.Instance);
                return;
            }

            // 범위 이탈 → Chase
            if (!enemy.IsPlayerInAttackRange())
            {
                enemy.ChangeState(EnemyChaseState.Instance);
                return;
            }

            // 공격 수행
            enemy.TryPerformAttack(deltaTime);
        }

        public void Exit(EnemyController enemy) { }
    }
}
