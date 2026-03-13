namespace Necrocis
{
    public class EnemyDeadState : IEnemyState
    {
        public static readonly EnemyDeadState Instance = new EnemyDeadState();

        public void Enter(EnemyController enemy)
        {
            enemy.SetIdleAnimation();
            enemy.DisableCollider();
        }

        public void Update(EnemyController enemy, float deltaTime)
        {
            // 사망 후 풀로 반환
            enemy.ReleaseToPool();
        }

        public void Exit(EnemyController enemy) { }
    }
}
