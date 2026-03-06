using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// Y 위치에 따른 스프라이트 정렬 (아래에 있을수록 앞에 보임)
    /// </summary>
    public class SpriteYSort : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private int baseSortingOrder = 1000;  // 타일맵보다 확실히 앞에
        [SerializeField] private float sortingMultiplier = 10f;
        [SerializeField] private bool useMinSortingClamp = true;
        [SerializeField] private int minSortingOrder = -900;
        [SerializeField] private bool updateOnlyWhenDirty = true;
        [SerializeField] private float positionEpsilon = 0.001f;
        [SerializeField] private UpdateMode updateMode = UpdateMode.Once;

        private SpriteRenderer spriteRenderer;
        private float lastZ = float.NaN;
        private bool hasUpdated;

        public enum UpdateMode
        {
            Continuous,
            Once
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Start()
        {
            // 시작 시 즉시 정렬 적용
            UpdateSorting();
        }

        private void LateUpdate()
        {
            UpdateSorting();
        }

        private void UpdateSorting()
        {
            if (spriteRenderer == null) return;

            // Z 위치 (또는 Y)가 작을수록 (아래/앞) sortingOrder가 높음
            // 2.5D에서 Z가 작을수록 카메라에 가까움
            if (updateMode == UpdateMode.Once && hasUpdated)
            {
                enabled = false;
                return;
            }

            if (updateOnlyWhenDirty)
            {
                float z = transform.position.z;
                if (!float.IsNaN(lastZ) && Mathf.Abs(z - lastZ) <= positionEpsilon)
                {
                    return;
                }
                lastZ = z;
            }

            float sortValue = -transform.position.z * sortingMultiplier;
            int order = baseSortingOrder + Mathf.RoundToInt(sortValue);
            if (useMinSortingClamp)
            {
                order = Mathf.Max(minSortingOrder, order);
            }
            spriteRenderer.sortingOrder = order;
            hasUpdated = true;

            if (updateMode == UpdateMode.Once)
            {
                enabled = false;
            }
        }

        public void SetUpdateMode(UpdateMode mode)
        {
            updateMode = mode;
            enabled = true;
            hasUpdated = false;
        }
    }
}
