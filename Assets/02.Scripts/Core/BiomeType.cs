using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 바이옴 타입 열거형
    /// </summary>
    public enum BiomeType
    {
        None = 0,
        Intestine = 1,  // 장 - 숲, 습지 (120x120)
        Liver = 2,      // 간 - 늪, 강 (150x150)
        Stomach = 3,    // 위 - 화산 (90x90)
        Lung = 4        // 폐 - 초원, 안개 (135x135)
    }

    /// <summary>
    /// 게임 진행 상태
    /// </summary>
    public enum GameState
    {
        InHub,          // 중간방에 있음
        InBiome,        // 바이옴 탐험 중
        InBossRoom,     // 보스룸에 있음
        InFinalBoss     // 대뇌 맵 (최종 보스)
    }

    /// <summary>
    /// 바이옴 데이터 (크기, 디버프 등)
    /// </summary>
    [System.Serializable]
    public class BiomeData
    {
        public BiomeType biomeType;
        public string displayName;
        public Vector2Int mapSize;
        public string vulnerableClass;      // 취약 클래스
        public string debuffDescription;    // 디버프 설명
        public float debuffValue;           // 디버프 수치

        public static BiomeData GetBiomeData(BiomeType type)
        {
            switch (type)
            {
                case BiomeType.Intestine:
                    return new BiomeData
                    {
                        biomeType = BiomeType.Intestine,
                        displayName = "장",
                        mapSize = new Vector2Int(120, 120),
                        vulnerableClass = "없음",
                        debuffDescription = "없음",
                        debuffValue = 0f
                    };

                case BiomeType.Liver:
                    return new BiomeData
                    {
                        biomeType = BiomeType.Liver,
                        displayName = "간",
                        mapSize = new Vector2Int(150, 150),
                        vulnerableClass = "마법사",
                        debuffDescription = "방어력 10% 감소",
                        debuffValue = 0.1f
                    };

                case BiomeType.Stomach:
                    return new BiomeData
                    {
                        biomeType = BiomeType.Stomach,
                        displayName = "위",
                        mapSize = new Vector2Int(90, 90),
                        vulnerableClass = "궁수",
                        debuffDescription = "방어력 5% 감소",
                        debuffValue = 0.05f
                    };

                case BiomeType.Lung:
                    return new BiomeData
                    {
                        biomeType = BiomeType.Lung,
                        displayName = "폐",
                        mapSize = new Vector2Int(135, 135),
                        vulnerableClass = "전사",
                        debuffDescription = "피격시 넉백",
                        debuffValue = 1f
                    };

                default:
                    return null;
            }
        }
    }
}
