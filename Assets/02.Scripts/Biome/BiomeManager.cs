using UnityEngine;
using System.Collections.Generic;

namespace Necrocis
{
    /// <summary>
    /// 바이옴 맵 관리 기본 클래스 (청크 기반 오픈월드)
    /// </summary>
    public abstract class BiomeManager : MonoBehaviour
    {
        [Header("맵 설정")]
        [SerializeField] protected int mapWidth = 90;
        [SerializeField] protected int mapHeight = 90;
        [SerializeField] protected int chunkSize = 16;
        [SerializeField] protected float tileSize = 1f;

        [Header("생성 설정")]
        [SerializeField] protected int seed = 0;
        [SerializeField] protected bool useRandomSeed = true;

        [Header("청크 로딩 설정")]
        [SerializeField] protected int loadDistance = 2;    // 플레이어 주변 로드할 청크 수
        [SerializeField] protected int unloadDistance = 3;  // 언로드 거리
        [SerializeField] protected float chunkUpdateInterval = 0.5f;  // 청크 갱신 간격

        [Header("참조")]
        [SerializeField] protected Transform tilesParent;
        [SerializeField] protected Transform objectsParent;

        // 청크 정보
        protected int chunksX;
        protected int chunksY;
        protected Chunk[,] chunks;

        // 로드된 청크 추적
        protected HashSet<Vector2Int> loadedChunks = new HashSet<Vector2Int>();
        protected Transform playerTransform;
        protected Vector2Int lastPlayerChunk = new Vector2Int(-999, -999);
        protected float chunkUpdateTimer = 0f;

        // 타일 데이터
        protected BiomeTile[,] tileMap;

        // 노이즈 생성기
        protected FastNoiseLite noise;

        // 바이옴 타입 (하위 클래스에서 설정)
        protected BiomeType biomeType = BiomeType.None;

        public int MapWidth => mapWidth;
        public int MapHeight => mapHeight;
        public int ChunkSize => chunkSize;
        public float TileSize => tileSize;
        public int Seed => seed;
        public BiomeType BiomeType => biomeType;

        protected virtual void Awake()
        {
            // 시드 설정
            if (useRandomSeed)
            {
                seed = Random.Range(0, 100000);
            }
            Random.InitState(seed);

            // 노이즈 초기화
            noise = new FastNoiseLite(seed);

            // 청크 수 계산
            chunksX = Mathf.CeilToInt((float)mapWidth / chunkSize);
            chunksY = Mathf.CeilToInt((float)mapHeight / chunkSize);

            Debug.Log($"[BiomeManager] 맵: {mapWidth}x{mapHeight}, 청크: {chunksX}x{chunksY} (각 {chunkSize}x{chunkSize})");
        }

        protected virtual void Start()
        {
            Initialize();
            SetupPlayer();
        }

        protected virtual void Update()
        {
            if (playerTransform == null) return;

            chunkUpdateTimer += Time.deltaTime;
            if (chunkUpdateTimer >= chunkUpdateInterval)
            {
                chunkUpdateTimer = 0f;
                UpdateChunks();
            }
        }

        /// <summary>
        /// 초기화
        /// </summary>
        protected virtual void Initialize()
        {
            // 부모 오브젝트 생성
            if (tilesParent == null)
            {
                GameObject tilesObj = new GameObject("Tiles");
                tilesObj.transform.SetParent(transform);
                tilesParent = tilesObj.transform;
            }

            if (objectsParent == null)
            {
                GameObject objsObj = new GameObject("Objects");
                objsObj.transform.SetParent(transform);
                objectsParent = objsObj.transform;
            }

            // 타일맵 초기화
            tileMap = new BiomeTile[mapWidth, mapHeight];

            // 청크 초기화
            chunks = new Chunk[chunksX, chunksY];
            for (int cx = 0; cx < chunksX; cx++)
            {
                for (int cy = 0; cy < chunksY; cy++)
                {
                    chunks[cx, cy] = new Chunk(cx, cy, chunkSize);
                }
            }
        }

        /// <summary>
        /// 플레이어 설정
        /// </summary>
        protected virtual void SetupPlayer()
        {
            // 기존 플레이어 찾기
            PlayerController player = PlayerController.Instance;
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
            }

