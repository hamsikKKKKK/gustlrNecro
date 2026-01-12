using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace Necrocis
{
    /// <summary>
    /// 바이옴 맵 관리 기본 클래스 (청크 기반 + Tilemap)
    /// </summary>
    public abstract class BiomeManager : MonoBehaviour
    {
        public static BiomeManager Active { get; private set; }

        [Header("맵 설정")]
        [SerializeField] protected int mapWidth = 90;
        [SerializeField] protected int mapHeight = 90;
        [SerializeField] protected int chunkSize;
        [SerializeField] protected float tileSize = 1f;

        [Header("생성 설정")]
        [SerializeField] protected int seed = 0;
        [SerializeField] protected bool useRandomSeed = true;

        [Header("청크 로딩 설정")]
        [SerializeField] protected int loadDistance = 2;    // 플레이어 주변 로드할 청크 수
        [SerializeField] protected int unloadDistance = 3;  // 언로드 거리
        [SerializeField] protected float chunkUpdateInterval = 0.5f;  // 청크 갱신 간격

        [Header("오브젝트 로딩 설정")]
        [SerializeField] protected int objectLoadDistance = 1;
        [SerializeField] protected int objectUnloadDistance = 2;
        [SerializeField] protected int objectGenerationBudget = 256; // 프레임당 처리 예산

        [Header("Tilemap")]
        [SerializeField] protected Grid grid;
        [SerializeField] protected Transform tilesParent;
        [SerializeField] protected Transform objectsParent;
        [SerializeField] protected Transform pooledObjectsParent;

        [Header("높이 설정")]
        [SerializeField] protected bool enableHeight = true;
        [SerializeField] protected int minHeightLevel = -1;
        [SerializeField] protected int maxHeightLevel = 1;
        [SerializeField] protected int maxStepHeight = 1;
        [SerializeField] protected float heightStep = 0.5f;
        [SerializeField] protected float cliffOverlayOffset = 0.01f;
        [SerializeField] protected Color cliffTint = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] protected float playerHeightOffset = -2f;

        // 청크 정보
        protected int chunksX;
        protected int chunksY;
        protected Chunk[,] chunks;

        // 로드된 청크 추적
        protected HashSet<Vector2Int> loadedChunks = new HashSet<Vector2Int>();
        protected Transform playerTransform;
        protected Vector2Int lastPlayerChunk = new Vector2Int(-999, -999);
        protected float chunkUpdateTimer = 0f;

        // 바이옴 타입 (하위 클래스에서 설정)
        protected BiomeType biomeType = BiomeType.None;

        // 로드된 청크 내 이동 불가 타일
        protected HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();

        private readonly Dictionary<int, Stack<GameObject>> objectPool = new Dictionary<int, Stack<GameObject>>();

        public int MapWidth => mapWidth;
        public int MapHeight => mapHeight;
        public int ChunkSize => chunkSize;
        public float TileSize => tileSize;
        public int Seed => seed;
        public BiomeType BiomeType => biomeType;
        public float HeightStep => heightStep;
        public int MinHeightLevel => minHeightLevel;
        public int MaxHeightLevel => maxHeightLevel;

        protected virtual void Awake()
        {
            Active = this;

            // 시드 설정
            if (useRandomSeed)
            {
                seed = Random.Range(0, 100000);
            }

            if (chunkSize <= 0)
            {
                Debug.LogError("[BiomeManager] chunkSize가 0 이하입니다. 인스펙터에서 설정하세요.");
                chunkSize = 1;
            }

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
            EnsureGrid();

            // 부모 오브젝트 생성
            if (tilesParent == null)
            {
                GameObject tilesObj = new GameObject("Tiles");
                tilesObj.transform.SetParent(grid.transform, false);
                tilesParent = tilesObj.transform;
            }

            if (objectsParent == null)
            {
                GameObject objsObj = new GameObject("Objects");
                objsObj.transform.SetParent(transform, false);
                objectsParent = objsObj.transform;
            }

            if (pooledObjectsParent == null)
            {
                GameObject poolObj = new GameObject("PooledObjects");
                poolObj.transform.SetParent(objectsParent, false);
                pooledObjectsParent = poolObj.transform;
            }

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

        private void EnsureGrid()
        {
            if (grid != null) return;

            GameObject gridObj = new GameObject("BiomeGrid");
            gridObj.transform.SetParent(transform, false);
            grid = gridObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(tileSize, tileSize, tileSize);
            grid.cellGap = Vector3.zero;
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
            Vector2Int spawnGrid = WorldToGrid(spawnPos);
            float groundHeight = GetGroundHeight(spawnGrid.x, spawnGrid.y);
            spawnPos.y = groundHeight + playerHeightOffset;
            player.SpawnAt(spawnPos);
            player.LockY(playerHeightOffset);
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

            UpdateObjectChunks(playerChunk);
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

            EnsureChunkRoot(chunk);
            LoadChunkChanges(chunk);

            // 타일/오브젝트 생성
            GenerateTiles(chunk);

            chunk.isLoaded = true;
            loadedChunks.Add(new Vector2Int(chunkX, chunkY));
            Debug.Log($"[BiomeManager] 청크 로드: ({chunkX}, {chunkY})");
        }

        private void EnsureChunkRoot(Chunk chunk)
        {
            if (chunk.root != null) return;

            GameObject chunkRoot = new GameObject($"Chunk_{chunk.chunkX}_{chunk.chunkY}");
            chunkRoot.transform.SetParent(tilesParent, false);

            Vector3 chunkOrigin = new Vector3(chunk.chunkX * chunkSize * tileSize, 0f, chunk.chunkY * chunkSize * tileSize);
            chunkRoot.transform.localPosition = chunkOrigin;

            chunk.root = chunkRoot;
            CreateChunkTilemaps(chunk);
        }

        private void CreateChunkTilemaps(Chunk chunk)
        {
            int levelCount = GetHeightLevelCount();
            chunk.tilemaps = new Tilemap[levelCount];
            chunk.tilemapRenderers = new TilemapRenderer[levelCount];
            chunk.cliffTilemaps = new Tilemap[levelCount];
            chunk.cliffTilemapRenderers = new TilemapRenderer[levelCount];

            for (int i = 0; i < levelCount; i++)
            {
                int heightLevel = minHeightLevel + i;
                float heightOffset = heightLevel * heightStep;

                chunk.tilemaps[i] = CreateTilemapLayer(chunk.root.transform, $"Tiles_{heightLevel}", heightOffset, -1000 + i * 2, out chunk.tilemapRenderers[i]);
                chunk.cliffTilemaps[i] = CreateTilemapLayer(chunk.root.transform, $"Cliff_{heightLevel}", heightOffset + cliffOverlayOffset, -999 + i * 2, out chunk.cliffTilemapRenderers[i]);
                chunk.cliffTilemaps[i].color = cliffTint;
            }
        }

        private Tilemap CreateTilemapLayer(Transform parent, string name, float yOffset, int sortingOrder, out TilemapRenderer renderer)
        {
            GameObject tileObj = new GameObject(name);
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.localPosition = new Vector3(0f, yOffset, 0f);
            tileObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Tilemap tilemap = tileObj.AddComponent<Tilemap>();
            renderer = tileObj.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private void LoadChunkChanges(Chunk chunk)
        {
            if (chunk.hasLoadedSave) return;

            ChunkSaveData savedData = ChunkSaveSystem.LoadChunk(biomeType, chunk.chunkX, chunk.chunkY);
            if (savedData != null)
            {
                if (savedData.objectStates != null)
                {
                    foreach (var objState in savedData.objectStates)
                    {
                        ObjectId id = new ObjectId(objState.worldX, objState.worldY, objState.objectType);
                        chunk.modifiedObjects[id] = new ObjectStateDelta(objState.isDestroyed, objState.isCollected);
                    }
                }

                if (savedData.tileDeltas != null)
                {
                    foreach (var tileDelta in savedData.tileDeltas)
                    {
                        Vector2Int pos = new Vector2Int(tileDelta.worldX, tileDelta.worldY);
                        chunk.modifiedTiles[pos] = (BiomeTileType)tileDelta.tileType;
                    }
                }
            }

            chunk.hasLoadedSave = true;
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
            SaveChunk(chunk);

            // 오브젝트 제거
            UnloadChunkObjects(chunk);

            // 타일 제거
            ClearChunkTilemaps(chunk);

            chunk.isLoaded = false;
            loadedChunks.Remove(new Vector2Int(chunkX, chunkY));
            Debug.Log($"[BiomeManager] 청크 언로드: ({chunkX}, {chunkY})");
        }

        /// <summary>
        /// 타일 생성
        /// </summary>
        protected virtual void GenerateTiles(Chunk chunk)
        {
            if (chunk.tilemaps == null || chunk.tilemaps.Length == 0) return;

            int levelCount = GetHeightLevelCount();
            int tileCount = chunkSize * chunkSize;
            TileBase[][] tilesByLevel = new TileBase[levelCount][];
            TileBase[][] cliffsByLevel = new TileBase[levelCount][];
            TileBase[] baseTiles = new TileBase[tileCount];
            int[,] heightLevels = new int[chunkSize, chunkSize];

            for (int i = 0; i < levelCount; i++)
            {
                tilesByLevel[i] = new TileBase[tileCount];
                cliffsByLevel[i] = new TileBase[tileCount];
            }

            int startX = chunk.chunkX * chunkSize;
            int startY = chunk.chunkY * chunkSize;

            for (int ly = 0; ly < chunkSize; ly++)
            {
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    int gx = startX + lx;
                    int gy = startY + ly;
                    int index = ly * chunkSize + lx;

                    if (!IsValidPosition(gx, gy))
                    {
                        baseTiles[index] = null;
                        continue;
                    }

                    TileSample sample = SampleTile(gx, gy, chunk);
                    baseTiles[index] = sample.tile;

                    int heightLevel = GetHeightLevel(gx, gy);
                    heightLevels[lx, ly] = heightLevel;
                    int heightIndex = GetHeightLevelIndex(heightLevel);
                    tilesByLevel[heightIndex][index] = sample.tile;
                }
            }

            for (int ly = 1; ly < chunkSize; ly++)
            {
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    int upperIndex = ly * chunkSize + lx;
                    int lowerIndex = (ly - 1) * chunkSize + lx;

                    if (baseTiles[upperIndex] == null || baseTiles[lowerIndex] == null)
                    {
                        continue;
                    }

                    int upperLevel = heightLevels[lx, ly];
                    int lowerLevel = heightLevels[lx, ly - 1];
                    if (upperLevel <= lowerLevel)
                    {
                        continue;
                    }

                    int lowerLevelIndex = GetHeightLevelIndex(lowerLevel);
                    cliffsByLevel[lowerLevelIndex][lowerIndex] = baseTiles[upperIndex];
                }
            }

            BoundsInt bounds = new BoundsInt(0, 0, 0, chunkSize, chunkSize, 1);
            for (int i = 0; i < levelCount; i++)
            {
                chunk.tilemaps[i].SetTilesBlock(bounds, tilesByLevel[i]);
                chunk.cliffTilemaps[i].SetTilesBlock(bounds, cliffsByLevel[i]);
            }
        }

        /// <summary>
        /// 오브젝트 생성 (하위 클래스에서 구현)
        /// </summary>
        protected abstract void GenerateObjectsForChunk(Chunk chunk);

        protected virtual System.Collections.IEnumerator GenerateObjectsForChunkAsync(Chunk chunk)
        {
            GenerateObjectsForChunk(chunk);
            yield break;
        }

        private void UpdateObjectChunks(Vector2Int playerChunk)
        {
            if (chunks == null) return;

            HashSet<Vector2Int> objectsToLoad = new HashSet<Vector2Int>();
            for (int dx = -objectLoadDistance; dx <= objectLoadDistance; dx++)
            {
                for (int dy = -objectLoadDistance; dy <= objectLoadDistance; dy++)
                {
                    int cx = playerChunk.x + dx;
                    int cy = playerChunk.y + dy;
                    if (IsValidChunk(cx, cy))
                    {
                        Vector2Int pos = new Vector2Int(cx, cy);
                        if (loadedChunks.Contains(pos))
                        {
                            objectsToLoad.Add(pos);
                        }
                    }
                }
            }

            foreach (var chunkPos in loadedChunks)
            {
                Chunk chunk = chunks[chunkPos.x, chunkPos.y];
                int dist = Mathf.Max(Mathf.Abs(chunkPos.x - playerChunk.x), Mathf.Abs(chunkPos.y - playerChunk.y));
                if (dist > objectUnloadDistance)
                {
                    UnloadChunkObjects(chunk);
                }
                else if (objectsToLoad.Contains(chunkPos))
                {
                    LoadChunkObjects(chunk);
                }
            }
        }

        private void LoadChunkObjects(Chunk chunk)
        {
            if (chunk.isObjectsLoaded || chunk.objectGenerationRoutine != null) return;

            chunk.objectGenerationRoutine = StartCoroutine(GenerateObjectsRoutine(chunk));
            Debug.Log($"[BiomeManager] 오브젝트 로드 예약: ({chunk.chunkX}, {chunk.chunkY})");
        }

        private System.Collections.IEnumerator GenerateObjectsRoutine(Chunk chunk)
        {
            System.Collections.IEnumerator inner = GenerateObjectsForChunkAsync(chunk);
            while (inner.MoveNext())
            {
                yield return inner.Current;
            }

            chunk.isObjectsLoaded = true;
            chunk.objectGenerationRoutine = null;
        }

        private void UnloadChunkObjects(Chunk chunk)
        {
            if (!chunk.isObjectsLoaded && chunk.objectGenerationRoutine == null) return;

            if (chunk.objectGenerationRoutine != null)
            {
                StopCoroutine(chunk.objectGenerationRoutine);
                chunk.objectGenerationRoutine = null;
            }

            DestroyChunkObjects(chunk);
            chunk.isObjectsLoaded = false;
            Debug.Log($"[BiomeManager] 오브젝트 언로드: ({chunk.chunkX}, {chunk.chunkY})");
        }

        /// <summary>
        /// 타일 샘플링 (저장된 변경사항 우선)
        /// </summary>
        protected virtual TileSample SampleTile(int worldX, int worldY, Chunk chunk)
        {
            Vector2Int pos = new Vector2Int(worldX, worldY);
            if (chunk.modifiedTiles.TryGetValue(pos, out BiomeTileType overrideType))
            {
                return CreateTileSample(overrideType);
            }

            return SampleBaseTile(worldX, worldY);
        }

        protected TileSample CreateTileSample(BiomeTileType tileType)
        {
            return new TileSample(tileType, GetTileAsset(tileType), IsTileWalkable(tileType));
        }

        protected abstract TileSample SampleBaseTile(int worldX, int worldY);
        protected abstract TileBase GetTileAsset(BiomeTileType tileType);

        protected virtual bool IsTileWalkable(BiomeTileType tileType)
        {
            return tileType != BiomeTileType.Wall && tileType != BiomeTileType.Obstacle;
        }

        /// <summary>
        /// 청크 저장 (변화만)
        /// </summary>
        protected virtual void SaveChunk(Chunk chunk)
        {
            ChunkSaveData saveData = new ChunkSaveData
            {
                chunkX = chunk.chunkX,
                chunkY = chunk.chunkY,
                seed = seed
            };

            foreach (var kvp in chunk.modifiedObjects)
            {
                ObjectId id = kvp.Key;
                ObjectStateDelta state = kvp.Value;

                ObjectStateData objData = new ObjectStateData
                {
                    worldX = id.x,
                    worldY = id.y,
                    objectType = id.type,
                    isDestroyed = state.isDestroyed,
                    isCollected = state.isCollected
                };
                saveData.objectStates.Add(objData);
            }

            foreach (var kvp in chunk.modifiedTiles)
            {
                Vector2Int pos = kvp.Key;
                BiomeTileType type = kvp.Value;

                TileDeltaData tileData = new TileDeltaData
                {
                    worldX = pos.x,
                    worldY = pos.y,
                    tileType = (int)type
                };
                saveData.tileDeltas.Add(tileData);
            }

            if (saveData.objectStates.Count == 0 && saveData.tileDeltas.Count == 0)
            {
                ChunkSaveSystem.DeleteChunk(biomeType, chunk.chunkX, chunk.chunkY);
                return;
            }

            ChunkSaveSystem.SaveChunk(biomeType, chunk.chunkX, chunk.chunkY, saveData);
        }

        /// <summary>
        /// 청크 오브젝트 파괴
        /// </summary>
        protected virtual void DestroyChunkObjects(Chunk chunk)
        {
            foreach (var state in chunk.objectStates)
            {
                if (state != null)
                {
                    state.SuppressDestroy();
                    if (state.BlocksMovement)
                    {
                        blockedCells.Remove(new Vector2Int(state.ObjectId.x, state.ObjectId.y));
                    }
                    ReleasePooledObject(state.ObjectId.type, state.gameObject);
                }
            }

            chunk.gameObjects.Clear();
            chunk.objectStates.Clear();
        }

        protected void RegisterObject(Chunk chunk, GameObject obj, ObjectId id, bool blocksMovement)
        {
            chunk.gameObjects.Add(obj);

            if (blocksMovement)
            {
                blockedCells.Add(new Vector2Int(id.x, id.y));
            }

            BiomeObjectState state = obj.GetComponent<BiomeObjectState>();
            if (state == null)
            {
                state = obj.AddComponent<BiomeObjectState>();
            }
            state.Initialize(this, new Vector2Int(chunk.chunkX, chunk.chunkY), id, blocksMovement);
            chunk.objectStates.Add(state);
        }

        protected GameObject GetPooledObject(int poolKey, System.Func<GameObject> createFunc)
        {
            if (!objectPool.TryGetValue(poolKey, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                objectPool[poolKey] = stack;
            }

            if (stack.Count > 0)
            {
                GameObject obj = stack.Pop();
                if (obj != null)
                {
                    obj.SetActive(true);
                    Debug.Log($"[BiomePool] 재사용: type={poolKey} name={obj.name}");
                    return obj;
                }
            }

            GameObject created = createFunc();
            Debug.Log($"[BiomePool] 생성: type={poolKey} name={created.name}");
            return created;
        }

        protected void ReleasePooledObject(int poolKey, GameObject obj)
        {
            if (obj == null) return;

            if (!objectPool.TryGetValue(poolKey, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                objectPool[poolKey] = stack;
            }

            obj.SetActive(false);
            obj.transform.SetParent(pooledObjectsParent, false);
            stack.Push(obj);
            Debug.Log($"[BiomePool] 반환: type={poolKey} name={obj.name}");
        }

        internal void NotifyObjectStateChanged(Vector2Int chunkCoord, ObjectId id, bool destroyed, bool collected)
        {
            if (!IsValidChunk(chunkCoord.x, chunkCoord.y)) return;

            Chunk chunk = chunks[chunkCoord.x, chunkCoord.y];
            chunk.modifiedObjects[id] = new ObjectStateDelta(destroyed, collected);

            if (destroyed || collected)
            {
                blockedCells.Remove(new Vector2Int(id.x, id.y));
            }
        }

        protected bool IsObjectSuppressed(Chunk chunk, ObjectId id)
        {
            if (chunk.modifiedObjects.TryGetValue(id, out ObjectStateDelta state))
            {
                return state.isDestroyed || state.isCollected;
            }
            return false;
        }

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

        public int GetHeightLevel(int worldX, int worldY)
        {
            if (!enableHeight) return 0;
            int level = GetBaseHeightLevel(worldX, worldY);
            return Mathf.Clamp(level, minHeightLevel, maxHeightLevel);
        }

        protected virtual int GetBaseHeightLevel(int worldX, int worldY)
        {
            return 0;
        }

        public float GetGroundHeight(int gridX, int gridY)
        {
            return GetHeightLevel(gridX, gridY) * heightStep;
        }

        public float GetGroundHeight(Vector3 worldPos)
        {
            Vector2Int grid = WorldToGrid(worldPos);
            return GetGroundHeight(grid.x, grid.y);
        }

        public Vector3 GridToWorldWithHeight(int gridX, int gridY, float yOffset = 0f)
        {
            Vector3 pos = GridToWorld(gridX, gridY);
            pos.y = GetGroundHeight(gridX, gridY) + yOffset;
            return pos;
        }

        public bool CanMove(Vector3 currentWorldPos, Vector3 desiredWorldPos)
        {
            Vector2Int currentGrid = WorldToGrid(currentWorldPos);
            Vector2Int targetGrid = WorldToGrid(desiredWorldPos);

            if (!IsValidPosition(targetGrid.x, targetGrid.y)) return false;
            if (!IsWalkable(targetGrid.x, targetGrid.y)) return false;

            int currentLevel = GetHeightLevel(currentGrid.x, currentGrid.y);
            int targetLevel = GetHeightLevel(targetGrid.x, targetGrid.y);
            int diff = Mathf.Abs(targetLevel - currentLevel);
            return diff <= maxStepHeight;
        }

        /// <summary>
        /// 이동 가능한지 확인
        /// </summary>
        public bool IsWalkable(int x, int y)
        {
            if (!IsValidPosition(x, y)) return false;
            if (blockedCells.Contains(new Vector2Int(x, y))) return false;

            Vector2Int chunkCoord = GridToChunk(x, y);
            if (IsValidChunk(chunkCoord.x, chunkCoord.y) && chunks != null)
            {
                TileSample sample = SampleTile(x, y, chunks[chunkCoord.x, chunkCoord.y]);
                return sample.walkable;
            }

            return SampleBaseTile(x, y).walkable;
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
                Chunk chunk = chunks[chunkPos.x, chunkPos.y];
                SaveChunk(chunk);
            }

            if (Active == this)
            {
                Active = null;
            }
        }

        private int GetHeightLevelCount()
        {
            return Mathf.Max(1, maxHeightLevel - minHeightLevel + 1);
        }

        private int GetHeightLevelIndex(int heightLevel)
        {
            int clamped = Mathf.Clamp(heightLevel, minHeightLevel, maxHeightLevel);
            return clamped - minHeightLevel;
        }

        private void ClearChunkTilemaps(Chunk chunk)
        {
            if (chunk.tilemaps != null)
            {
                foreach (Tilemap tilemap in chunk.tilemaps)
                {
                    if (tilemap != null)
                    {
                        tilemap.ClearAllTiles();
                    }
                }
            }

            if (chunk.cliffTilemaps != null)
            {
                foreach (Tilemap tilemap in chunk.cliffTilemaps)
                {
                    if (tilemap != null)
                    {
                        tilemap.ClearAllTiles();
                    }
                }
            }
        }
    }

    /// <summary>
    /// 타일 샘플 결과
    /// </summary>
    public struct TileSample
    {
        public BiomeTileType tileType;
        public TileBase tile;
        public bool walkable;

        public TileSample(BiomeTileType tileType, TileBase tile, bool walkable)
        {
            this.tileType = tileType;
            this.tile = tile;
            this.walkable = walkable;
        }
    }

    /// <summary>
    /// 오브젝트 식별자 (결정론)
    /// </summary>
    public struct ObjectId
    {
        public int x;
        public int y;
        public int type;

        public ObjectId(int x, int y, int type)
        {
            this.x = x;
            this.y = y;
            this.type = type;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + type;
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is ObjectId other)
            {
                return x == other.x && y == other.y && type == other.type;
            }
            return false;
        }
    }

    /// <summary>
    /// 오브젝트 변경 상태
    /// </summary>
    public struct ObjectStateDelta
    {
        public bool isDestroyed;
        public bool isCollected;

        public ObjectStateDelta(bool destroyed, bool collected)
        {
            isDestroyed = destroyed;
            isCollected = collected;
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
        public bool isLoaded;
        public bool hasLoadedSave;
        public bool isObjectsLoaded;

        public GameObject root;
        public Tilemap[] tilemaps;
        public TilemapRenderer[] tilemapRenderers;
        public Tilemap[] cliffTilemaps;
        public TilemapRenderer[] cliffTilemapRenderers;

        public Coroutine objectGenerationRoutine;

        public List<GameObject> gameObjects = new List<GameObject>();
        public List<BiomeObjectState> objectStates = new List<BiomeObjectState>();

        public Dictionary<ObjectId, ObjectStateDelta> modifiedObjects = new Dictionary<ObjectId, ObjectStateDelta>();
        public Dictionary<Vector2Int, BiomeTileType> modifiedTiles = new Dictionary<Vector2Int, BiomeTileType>();

        public Chunk(int x, int y, int size)
        {
            chunkX = x;
            chunkY = y;
            this.size = size;
            isLoaded = false;
            hasLoadedSave = false;
            isObjectsLoaded = false;
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
