namespace Necrocis
{
    public class EnemyDeadState : IEnemyState
    {
        public static readonly EnemyDeadState Instance = new EnemyDeadState();

        public void Enter(EnemyController enemy)
        {
            enemy.SetIdleAnimation();
            enemy.DisableCollider();
            enemy.GrantExp();
        }

        public void Update(EnemyController enemy, float deltaTime)
        {
            enemy.ReleaseToPool();
        }

        public void Exit(EnemyController enemy) { }
    }
}
