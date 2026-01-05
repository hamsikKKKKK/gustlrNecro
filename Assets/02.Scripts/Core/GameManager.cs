using UnityEngine;
using UnityEngine.Events;

namespace Necrocis
{
    /// <summary>
    /// 게임 전체 상태 관리 (싱글톤)
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("게임 상태")]
        [SerializeField] private GameState currentState = GameState.InHub;
        [SerializeField] private BiomeType currentBiome = BiomeType.None;

        [Header("보스 부산물 (목) 수집 현황")]
        [SerializeField] private bool hasIntestineRelic = false;  // 장 보스 부산물
        [SerializeField] private bool hasLiverRelic = false;      // 간 보스 부산물
        [SerializeField] private bool hasStomachRelic = false;    // 위 보스 부산물
        [SerializeField] private bool hasLungRelic = false;       // 폐 보스 부산물

        [Header("바이옴 진입 횟수 (강화용)")]
        [SerializeField] private int intestineEntryCount = 0;
        [SerializeField] private int liverEntryCount = 0;
        [SerializeField] private int stomachEntryCount = 0;
        [SerializeField] private int lungEntryCount = 0;

        // 이벤트
        public UnityEvent<GameState> OnGameStateChanged;
        public UnityEvent<BiomeType> OnBiomeEntered;
        public UnityEvent OnAllRelicsCollected;

        // 프로퍼티
        public GameState CurrentState => currentState;
        public BiomeType CurrentBiome => currentBiome;
        public int CollectedRelicCount => (hasIntestineRelic ? 1 : 0) + (hasLiverRelic ? 1 : 0)
                                        + (hasStomachRelic ? 1 : 0) + (hasLungRelic ? 1 : 0);
        public bool HasAllRelics => CollectedRelicCount >= 4;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        public void SetGameState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            OnGameStateChanged?.Invoke(currentState);
            Debug.Log($"[GameManager] 상태 변경: {currentState}");
        }

        /// <summary>
        /// 바이옴 진입
        /// </summary>
        public void EnterBiome(BiomeType biome)
        {
            currentBiome = biome;
            IncrementEntryCount(biome);
            SetGameState(GameState.InBiome);
            OnBiomeEntered?.Invoke(biome);
            Debug.Log($"[GameManager] {biome} 바이옴 진입 (진입 횟수: {GetEntryCount(biome)})");
        }

        /// <summary>
        /// 중간방으로 귀환
        /// </summary>
        public void ReturnToHub()
        {
            currentBiome = BiomeType.None;
            SetGameState(GameState.InHub);
            Debug.Log("[GameManager] 중간방 귀환");
        }

        /// <summary>
        /// 보스 부산물 획득
        /// </summary>
        public void CollectRelic(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Intestine: hasIntestineRelic = true; break;
                case BiomeType.Liver: hasLiverRelic = true; break;
                case BiomeType.Stomach: hasStomachRelic = true; break;
                case BiomeType.Lung: hasLungRelic = true; break;
            }

            Debug.Log($"[GameManager] {biome} 부산물 획득! (총 {CollectedRelicCount}/4)");

            if (HasAllRelics)
            {
                OnAllRelicsCollected?.Invoke();
                Debug.Log("[GameManager] 모든 부산물 수집 완료! 재단 활성화 가능");
            }
        }

        /// <summary>
        /// 특정 바이옴 부산물 보유 여부
        /// </summary>
        public bool HasRelic(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Intestine => hasIntestineRelic,
                BiomeType.Liver => hasLiverRelic,
                BiomeType.Stomach => hasStomachRelic,
                BiomeType.Lung => hasLungRelic,
                _ => false
            };
        }

        /// <summary>
        /// 바이옴 진입 횟수 (몬스터 강화용)
        /// </summary>
        public int GetEntryCount(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Intestine => intestineEntryCount,
                BiomeType.Liver => liverEntryCount,
                BiomeType.Stomach => stomachEntryCount,
                BiomeType.Lung => lungEntryCount,
                _ => 0
            };
        }

        private void IncrementEntryCount(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Intestine: intestineEntryCount++; break;
                case BiomeType.Liver: liverEntryCount++; break;
                case BiomeType.Stomach: stomachEntryCount++; break;
                case BiomeType.Lung: lungEntryCount++; break;
            }
        }

        /// <summary>
        /// 최종 보스 진입 (재단에서 호출)
        /// </summary>
        public void EnterFinalBoss()
        {
            if (!HasAllRelics)
            {
                Debug.LogWarning("[GameManager] 부산물이 부족합니다!");
                return;
            }
            SetGameState(GameState.InFinalBoss);
            Debug.Log("[GameManager] 대뇌 맵 진입!");
        }
    }
}
