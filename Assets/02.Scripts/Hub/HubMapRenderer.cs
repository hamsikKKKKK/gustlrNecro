using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 중간방 통이미지 렌더러 + 충돌 영역 관리
    /// </summary>
    public class HubMapRenderer : MonoBehaviour
    {
        [Header("맵 설정")]
        [SerializeField] private int mapWidth = 30;
        [SerializeField] private int mapHeight = 30;
        [SerializeField] private float tileSize = 1f;

        [Header("맵 배경 이미지")]
        [SerializeField] private MeshRenderer mapBackground;
        [SerializeField] private Sprite mapSprite;
        [SerializeField] private Texture2D mapTexture;  // Texture로도 사용 가능

        [Header("충돌 영역 설정")]
        [SerializeField] private int wallThickness = 2;  // 가장자리 벽 두께

        // 이동 가능 영역 (true = 이동 가능)
        private bool[,] walkableMap;

        public int MapWidth => mapWidth;
        public int MapHeight => mapHeight;
        public float TileSize => tileSize;

        /// <summary>
        /// 맵 초기화
        /// </summary>
        public void Initialize(Sprite backgroundSprite = null)
        {
            if (backgroundSprite != null)
            {
                mapSprite = backgroundSprite;
            }
            SetupBackground();
            InitializeWalkableMap();
            Debug.Log($"[HubMapRenderer] 맵 초기화 완료 ({mapWidth}x{mapHeight})");
        }

        /// <summary>
        /// 맵 초기화 (Texture 사용)
        /// </summary>
        public void Initialize(Texture2D backgroundTexture)
        {
            mapTexture = backgroundTexture;
            SetupBackground();
            InitializeWalkableMap();
            Debug.Log($"[HubMapRenderer] 맵 초기화 완료 ({mapWidth}x{mapHeight})");
        }

        /// <summary>
        /// 배경 이미지 설정 (3D Quad 사용 - 스프라이트 정렬과 충돌 방지)
        /// </summary>
        private void SetupBackground()
        {
            float mapWorldWidth = mapWidth * tileSize;
            float mapWorldHeight = mapHeight * tileSize;
            float centerX = mapWorldWidth / 2f;
            float centerZ = mapWorldHeight / 2f;

            if (mapBackground == null)
            {
                // 3D Quad 생성 (바닥에 깔림)
                GameObject bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bgObj.name = "MapBackground";
                bgObj.transform.SetParent(transform);

                // 콜라이더 제거 (바닥 충돌은 별도로 처리)
                Collider col = bgObj.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);

                mapBackground = bgObj.GetComponent<MeshRenderer>();
            }

            // 배경 위치 (맵 중앙, 바닥)
            mapBackground.transform.position = new Vector3(centerX, -0.01f, centerZ);

            // Quad를 바닥에 깔기 위해 회전
            mapBackground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 스케일 조정 (맵 크기에 맞게)
            mapBackground.transform.localScale = new Vector3(mapWorldWidth, mapWorldHeight, 1f);

            // 텍스처 가져오기
            Texture2D tex = null;
            if (mapSprite != null)
            {
                tex = mapSprite.texture;
            }
            else if (mapTexture != null)
            {
                tex = mapTexture;
            }

            // 머티리얼 설정 (여러 Shader 시도)
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            if (shader != null)
            {
                Material mat = new Material(shader);
                if (tex != null)
                {
                    mat.mainTexture = tex;
                }
                else
                {
                    // 텍스처 없으면 기본 색상 (어두운 붉은색)
                    mat.color = new Color(0.3f, 0.1f, 0.1f);
                }
                mapBackground.material = mat;
                Debug.Log($"[HubMapRenderer] 배경 설정 완료 - Shader: {shader.name}, Texture: {(tex != null ? tex.name : "없음")}");
            }
            else
            {
                Debug.LogError("[HubMapRenderer] Shader를 찾을 수 없습니다!");
            }
        }

        /// <summary>
        /// 이동 가능 영역 초기화
        /// </summary>
        private void InitializeWalkableMap()
        {
            walkableMap = new bool[mapWidth, mapHeight];

            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    // 가장자리는 벽 (이동 불가)
                    if (x < wallThickness || x >= mapWidth - wallThickness ||
                        y < wallThickness || y >= mapHeight - wallThickness)
                    {
                        walkableMap[x, y] = false;
                    }
                    else
                    {
                        walkableMap[x, y] = true;
                    }
                }
            }
        }

        /// <summary>
        /// 특정 영역을 이동 불가로 설정 (장애물 등)
        /// </summary>
        public void SetUnwalkable(int x, int y)
        {
            if (IsValidPosition(x, y))
            {
                walkableMap[x, y] = false;
            }
        }

        /// <summary>
        /// 특정 영역을 이동 불가로 설정 (사각형 범위)
        /// </summary>
        public void SetUnwalkableArea(int startX, int startY, int width, int height)
        {
            for (int x = startX; x < startX + width; x++)
            {
                for (int y = startY; y < startY + height; y++)
                {
                    SetUnwalkable(x, y);
                }
            }
        }

        /// <summary>
        /// 유효한 좌표인지 확인
        /// </summary>
        public bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
        }

        /// <summary>
        /// 이동 가능한지 확인 (그리드 좌표)
        /// </summary>
        public bool IsWalkable(int x, int y)
        {
            if (!IsValidPosition(x, y)) return false;
            return walkableMap[x, y];
        }

        /// <summary>
        /// 이동 가능한지 확인 (월드 좌표)
        /// </summary>
        public bool IsWalkableWorld(Vector3 worldPos)
        {
            Vector2Int grid = WorldToGrid(worldPos);
            return IsWalkable(grid.x, grid.y);
        }

        /// <summary>
        /// 그리드 좌표 → 월드 좌표
        /// </summary>
        public Vector3 GridToWorld(int gridX, int gridY)
        {
            float worldX = gridX * tileSize + tileSize / 2f;
            float worldZ = gridY * tileSize + tileSize / 2f;
            return new Vector3(worldX, 0f, worldZ);
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
        /// 플레이어 스폰 위치
        /// </summary>
        public Vector3 GetPlayerSpawnPosition()
        {
            return GridToWorld(mapWidth / 2, mapHeight / 2 - 3);
        }

        /// <summary>
        /// 재단 위치
        /// </summary>
        public Vector3 GetAltarPosition()
        {
            return GridToWorld(mapWidth / 2, mapHeight / 2);
        }

        /// <summary>
        /// 포털 위치들 (위쪽에 4개 나란히)
        /// </summary>
        public Vector3[] GetPortalPositions()
        {
            int topY = mapHeight - 5;  // 위쪽에서 5칸 아래
            int spacing = 6;           // 포털 간격
            int startX = mapWidth / 2 - (spacing * 3 / 2);  // 중앙 기준 시작점

            return new Vector3[]
            {
                GridToWorld(startX, topY),                    // 장
                GridToWorld(startX + spacing, topY),          // 간
                GridToWorld(startX + spacing * 2, topY),      // 위
                GridToWorld(startX + spacing * 3, topY)       // 폐
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

            // 이동 가능 영역 (벽 제외)
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Vector3 walkableCenter = new Vector3(mapWidth * tileSize / 2f, 0, mapHeight * tileSize / 2f);
            Vector3 walkableSize = new Vector3(
                (mapWidth - wallThickness * 2) * tileSize,
                0.1f,
                (mapHeight - wallThickness * 2) * tileSize
            );
            Gizmos.DrawCube(walkableCenter, walkableSize);

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
