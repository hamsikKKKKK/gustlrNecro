namespace Necrocis
{
    public class EnemyReturnState : IEnemyState
    {
        public static readonly EnemyReturnState Instance = new EnemyReturnState();

        public void Enter(EnemyController enemy)
        {
            enemy.SetReturnDestination();
            enemy.SetMoveAnimation();
        }

        public void Update(EnemyController enemy, float deltaTime)
        {
            // 앵커 도착 → Idle
            if (!enemy.MoveTowardDestination(deltaTime))
            {
                enemy.ChangeState(EnemyIdleState.Instance);
            }
        }

        public void Exit(EnemyController enemy) { }
    }
}
