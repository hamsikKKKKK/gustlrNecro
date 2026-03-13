namespace Necrocis
{
    public class EnemyIdleState : IEnemyState
    {
        public static readonly EnemyIdleState Instance = new EnemyIdleState();

        public void Enter(EnemyController enemy)
        {
            enemy.SetIdleAnimation();
            enemy.ResetIdleTimer();
        }

        public void Update(EnemyController enemy, float deltaTime)
        {
            // 감지범위 진입 → Chase
            if (enemy.IsPlayerInChaseRange())
            {
                enemy.ChangeState(EnemyChaseState.Instance);
                return;
            }

            // 대기 타임아웃 → Wander
            if (enemy.IsIdleTimerExpired(deltaTime))
            {
                enemy.ChangeState(EnemyWanderState.Instance);
            }
        }

        public void Exit(EnemyController enemy) { }
    }
}
