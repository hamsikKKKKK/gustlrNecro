using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 외형 관리
    /// 1~9레벨: 기본 스프라이트
    /// 10레벨 직업 선택 후: 선택한 직업의 전직 스프라이트
    /// </summary>
    public class PlayerAppearance : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("기본 스프라이트 (10레벨 이전)")]
        [SerializeField] private Sprite defaultSprite;

        [Header("전직 후 스프라이트")]
        [SerializeField] private Sprite warriorAdvancedSprite;
        [SerializeField] private Sprite mageAdvancedSprite;
        [SerializeField] private Sprite archerAdvancedSprite;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            LevelUpManager.OnClassChanged += HandleClassChanged;
        }

        private void OnDisable()
        {
            LevelUpManager.OnClassChanged -= HandleClassChanged;
        }

        private void Start()
        {
            RefreshAppearance();
        }

        private void HandleClassChanged(JobType job, bool isAdvanced)
        {
            UpdateAppearance(job, isAdvanced);
        }

        public void RefreshAppearance()
        {
            UpdateAppearance(LevelUpManager.GetCurrentJob(), LevelUpManager.IsAdvanced());
        }

        private void UpdateAppearance(JobType job, bool isAdvanced)
        {
            if (spriteRenderer == null)
            {
                Debug.LogWarning("[PlayerAppearance] SpriteRenderer가 없습니다.");
                return;
            }

            // 10레벨 이전 또는 직업 미선택 상태
            if (job == JobType.None || !isAdvanced)
            {
                if (defaultSprite != null)
                {
                    spriteRenderer.sprite = defaultSprite;
                    Debug.Log("[PlayerAppearance] 기본 스프라이트 적용");
                }
                return;
            }

            Sprite targetSprite = null;

            switch (job)
            {
                case JobType.Warrior:
                    targetSprite = warriorAdvancedSprite;
                    break;

                case JobType.Mage:
                    targetSprite = mageAdvancedSprite;
                    break;

                case JobType.Archer:
                    targetSprite = archerAdvancedSprite;
                    break;
            }

            if (targetSprite != null)
            {
                spriteRenderer.sprite = targetSprite;
                Debug.Log($"[PlayerAppearance] 전직 외형 변경 | 직업={job}");
            }
            else
            {
                Debug.LogWarning($"[PlayerAppearance] {job} 전직 스프라이트가 비어 있습니다.");
            }
        }
    }
}