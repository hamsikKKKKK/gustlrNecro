using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 바이옴 오브젝트 변경 상태 추적
    /// </summary>
    public class BiomeObjectState : MonoBehaviour
    {
        private BiomeManager owner;
        private Vector2Int chunkCoord;
        private ObjectId objectId;
        private bool blocksMovement;
        private bool suppressDestroy;
        private bool isDestroyed;
        private bool isCollected;

        public ObjectId ObjectId => objectId;
        public bool BlocksMovement => blocksMovement;

        public void Initialize(BiomeManager owner, Vector2Int chunkCoord, ObjectId objectId, bool blocksMovement)
        {
            this.owner = owner;
            this.chunkCoord = chunkCoord;
            this.objectId = objectId;
            this.blocksMovement = blocksMovement;
            suppressDestroy = false;
            isDestroyed = false;
            isCollected = false;
        }

        public void MarkDestroyed()
        {
            if (isDestroyed || isCollected) return;
            isDestroyed = true;
            if (owner != null)
            {
                owner.NotifyObjectStateChanged(chunkCoord, objectId, true, false);
            }
        }

        public void MarkCollected()
        {
            if (isDestroyed || isCollected) return;
            isCollected = true;
            if (owner != null)
            {
                owner.NotifyObjectStateChanged(chunkCoord, objectId, false, true);
            }
        }

        public void SuppressDestroy()
        {
            suppressDestroy = true;
        }

        private void OnDestroy()
        {
            if (suppressDestroy) return;

            if (!isDestroyed && !isCollected)
            {
                isDestroyed = true;
                if (owner != null)
                {
                    owner.NotifyObjectStateChanged(chunkCoord, objectId, true, false);
                }
            }
        }
    }
}
