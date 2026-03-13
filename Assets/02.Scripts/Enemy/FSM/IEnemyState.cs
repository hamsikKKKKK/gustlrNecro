namespace Necrocis
{
    public interface IEnemyState
    {
        void Enter(EnemyController enemy);
        void Update(EnemyController enemy, float deltaTime);
        void Exit(EnemyController enemy);
    }
}
