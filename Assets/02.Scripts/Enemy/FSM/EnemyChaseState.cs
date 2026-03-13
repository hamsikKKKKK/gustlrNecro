namespace Necrocis
{
    public class EnemyChaseState : IEnemyState
    {
        public static readonly EnemyChaseState Instance = new EnemyChaseState();

        public void Enter(EnemyController enemy)
        {
            enemy.SetMoveAnimation();
        }

        public void Update(EnemyController enemy, float deltaTime)
        {
            // 체력 0 → Dead
            if (enemy.IsDead)
            {
                enemy.ChangeState(EnemyDeadState.Instance);
                return;
            }

            // leash 이탈 → Return
            if (enemy.IsOutOfLeash())
            {
                enemy.ChangeState(EnemyReturnState.Instance);
                return;
            }

            // 감지범위 이탈 → Wander
            if (!enemy.IsPlayerInChaseRange())
            {
                enemy.ChangeState(EnemyWanderState.Instance);
                return;
            }

            // 공격 범위 진입 → Attack
            if (enemy.IsPlayerInAttackRange())
            {
                enemy.ChangeState(EnemyAttackState.Instance);
                return;
            }

            // 플레이어 추격
            enemy.SetChaseDestination();
            enemy.MoveTowardDestination(deltaTime);
        }

        public void Exit(EnemyController enemy) { }
    }
}
