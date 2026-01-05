using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 타일 종류
    /// </summary>
    public enum TileType
    {
        Floor = 0,      // 바닥
        Wall = 1,       // 벽
        Water = 2,      // 물
        Lava = 3,       // 용암
        Acid = 4,       // 위액
        Slime = 5,      // 점액
        Boundary = 6    // 경계 (맵 끝)
    }

    /// <summary>
    /// 타일 데이터
    /// </summary>
    [System.Serializable]
    public class TileData
    {
        public TileType tileType;
        public bool isWalkable;
        public float movementModifier;  // 이동속도 배율 (1.0 = 기본)
        public float damagePerSecond;   // 초당 데미지 (0 = 없음)

        public static TileData GetTileData(TileType type)
        {
            return type switch
            {
                TileType.Floor => new TileData
                {
                    tileType = TileType.Floor,
                    isWalkable = true,
                    movementModifier = 1f,
                    damagePerSecond = 0f
                },
                TileType.Wall => new TileData
                {
                    tileType = TileType.Wall,
                    isWalkable = false,
                    movementModifier = 0f,
                    damagePerSecond = 0f
                },
                TileType.Water => new TileData
                {
                    tileType = TileType.Water,
                    isWalkable = true,
                    movementModifier = 0.7f,
                    damagePerSecond = 0f
                },
                TileType.Lava => new TileData
                {
                    tileType = TileType.Lava,
                    isWalkable = true,
                    movementModifier = 0.5f,
                    damagePerSecond = 2f
                },
                TileType.Acid => new TileData
                {
                    tileType = TileType.Acid,
                    isWalkable = true,
                    movementModifier = 0.8f,
                    damagePerSecond = 1f
                },
                TileType.Slime => new TileData
                {
                    tileType = TileType.Slime,
                    isWalkable = true,
                    movementModifier = 0.8f,
                    damagePerSecond = 0f
                },
                TileType.Boundary => new TileData
                {
                    tileType = TileType.Boundary,
                    isWalkable = false,
                    movementModifier = 0f,
                    damagePerSecond = 0f
                },
                _ => new TileData
                {
                    tileType = TileType.Floor,
                    isWalkable = true,
                    movementModifier = 1f,
                    damagePerSecond = 0f
                }
            };
        }
    }
}
