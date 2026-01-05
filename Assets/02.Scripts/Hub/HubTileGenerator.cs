using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 중간방 타일 생성기
    /// </summary>
    public class HubTileGenerator : MonoBehaviour
    {
        [Header("맵 설정")]
        [SerializeField] private int mapWidth = 30;
        [SerializeField] private int mapHeight = 30;
        [SerializeField] private float tileSize = 1f;

        [Header("타일 프리팹")]
        [SerializeField] private GameObject floorPrefab;
        [SerializeField] private GameObject wallPrefab;

        [Header("부모 오브젝트")]
        [SerializeField] private Transform tilesParent;

        // 타일 데이터 배열
        private TileType[,] tileMap;
        private GameObject[,] tileObjects;

        public int MapWidth => mapWidth;
        public int MapHeight => mapHeight;
        public float TileSize => tileSize;

        /// <summary>
        /// 중간방 맵 생성
        /// </summary>
        public void GenerateMap()
        {
            InitializeTileMap();
            CreateTileObjects();
            Debug.Log($"[HubTileGenerator] 맵 생성 완료 ({mapWidth}x{mapHeight})");
        }

        /// <summary>
        /// 타일맵 데이터 초기화
        /// </summary>
        private void InitializeTileMap()
        {
            tileMap = new TileType[mapWidth, mapHeight];
            tileObjects = new GameObject[mapWidth, mapHeight];

            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    // 가장자리 2칸은 벽
                    if (x < 2 || x >= mapWidth - 2 || y < 2 || y >= mapHeight - 2)
                    {
                        tileMap[x, y] = TileType.Wall;
                    }
                    else
                    {
                        tileMap[x, y] = TileType.Floor;
                    }
                }
            }

            // 포털 위치 주변 바닥 확보 (옵션)
            EnsurePortalArea();
        }

        /// <summary>
        /// 포털 위치 주변 바닥 확보
        /// </summary>
        private void EnsurePortalArea()
        {
            int centerX = mapWidth / 2;
            int centerY = mapHeight / 2;
            int portalOffset = 10;

            // 북, 동, 남, 서 포털 위치 주변 3x3 바닥
            Vector2Int[] portalPositions = new Vector2Int[]
            {
                new Vector2Int(centerX, centerY + portalOffset),      // 북
                new Vector2Int(centerX + portalOffset, centerY),      // 동
                new Vector2Int(centerX, centerY - portalOffset),      // 남
                new Vector2Int(centerX - portalOffset, centerY)       // 서
            };

            foreach (var pos in portalPositions)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int px = pos.x + dx;
                        int py = pos.y + dy;
                        if (IsValidPosition(px, py))
                        {
                            tileMap[px, py] = TileType.Floor;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 타일 오브젝트 생성
        /// </summary>
        private void CreateTileObjects()
        {
            // 부모 오브젝트 생성
            if (tilesParent == null)
            {
                GameObject parent = new GameObject("HubTiles");
                parent.transform.SetParent(transform);
                tilesParent = parent.transform;
            }

            // 기존 타일 제거
            ClearTiles();

            // 타일 생성
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    CreateTile(x, y);
                }
            }
        }

        /// <summary>
        /// 단일 타일 생성
        /// </summary>
        private void CreateTile(int x, int y)
        {
            TileType type = tileMap[x, y];
            GameObject prefab = GetPrefabForType(type);

            if (prefab == null)
            {
                // 프리팹 없으면 기본 큐브 생성 (테스트용)
                prefab = CreateDefaultTile(type);
            }

            Vector3 worldPos = GridToWorld(x, y);
            GameObject tile = Instantiate(prefab, worldPos, Quaternion.identity, tilesParent);
            tile.name = $"Tile_{x}_{y}_{type}";
            tileObjects[x, y] = tile;
        }

        /// <summary>
        /// 타일 타입에 맞는 프리팹 반환
        /// </summary>
        private GameObject GetPrefabForType(TileType type)
        {
            return type switch
            {
                TileType.Floor => floorPrefab,
                TileType.Wall => wallPrefab,
                _ => floorPrefab
            };
        }

        /// <summary>
        /// 기본 타일 생성 (프리팹 없을 때 테스트용)
        /// </summary>
        private GameObject CreateDefaultTile(TileType type)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.localScale = new Vector3(tileSize, 0.1f, tileSize);

            // 색상 설정
            Renderer renderer = tile.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = type switch
                {
                    TileType.Floor => new Color(0.4f, 0.3f, 0.2f),  // 갈색
                    TileType.Wall => new Color(0.3f, 0.3f, 0.3f),   // 회색
                    _ => Color.white
                };
                renderer.material = mat;
            }

            return tile;
        }

        /// <summary>
        /// 기존 타일 제거
        /// </summary>
        public void ClearTiles()
        {
            if (tilesParent == null) return;

            // 에디터에서는 DestroyImmediate, 런타임에서는 Destroy
            while (tilesParent.childCount > 0)
            {
                Transform child = tilesParent.GetChild(0);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            tileObjects = new GameObject[mapWidth, mapHeight];
        }

        /// <summary>
        /// 그리드 좌표 → 월드 좌표
        /// </summary>
        public Vector3 GridToWorld(int gridX, int gridY)
        {
            float worldX = gridX * tileSize + tileSize / 2f;
            float worldZ = gridY * tileSize + tileSize / 2f;
            return new Vector3(worldX, 0, worldZ);
        }

        /// <summary>
        /// 월드 좌표 → 그리드 좌표
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            int gridX = Mathf.FloorToInt(worldPos.x / tileSize);
            int gridY = Mathf.FloorToInt(worldPos.z / tileSize);
            return new Vector2Int(gridX, gridY);
        }

        /// <summary>
        /// 유효한 좌표인지 확인
        /// </summary>
        public bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
        }

        /// <summary>
        /// 해당 좌표가 이동 가능한지
        /// </summary>
        public bool IsWalkable(int x, int y)
        {
            if (!IsValidPosition(x, y)) return false;
            TileData data = TileData.GetTileData(tileMap[x, y]);
            return data.isWalkable;
        }

        /// <summary>
        /// 해당 좌표의 타일 타입
        /// </summary>
        public TileType GetTileType(int x, int y)
        {
            if (!IsValidPosition(x, y)) return TileType.Boundary;
            return tileMap[x, y];
        }

        /// <summary>
        /// 플레이어 스폰 위치 (맵 중앙)
        /// </summary>
        public Vector3 GetPlayerSpawnPosition()
        {
            int centerX = mapWidth / 2;
            int centerY = mapHeight / 2;
            return GridToWorld(centerX, centerY - 3);
        }

        /// <summary>
        /// 재단 위치 (맵 중앙)
        /// </summary>
        public Vector3 GetAltarPosition()
        {
            return GridToWorld(mapWidth / 2, mapHeight / 2);
        }

        /// <summary>
        /// 포털 위치들 반환
        /// </summary>
        public Vector3[] GetPortalPositions()
        {
            int centerX = mapWidth / 2;
            int centerY = mapHeight / 2;
            int offset = 10;

            return new Vector3[]
            {
                GridToWorld(centerX, centerY + offset),      // 북 (장)
                GridToWorld(centerX + offset, centerY),      // 동 (간)
                GridToWorld(centerX, centerY - offset),      // 남 (위)
                GridToWorld(centerX - offset, centerY)       // 서 (폐)
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 맵 범위
            Gizmos.color = Color.green;
            Vector3 center = new Vector3(mapWidth * tileSize / 2f, 0, mapHeight * tileSize / 2f);
            Vector3 size = new Vector3(mapWidth * tileSize, 0.2f, mapHeight * tileSize);
            Gizmos.DrawWireCube(center, size);

            // 포털 위치
            Gizmos.color = Color.cyan;
            foreach (var pos in GetPortalPositions())
            {
                Gizmos.DrawWireSphere(pos, 1.5f);
            }

            // 재단 위치
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetAltarPosition(), 2f);

            // 스폰 위치
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(GetPlayerSpawnPosition(), 1f);
        }
#endif
    }
}
