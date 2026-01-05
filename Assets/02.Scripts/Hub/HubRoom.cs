using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 중간방(허브) 관리자 - 통이미지 배경 + 포털/재단 배치
    /// </summary>
    public class HubRoom : MonoBehaviour
    {
        public static HubRoom Instance { get; private set; }

        [Header("맵 렌더러")]
        [SerializeField] private HubMapRenderer mapRenderer;

        [Header("맵 배경")]
        [SerializeField] private Sprite mapBackgroundSprite;

        [Header("포털 스프라이트 (색깔별)")]
        [SerializeField] private Sprite portalIntestine;  // 장 (북)
        [SerializeField] private Sprite portalLiver;      // 간 (동)
        [SerializeField] private Sprite portalStomach;    // 위 (남)
        [SerializeField] private Sprite portalLung;       // 폐 (서)

        [Header("재단 스프라이트")]
        [SerializeField] private Sprite altarSprite;

        [Header("스케일 설정")]
        [SerializeField] private float portalScale = 0.5f;    // 포털 크기
        [SerializeField] private float altarScale = 0.5f;     // 재단 크기

        [Header("생성된 오브젝트")]
        [SerializeField] private Portal[] portals = new Portal[4];
        [SerializeField] private Altar altar;

        [Header("오브젝트 부모")]
        [SerializeField] private Transform objectsParent;

        private bool isGenerated = false;

        public bool IsGenerated => isGenerated;
        public HubMapRenderer MapRenderer => mapRenderer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            GenerateHub();
        }

        /// <summary>
        /// 중간방 생성
        /// </summary>
        public void GenerateHub()
        {
            if (isGenerated)
            {
                Debug.Log("[HubRoom] 이미 생성됨");
                return;
            }

            // 오브젝트 부모 생성
            if (objectsParent == null)
            {
                GameObject parent = new GameObject("HubObjects");
                parent.transform.SetParent(transform);
                objectsParent = parent.transform;
            }

            // 맵 렌더러 초기화
            if (mapRenderer == null)
            {
                mapRenderer = GetComponentInChildren<HubMapRenderer>();
                if (mapRenderer == null)
                {
                    GameObject rendererObj = new GameObject("MapRenderer");
                    rendererObj.transform.SetParent(transform);
                    mapRenderer = rendererObj.AddComponent<HubMapRenderer>();
                }
            }
            mapRenderer.Initialize(mapBackgroundSprite);

            // 벽 콜라이더 생성 (보이지 않는 충돌용)
            SetupWallColliders();

            // 포털 생성
            SetupPortals();

            // 재단 생성
            SetupAltar();

            isGenerated = true;
            Debug.Log("[HubRoom] 중간방 생성 완료!");
        }

        /// <summary>
        /// 벽 콜라이더 생성 (보이지 않는 충돌용)
        /// </summary>
        private void SetupWallColliders()
        {
            GameObject wallsParent = new GameObject("WallColliders");
            wallsParent.transform.SetParent(objectsParent);

            int mapWidth = mapRenderer.MapWidth;
            int mapHeight = mapRenderer.MapHeight;
            float tileSize = mapRenderer.TileSize;
            float wallThickness = 0.5f;  // 벽 두께 줄임

            float wallHeight = 3f;  // 벽 높이

            // 북쪽 벽
            CreateWall(wallsParent.transform, "Wall_North",
                new Vector3(mapWidth * tileSize / 2f, wallHeight / 2f, mapHeight * tileSize - wallThickness * tileSize / 2f),
                new Vector3(mapWidth * tileSize, wallHeight, wallThickness * tileSize));

            // 남쪽 벽
            CreateWall(wallsParent.transform, "Wall_South",
                new Vector3(mapWidth * tileSize / 2f, wallHeight / 2f, wallThickness * tileSize / 2f),
                new Vector3(mapWidth * tileSize, wallHeight, wallThickness * tileSize));

            // 동쪽 벽
            CreateWall(wallsParent.transform, "Wall_East",
                new Vector3(mapWidth * tileSize - wallThickness * tileSize / 2f, wallHeight / 2f, mapHeight * tileSize / 2f),
                new Vector3(wallThickness * tileSize, wallHeight, mapHeight * tileSize));

            // 서쪽 벽
            CreateWall(wallsParent.transform, "Wall_West",
                new Vector3(wallThickness * tileSize / 2f, wallHeight / 2f, mapHeight * tileSize / 2f),
                new Vector3(wallThickness * tileSize, wallHeight, mapHeight * tileSize));

            Debug.Log("[HubRoom] 벽 콜라이더 생성 완료");
        }

        /// <summary>
        /// 개별 벽 생성
        /// </summary>
        private void CreateWall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent);
            wall.transform.position = position;
            wall.layer = LayerMask.NameToLayer("Default");

            BoxCollider col = wall.AddComponent<BoxCollider>();
            col.size = size;
            col.isTrigger = false;  // 실제 충돌
        }

        /// <summary>
        /// 포털 4개 생성
        /// </summary>
        private void SetupPortals()
        {
            Vector3[] positions = mapRenderer.GetPortalPositions();
            Sprite[] sprites = { portalIntestine, portalLiver, portalStomach, portalLung };
            BiomeType[] biomes = { BiomeType.Intestine, BiomeType.Liver, BiomeType.Stomach, BiomeType.Lung };
            string[] names = { "Portal_장", "Portal_간", "Portal_위", "Portal_폐" };

            for (int i = 0; i < 4; i++)
            {
                if (portals[i] != null)
                {
                    portals[i].transform.position = positions[i];
                    portals[i].Initialize(biomes[i]);
                    continue;
                }

                // 포털 오브젝트 생성
                GameObject portalObj = new GameObject(names[i]);
                portalObj.transform.SetParent(objectsParent);
                portalObj.transform.position = positions[i];
                portalObj.transform.localScale = Vector3.one * portalScale;

                // 스프라이트 렌더러
                SpriteRenderer sr = portalObj.AddComponent<SpriteRenderer>();
                if (sprites[i] != null)
                {
                    sr.sprite = sprites[i];
                }

                // Y정렬 사용 - 플레이어가 포털보다 앞에 있으면 앞에 보이고, 뒤에 있으면 뒤에 보임
                portalObj.AddComponent<SpriteYSort>();

                // Billboard로 카메라 향하게 (2.5D)
                portalObj.AddComponent<Billboard>();

                // 콜라이더 (트리거)
                BoxCollider col = portalObj.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(2f, 2f, 1f);

                // Portal 컴포넌트
                Portal portal = portalObj.AddComponent<Portal>();
                portal.Initialize(biomes[i]);
                portals[i] = portal;
            }
        }

        /// <summary>
        /// 재단 생성
        /// </summary>
        private void SetupAltar()
        {
            Vector3 altarPos = mapRenderer.GetAltarPosition();

            if (altar != null)
            {
                altar.transform.position = altarPos;
                return;
            }

            // 재단 오브젝트 생성
            GameObject altarObj = new GameObject("Altar");
            altarObj.transform.SetParent(objectsParent);
            altarObj.transform.position = altarPos;
            altarObj.transform.localScale = Vector3.one * altarScale;

            // 스프라이트 렌더러
            SpriteRenderer sr = altarObj.AddComponent<SpriteRenderer>();
            if (altarSprite != null)
            {
                sr.sprite = altarSprite;
            }
            sr.sortingOrder = 2;

            // Billboard로 카메라 향하게 (2.5D)
            altarObj.AddComponent<Billboard>();

            // 콜라이더 (트리거)
            BoxCollider col = altarObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3f, 3f, 1f);

            // Altar 컴포넌트
            altar = altarObj.AddComponent<Altar>();
        }

        /// <summary>
        /// 플레이어 스폰 위치
        /// </summary>
        public Vector3 GetPlayerSpawnPosition()
        {
            if (mapRenderer != null)
                return mapRenderer.GetPlayerSpawnPosition();
            return Vector3.zero;
        }

        /// <summary>
        /// 이동 가능 여부
        /// </summary>
        public bool IsWalkable(Vector3 worldPos)
        {
            if (mapRenderer == null) return false;
            return mapRenderer.IsWalkableWorld(worldPos);
        }

        /// <summary>
        /// 포털 가져오기
        /// </summary>
        public Portal GetPortal(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Intestine => portals[0],
                BiomeType.Liver => portals[1],
                BiomeType.Stomach => portals[2],
                BiomeType.Lung => portals[3],
                _ => null
            };
        }

        /// <summary>
        /// 중간방 리셋
        /// </summary>
        public void ResetHub()
        {
            if (objectsParent != null)
            {
                foreach (Transform child in objectsParent)
                {
                    Destroy(child.gameObject);
                }
            }

            portals = new Portal[4];
            altar = null;
            isGenerated = false;

            GenerateHub();
        }

#if UNITY_EDITOR
        [ContextMenu("Generate Hub")]
        private void EditorGenerateHub()
        {
            isGenerated = false;
            GenerateHub();
        }

        [ContextMenu("Clear Hub")]
        private void EditorClearHub()
        {
            if (objectsParent != null)
            {
                while (objectsParent.childCount > 0)
                {
                    DestroyImmediate(objectsParent.GetChild(0).gameObject);
                }
            }
            portals = new Portal[4];
            altar = null;
            isGenerated = false;
        }
#endif
    }
}
