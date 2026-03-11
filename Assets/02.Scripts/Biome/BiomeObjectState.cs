using UnityEngine;
using System.Collections.Generic;

namespace Necrocis
{
    /// <summary>
    /// 바이옴 오브젝트의 이동 차단 상태 관리
    /// </summary>
    public class BiomeObjectState : MonoBehaviour
    {
        private BiomeManager owner;
        private ObjectId objectId;
        private ObjectPoolKey poolKey;
        private bool blocksMovement;
        private bool suppressDestroy;
        private readonly List<Vector2Int> occupiedCells = new List<Vector2Int>();

        public ObjectId ObjectId => objectId;
        public ObjectPoolKey PoolKey => poolKey;
        public bool BlocksMovement => blocksMovement;
        public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;

        public void Initialize(BiomeManager owner, ObjectId objectId, ObjectPoolKey poolKey, bool blocksMovement)
        {
            this.owner = owner;
            this.objectId = objectId;
            this.poolKey = poolKey;
            this.blocksMovement = blocksMovement;
            suppressDestroy = false;
        }

        public void SuppressDestroy()
        {
            suppressDestroy = true;
        }

        public void SetOccupiedCells(List<Vector2Int> cells)
        {
            occupiedCells.Clear();
            if (cells == null)
            {
                return;
            }

            occupiedCells.AddRange(cells);
        }

        private void OnDestroy()
        {
            if (suppressDestroy) return;

            if (owner != null)
            {
                owner.NotifyObjectRemoved(objectId, blocksMovement, occupiedCells);
            }
        }
    }
}
