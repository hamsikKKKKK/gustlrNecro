using System.Collections.Generic;
using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 범용 오브젝트 풀러 (투사체 등).
    /// </summary>
    public class ObjectPooler : MonoBehaviour
    {
        public static ObjectPooler Instance;

        [SerializeField] private GameObject objectToPool;
        [SerializeField] private int amountToPool = 10;

        private List<GameObject> pooledObjects;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            pooledObjects = new List<GameObject>(amountToPool);

            for (int i = 0; i < amountToPool; i++)
            {
                GameObject obj = Instantiate(objectToPool);
                obj.SetActive(false);
                pooledObjects.Add(obj);
            }
        }

        public GameObject GetPooledObject()
        {
            for (int i = 0; i < pooledObjects.Count; i++)
            {
                if (!pooledObjects[i].activeInHierarchy)
                {
                    return pooledObjects[i];
                }
            }

            return null;
        }
    }
}