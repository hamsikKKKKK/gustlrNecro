using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 바이옴 오브젝트의 이동 차단 상태 관리
    /// </summary>
    public class BiomeObjectState : MonoBehaviour
    {
        private BiomeManager owner;
        private ObjectId objectId;
        private bool blocksMovement;
        private bool suppressDestroy;

        public ObjectId ObjectId => objectId;
        public bool BlocksMovement => blocksMovement;

        public void Initialize(BiomeManager owner, ObjectId objectId, bool blocksMovement)
        {
            this.owner = owner;
            this.objectId = objectId;
            this.blocksMovement = blocksMovement;
            suppressDestroy = false;
        }

        public void SuppressDestroy()
        {
            suppressDestroy = true;
        }

        private void OnDestroy()
        {
            if (suppressDestroy) return;

            if (owner != null)
            {
                owner.NotifyObjectRemoved(objectId, blocksMovement);
            }
        }
    }
}