            // 플레이어가 없으면 Hub에서 시작하라고 안내
            if (player == null)
            {
                Debug.LogError("[BiomeManager] 플레이어가 없습니다! Hub 씬에서 시작하세요.");
                // Hub로 이동
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.ReturnToHub();
                }
                return;
            }

            playerTransform = player.transform;
            Vector3 spawnPos = GetPlayerSpawnPosition();
            player.SpawnAt(spawnPos);
            Debug.Log($"[BiomeManager] 플레이어 스폰: {spawnPos}");

            // 카메라 설정
            SetupCamera(playerTransform);

            // 초기 청크 로드
            UpdateChunks();
        }

        /// <summary>
        /// 카메라 설정
        /// </summary>
        protected virtual void SetupCamera(Transform target)
        {
            DontStarveCamera cam = DontStarveCamera.Instance;
            if (cam == null)
            {
                cam = FindFirstObjectByType<DontStarveCamera>();
            }

            if (cam != null)
            {
                cam.SetTarget(target);
                cam.SnapToTarget();
                Debug.Log("[BiomeManager] 카메라 타겟 설정 완료");
            }
            else
            {
                Debug.LogError("[BiomeManager] 카메라가 없습니다! Hub 씬의 Camera에 DontStarveCamera 스크립트가 있는지 확인하세요.");
            }
        }

        /// <summary>
        /// 청크 업데이트 (로드/언로드)
        /// </summary>
        protected virtual void UpdateChunks()
        {
            if (playerTransform == null) return;

            Vector2Int playerGrid = WorldToGrid(playerTransform.position);
            Vector2Int playerChunk = GridToChunk(playerGrid.x, playerGrid.y);

            // 플레이어 청크가 변경되지 않았으면 스킵
            if (playerChunk == lastPlayerChunk) return;
            lastPlayerChunk = playerChunk;

            // 로드할 청크 목록
            HashSet<Vector2Int> chunksToLoad = new HashSet<Vector2Int>();
            for (int dx = -loadDistance; dx <= loadDistance; dx++)
            {
                for (int dy = -loadDistance; dy <= loadDistance; dy++)
                {
                    int cx = playerChunk.x + dx;
                    int cy = playerChunk.y + dy;

                    if (IsValidChunk(cx, cy))
                    {
                        chunksToLoad.Add(new Vector2Int(cx, cy));
                    }
                }
            }

            // 언로드할 청크 찾기
            List<Vector2Int> chunksToUnload = new List<Vector2Int>();
            foreach (var chunkPos in loadedChunks)
            {
                int dist = Mathf.Max(Mathf.Abs(chunkPos.x - playerChunk.x), Mathf.Abs(chunkPos.y - playerChunk.y));
                if (dist > unloadDistance)
                {
                    chunksToUnload.Add(chunkPos);
                }
            }

            // 언로드
            foreach (var chunkPos in chunksToUnload)
            {
                UnloadChunk(chunkPos.x, chunkPos.y);
            }

            // 로드
            foreach (var chunkPos in chunksToLoad)
            {
                if (!loadedChunks.Contains(chunkPos))
                {
                    LoadChunk(chunkPos.x, chunkPos.y);
                }
            }
        }

        /// <summary>
        /// 청크 로드
        /// </summary>
        protected virtual void LoadChunk(int chunkX, int chunkY)
        {
            if (!IsValidChunk(chunkX, chunkY)) return;

            Chunk chunk = chunks[chunkX, chunkY];

            // 이미 로드됨
            if (chunk.isLoaded) return;

            // 저장된 데이터 확인
            ChunkSaveData savedData = ChunkSaveSystem.LoadChunk(biomeType, chunkX, chunkY);

            if (savedData != null && savedData.isGenerated)
            {
                // 저장된 데이터로 복원
                RestoreChunk(chunk, savedData);
            }
            else
            {
                // 새로 생성
                GenerateChunk(chunkX, chunkY);
            }

            chunk.isLoaded = true;
            loadedChunks.Add(new Vector2Int(chunkX, chunkY));
        }

        /// <summary>
        /// 청크 언로드
        /// </summary>
        protected virtual void UnloadChunk(int chunkX, int chunkY)
        {
            if (!IsValidChunk(chunkX, chunkY)) return;

            Chunk chunk = chunks[chunkX, chunkY];
            if (!chunk.isLoaded) return;

            // 청크 데이터 저장
            SaveChunk(chunkX, chunkY);

            // 오브젝트 제거
            DestroyChunkObjects(chunk);

            chunk.isLoaded = false;
            loadedChunks.Remove(new Vector2Int(chunkX, chunkY));
        }

        /// <summary>
        /// 청크 생성 (하위 클래스에서 구현)
        /// </summary>
        protected abstract void GenerateChunk(int chunkX, int chunkY);

        /// <summary>
        /// 청크 저장
        /// </summary>
        protected virtual void SaveChunk(int chunkX, int chunkY)
        {
            Chunk chunk = chunks[chunkX, chunkY];

            ChunkSaveData saveData = new ChunkSaveData
            {
                chunkX = chunkX,
                chunkY = chunkY,
                seed = seed,
                isGenerated = chunk.isGenerated
            };

            // 타일 데이터 저장
            int startX = chunkX * chunkSize;
            int startY = chunkY * chunkSize;

            for (int lx = 0; lx < chunkSize; lx++)
            {
                for (int ly = 0; ly < chunkSize; ly++)
                {
                    int gx = startX + lx;
                    int gy = startY + ly;

                    if (IsValidPosition(gx, gy) && tileMap[gx, gy] != null)
                    {
                        TileSaveData tileData = new TileSaveData
                        {
                            localX = lx,
                            localY = ly,
                            tileType = (int)tileMap[gx, gy].tileType,
                            isWalkable = tileMap[gx, gy].isWalkable,
                            spriteKey = GetTileSpriteKey(tileMap[gx, gy])
                        };
                        saveData.tiles.Add(tileData);
                    }
                }
            }

            // 오브젝트 데이터 저장
            foreach (var objData in chunk.objectDataList)
            {
                saveData.objects.Add(objData);
            }

            ChunkSaveSystem.SaveChunk(biomeType, chunkX, chunkY, saveData);
        }

        /// <summary>
        /// 청크 복원
        /// </summary>
        protected virtual void RestoreChunk(Chunk chunk, ChunkSaveData saveData)
        {
            int startX = chunk.chunkX * chunkSize;
            int startY = chunk.chunkY * chunkSize;

            // 타일 복원
            foreach (var tileData in saveData.tiles)
            {
                int gx = startX + tileData.localX;
                int gy = startY + tileData.localY;

                if (IsValidPosition(gx, gy))
                {
                    Sprite sprite = GetSpriteByKey(tileData.spriteKey);
                    BiomeTile tile = new BiomeTile((BiomeTileType)tileData.tileType, tileData.isWalkable, sprite);
                    tileMap[gx, gy] = tile;
                    PlaceTile(gx, gy, tile, chunk);
                }
            }

            // 오브젝트 복원
            foreach (var objData in saveData.objects)
            {
                if (!objData.isDestroyed && !objData.isCollected)
                {
                    int gx = startX + objData.localX;
                    int gy = startY + objData.localY;
                    RestoreObject(gx, gy, objData, chunk);
                }
            }

            chunk.isGenerated = true;
            chunk.objectDataList = new List<ObjectSaveData>(saveData.objects);
        }

        /// <summary>
        /// 청크 오브젝트 파괴
        /// </summary>
        protected virtual void DestroyChunkObjects(Chunk chunk)
        {
            foreach (var go in chunk.tileObjects)
            {
                if (go != null) Destroy(go);
            }
            chunk.tileObjects.Clear();

            foreach (var go in chunk.gameObjects)
            {
                if (go != null) Destroy(go);
            }
            chunk.gameObjects.Clear();
        }

        /// <summary>
        /// 타일 배치 (청크 추적 포함)
        /// </summary>
        protected abstract void PlaceTile(int x, int y, BiomeTile tile, Chunk chunk);

        /// <summary>
        /// 오브젝트 복원 (하위 클래스에서 구현)
        /// </summary>
        protected abstract void RestoreObject(int x, int y, ObjectSaveData objData, Chunk chunk);

        /// <summary>
        /// 타일 스프라이트 키 가져오기 (하위 클래스에서 구현)
        /// </summary>
        protected abstract string GetTileSpriteKey(BiomeTile tile);

        /// <summary>
        /// 스프라이트 키로 스프라이트 가져오기 (하위 클래스에서 구현)
        /// </summary>
        protected abstract Sprite GetSpriteByKey(string key);

        /// <summary>
        /// 유효한 청크인지 확인
        /// </summary>
        public bool IsValidChunk(int chunkX, int chunkY)
        {
            return chunkX >= 0 && chunkX < chunksX && chunkY >= 0 && chunkY < chunksY;
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
        /// 그리드 좌표 → 청크 좌표
        /// </summary>
        public Vector2Int GridToChunk(int gridX, int gridY)
        {
            return new Vector2Int(gridX / chunkSize, gridY / chunkSize);
        }

        /// <summary>
        /// 유효한 좌표인지 확인
        /// </summary>
        public bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
        }

        /// <summary>
        /// 이동 가능한지 확인
        /// </summary>
        public bool IsWalkable(int x, int y)
        {
            if (!IsValidPosition(x, y)) return false;
            return tileMap[x, y] != null && tileMap[x, y].isWalkable;
        }

        /// <summary>
        /// 플레이어 스폰 위치
        /// </summary>
        public virtual Vector3 GetPlayerSpawnPosition()
        {
            return GridToWorld(mapWidth / 2, 5);
        }

        /// <summary>
        /// 허브 귀환 포털 위치
        /// </summary>
        public virtual Vector3 GetReturnPortalPosition()
        {
            return GridToWorld(mapWidth / 2, 3);
        }

        /// <summary>
        /// 씬 종료 시 모든 로드된 청크 저장
        /// </summary>
        protected virtual void OnDestroy()
        {
            foreach (var chunkPos in loadedChunks)
            {
                SaveChunk(chunkPos.x, chunkPos.y);
            }
        }
    }

    /// <summary>
    /// 청크 데이터
    /// </summary>
    [System.Serializable]
    public class Chunk
    {
        public int chunkX;
        public int chunkY;
        public int size;
        public bool isGenerated;
        public bool isLoaded;

        // 청크 내 게임오브젝트
        public List<GameObject> tileObjects = new List<GameObject>();
        public List<GameObject> gameObjects = new List<GameObject>();

        // 오브젝트 상태 데이터
        public List<ObjectSaveData> objectDataList = new List<ObjectSaveData>();

        public Chunk(int x, int y, int size)
        {
            this.chunkX = x;
            this.chunkY = y;
            this.size = size;
            this.isGenerated = false;
            this.isLoaded = false;
        }
    }

    /// <summary>
    /// 바이옴 타일 데이터
    /// </summary>
    [System.Serializable]
    public class BiomeTile
    {
        public BiomeTileType tileType;
        public bool isWalkable;
        public Sprite sprite;
        public GameObject tileObject;

        public BiomeTile(BiomeTileType type, bool walkable = true, Sprite spr = null)
        {
            tileType = type;
            isWalkable = walkable;
            sprite = spr;
        }
    }

    /// <summary>
    /// 바이옴 타일 종류 (기본)
    /// </summary>
    public enum BiomeTileType
    {
        None,
        Floor,          // 기본 바닥
        FloorVariant,   // 바닥 변형
        Decoration,     // 장식 바닥 (풀 등)
        Puddle,         // 웅덩이
        Wall,           // 벽
        Obstacle        // 장애물
    }

    /// <summary>
    /// 바이옴 오브젝트 종류 (기본)
    /// </summary>
    public enum BiomeObjectType
    {
        None,
        DecorationSmall,        // 작은 장식물
        DecorationLarge,        // 큰 장식물
        InteractableDecoration, // 상호작용 가능 장식물
        DestructibleObject,     // 파괴 가능 오브젝트
        Item,                   // 아이템
        MonsterSpawnPoint,      // 몬스터 스폰 포인트
        ReturnPortal            // 귀환 포털
    }
}
