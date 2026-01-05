# Necrocis 청크 기반 오픈월드 시스템 문서

## 목차
1. [프로젝트 개요](#프로젝트-개요)
2. [시스템 아키텍처](#시스템-아키텍처)
3. [핵심 개념 설명](#핵심-개념-설명)
4. [파일별 코드 설명](#파일별-코드-설명)
5. [사용법](#사용법)
6. [향후 확장](#향후-확장)

---

## 프로젝트 개요

### 목표
고정맵 기반 테스트 환경을 **무한 확장 가능한 청크 기반 오픈월드**로 전환

### 핵심 특징
- 플레이어 주변만 동적 로드/언로드
- 시드 기반 결정론적 지형 생성
- 오브젝트 풀링으로 메모리 최적화
- 게임 상태 영구 저장

---

## 시스템 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                        Unity Scene                           │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │ SaveManager  │◄──►│ ChunkManager │◄──►│ MapGenerator │  │
│  │   (저장)     │    │  (청크관리)   │    │  (노이즈)    │  │
│  └──────────────┘    └──────┬───────┘    └──────────────┘  │
│                             │                                │
│         ┌───────────────────┼───────────────────┐           │
│         ▼                   ▼                   ▼           │
│  ┌────────────┐      ┌────────────┐      ┌────────────┐    │
│  │  Chunk     │      │  Chunk     │      │  Chunk     │    │
│  │  (-1, 0)   │      │  (0, 0)    │      │  (1, 0)    │    │
│  └────────────┘      └────────────┘      └────────────┘    │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │                    Object Pool                        │   │
│  │  [Tile][Tile][Tile][Tile][Tile][Tile][Tile][Tile]... │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### 데이터 흐름

```
플레이어 이동
     │
     ▼
ChunkManager.Update()
     │
     ├─► 현재 청크 좌표 계산
     │
     ├─► 이전 청크와 다르면?
     │         │
     │         ▼
     │   UpdateLoadedChunks()
     │         │
     │         ├─► 범위 밖 청크 언로드
     │         │         │
     │         │         └─► Chunk.Unload()
     │         │                   │
     │         │                   └─► 타일을 풀에 반환
     │         │
     │         └─► 새 청크 로드
     │                   │
     │                   └─► Chunk.Generate()
     │                             │
     │                             ├─► MapGenerator에서 노이즈 조회
     │                             └─► 풀에서 타일 가져와 배치
     │
     └─► 저장 데이터 업데이트
```

---

## 핵심 개념 설명

### 1. 청크 (Chunk)

#### 개념
```
청크란?
- 월드를 일정 크기로 나눈 조각
- 마인크래프트의 청크와 같은 개념
- 각 청크는 독립적으로 로드/언로드 가능
```

#### 왜 필요한가?
```
문제: 120x120 맵을 한번에 생성하면?
- 14,400개 타일 생성 → 로딩 지연
- 14,400개 오브젝트 유지 → 메모리 과다 사용
- 멀리 있는 타일도 렌더링 → GPU 낭비

해결: 청크 분할
- 16x16 = 256개 타일/청크
- 플레이어 주변 5x5 = 25청크만 로드
- 실제 로드되는 타일: 6,400개 (55% 절감)
- 플레이어가 이동하면 동적으로 교체
```

#### 시각화
```
viewDistance = 2 일 때:

     [-2,-2][-1,-2][ 0,-2][ 1,-2][ 2,-2]
     [-2,-1][-1,-1][ 0,-1][ 1,-1][ 2,-1]
     [-2, 0][-1, 0][PLAYER][ 1, 0][ 2, 0]
     [-2, 1][-1, 1][ 0, 1][ 1, 1][ 2, 1]
     [-2, 2][-1, 2][ 0, 2][ 1, 2][ 2, 2]

     총 25개 청크 로드 (5x5)
```

---

### 2. 오브젝트 풀링 (Object Pooling)

#### 개념
```
오브젝트 풀이란?
- 자주 생성/파괴되는 오브젝트를 미리 만들어 재사용
- Destroy() 대신 SetActive(false)로 비활성화
- 필요할 때 SetActive(true)로 재활성화
```

#### 왜 필요한가?
```
문제: 청크 로드/언로드마다 타일 생성/파괴
┌─────────────────────────────────────────────────┐
│ 플레이어가 동쪽으로 이동                          │
│                                                  │
│ 1. 서쪽 청크 5개 언로드 (Destroy 1,280개)        │
│ 2. 동쪽 청크 5개 로드 (Instantiate 1,280개)      │
│ 3. 가비지 컬렉션 발생 → 프레임 드랍!             │
│                                                  │
│ 심각: 청크 이동마다 2,560개 오브젝트 처리         │
└─────────────────────────────────────────────────┘

해결: 오브젝트 풀
┌─────────────────────────────────────────────────┐
│ 1. 서쪽 청크 언로드 → 풀에 반환 (SetActive=false)│
│ 2. 동쪽 청크 로드 → 풀에서 가져옴 (SetActive=true)│
│ 3. 가비지 컬렉션 없음 → 부드러운 프레임!          │
│                                                  │
│ 장점: 메모리 재할당 없음, GC 스파이크 방지        │
└─────────────────────────────────────────────────┘
```

#### Unity의 ObjectPool API
```csharp
// Unity 2021+ 내장 풀링 API
ObjectPool<GameObject> pool = new ObjectPool<GameObject>(
    createFunc: () => CreateNewTile(),      // 새 오브젝트 생성
    actionOnGet: tile => tile.SetActive(true),  // 가져올 때
    actionOnRelease: tile => tile.SetActive(false), // 반환할 때
    actionOnDestroy: tile => Destroy(tile), // 풀 정리 시
    defaultCapacity: 6400,  // 기본 풀 크기
    maxSize: 10000          // 최대 풀 크기
);

// 사용법
GameObject tile = pool.Get();    // 풀에서 가져오기
pool.Release(tile);              // 풀에 반환
```

---

### 3. 절차적 생성 (Procedural Generation)

#### 개념
```
절차적 생성이란?
- 알고리즘으로 콘텐츠를 자동 생성
- 같은 입력(시드) → 항상 같은 출력
- 무한한 월드를 작은 코드로 표현 가능
```

#### FastNoise Lite
```
노이즈(Noise)란?
- 연속적이고 자연스러운 랜덤 값
- 인접한 좌표는 비슷한 값을 가짐
- 지형, 텍스처, 구름 등 자연현상 표현에 적합

종류:
┌────────────────────────────────────────────────┐
│ Perlin Noise                                    │
│ - 부드러운 연속 노이즈                          │
│ - 값 범위: -1 ~ 1                               │
│ - 용도: 고도맵, 지형 변화                       │
├────────────────────────────────────────────────┤
│ Simplex Noise                                   │
│ - Perlin의 개선판 (더 빠르고 균일)              │
│ - 고차원에서 효율적                             │
│ - 본 프로젝트에서 사용                          │
├────────────────────────────────────────────────┤
│ Fractal (FBm)                                   │
│ - 여러 노이즈를 겹침                            │
│ - 자연스러운 디테일 추가                        │
│ - Octaves: 겹치는 레이어 수                     │
└────────────────────────────────────────────────┘
```

#### 노이즈 파라미터
```
Frequency (주파수)
- 패턴의 밀도
- 높을수록: 촘촘한 패턴
- 낮을수록: 넓은 패턴

Octaves (옥타브)
- 디테일 레이어 수
- 높을수록: 더 세밀한 디테일
- 성능 비용 증가

Lacunarity (공백도)
- 옥타브 간 주파수 비율
- 보통 2.0 사용

Gain (게인)
- 옥타브 간 진폭 비율
- 보통 0.5 사용
```

#### 결정론적(Deterministic) 생성
```
왜 중요한가?
┌─────────────────────────────────────────────────┐
│ 시드 = 12345 일 때                               │
│                                                  │
│ 좌표 (100, 50)의 노이즈 값:                     │
│ - 게임 시작 시: 0.342                           │
│ - 1시간 후 재방문: 0.342                        │
│ - 게임 재시작 후: 0.342                         │
│ - 다른 컴퓨터에서: 0.342                        │
│                                                  │
│ → 항상 같은 지형이 생성됨!                      │
└─────────────────────────────────────────────────┘

구현:
float noise = fastNoise.GetNoise(worldX, worldZ);
// 같은 시드 + 같은 좌표 = 항상 같은 값
```

---

### 4. 싱글톤 패턴 (Singleton Pattern)

#### 개념
```
싱글톤이란?
- 클래스의 인스턴스가 단 하나만 존재하도록 보장
- 전역 접근점 제공
```

#### 왜 필요한가?
```
문제: ChunkManager가 여러 개 있으면?
- 어떤 매니저가 청크를 관리하는지 혼란
- 중복 청크 생성
- 메모리 낭비

해결: 싱글톤
- ChunkManager.Instance로 어디서든 접근
- 인스턴스 중복 방지
- 명확한 책임 소재
```

#### 구현
```csharp
public class ChunkManager : MonoBehaviour
{
    public static ChunkManager Instance { get; private set; }

    private void Awake()
    {
        // 이미 인스턴스가 있으면 자신을 파괴
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
}

// 사용
ChunkManager.Instance.RegisterDroppedItem("sword", position);
```

---

### 5. 좌표 시스템

#### 세 가지 좌표 체계
```
┌─────────────────────────────────────────────────────────────┐
│ 1. 월드 좌표 (World Coordinate)                              │
│    - Unity의 Transform.position                              │
│    - 실제 게임 공간에서의 위치                               │
│    - 예: (16.5, 0, 32.0)                                     │
├─────────────────────────────────────────────────────────────┤
│ 2. 청크 좌표 (Chunk Coordinate)                              │
│    - 청크의 식별자                                           │
│    - 월드를 청크 크기로 나눈 값                              │
│    - 예: (1, 2) = 청크 1행 2열                               │
├─────────────────────────────────────────────────────────────┤
│ 3. 로컬 좌표 (Local Coordinate)                              │
│    - 청크 내에서의 상대 위치                                 │
│    - 0 ~ (chunkSize-1) 범위                                  │
│    - 예: (0, 0) ~ (15, 15)                                   │
└─────────────────────────────────────────────────────────────┘
```

#### 좌표 변환 공식
```csharp
// 월드 → 청크
Vector2Int WorldToChunkCoord(Vector3 worldPos)
{
    float chunkWorldSize = chunkSize * tileSize; // 16 * 1 = 16
    int chunkX = Mathf.FloorToInt(worldPos.x / chunkWorldSize);
    int chunkZ = Mathf.FloorToInt(worldPos.z / chunkWorldSize);
    return new Vector2Int(chunkX, chunkZ);
}

// 예시:
// worldPos = (25.0, 0, 40.0)
// chunkWorldSize = 16
// chunkX = Floor(25 / 16) = Floor(1.56) = 1
// chunkZ = Floor(40 / 16) = Floor(2.5) = 2
// 결과: 청크 (1, 2)
```

---

### 6. 저장 시스템

#### 왜 필요한가?
```
문제: 청크 언로드 시 데이터 손실
┌─────────────────────────────────────────────────┐
│ 플레이어가 아이템을 떨어뜨림                     │
│           ↓                                      │
│ 플레이어가 멀리 이동 (청크 언로드)               │
│           ↓                                      │
│ 다시 돌아옴                                      │
│           ↓                                      │
│ 아이템이 사라짐! (데이터 손실)                   │
└─────────────────────────────────────────────────┘

해결: 청크별 저장 데이터
┌─────────────────────────────────────────────────┐
│ 플레이어가 아이템을 떨어뜨림                     │
│           ↓                                      │
│ ChunkSaveData에 아이템 정보 저장                 │
│           ↓                                      │
│ 청크 언로드 (데이터는 메모리에 유지)             │
│           ↓                                      │
│ 다시 돌아옴                                      │
│           ↓                                      │
│ 저장된 데이터로 아이템 복원!                     │
└─────────────────────────────────────────────────┘
```

#### JSON 직렬화
```
왜 JSON인가?
- 사람이 읽을 수 있음 (디버깅 용이)
- Unity의 JsonUtility 지원
- 크로스 플랫폼 호환

주의: Dictionary는 직접 직렬화 불가
→ List로 변환하는 래퍼 클래스 필요
```

---

## 파일별 코드 설명

### 1. MapGenerator.cs

```
역할: 노이즈 기반 지형 데이터 제공

핵심 기능:
├── Initialize() - 노이즈 초기화
├── GetNoiseValue(x, z) - 좌표의 노이즈 값 반환
├── GetTileType(x, z) - 좌표의 타일 타입 결정
└── GetDeterministicSprite() - 결정론적 스프라이트 선택
```

```csharp
// 핵심 로직: 결정론적 스프라이트 선택
public Sprite GetDeterministicSprite(TileType type, int worldX, int worldZ)
{
    Sprite[] sprites = GetSpritesForType(type);

    // 해시 기반 선택 (같은 좌표 = 같은 스프라이트)
    int hash = worldX * 73856093 ^ worldZ * 19349663 ^ seed;
    int index = Mathf.Abs(hash) % sprites.Length;

    return sprites[index];
}

// 왜 이런 숫자들?
// 73856093, 19349663 = 큰 소수
// XOR 연산으로 해시 분포 균일화
// 결과: 시각적으로 랜덤하지만 재현 가능
```

---

### 2. ChunkManager.cs

```
역할: 청크 생명주기 관리, 오브젝트 풀링

핵심 기능:
├── UpdateLoadedChunks() - 청크 로드/언로드 결정
├── LoadChunk() / UnloadChunk() - 청크 생성/해제
├── Object Pool 관리
└── 저장 데이터 관리 API
```

```csharp
// 핵심 로직: 청크 업데이트
private void UpdateLoadedChunks()
{
    // 1. 로드해야 할 청크 계산 (플레이어 중심 정사각형)
    HashSet<Vector2Int> chunksToLoad = new HashSet<Vector2Int>();
    for (int x = -viewDistance; x <= viewDistance; x++)
    {
        for (int z = -viewDistance; z <= viewDistance; z++)
        {
            chunksToLoad.Add(lastPlayerChunkPos + new Vector2Int(x, z));
        }
    }

    // 2. 범위 밖 청크 언로드
    foreach (var chunk in activeChunks)
    {
        if (!chunksToLoad.Contains(chunk.Key))
            UnloadChunk(chunk.Key);
    }

    // 3. 새 청크 로드
    foreach (var coord in chunksToLoad)
    {
        if (!activeChunks.ContainsKey(coord))
            LoadChunk(coord);
    }
}
```

---

### 3. Chunk.cs

```
역할: 개별 청크의 타일 생성/관리

핵심 기능:
├── Generate() - 청크 내 모든 타일 생성
├── Unload() - 타일을 풀에 반환
└── RestoreDroppedItems() - 저장된 아이템 복원
```

```csharp
// 핵심 로직: 타일 생성
public void Generate()
{
    for (int localX = 0; localX < chunkSize; localX++)
    {
        for (int localZ = 0; localZ < chunkSize; localZ++)
        {
            // 로컬 → 월드 좌표 변환
            int worldX = ChunkCoord.x * chunkSize + localX;
            int worldZ = ChunkCoord.y * chunkSize + localZ;

            // 노이즈로 타일 타입 결정
            TileType type = mapGenerator.GetTileType(worldX, worldZ);

            // 풀에서 타일 가져와 배치
            GameObject tile = manager.GetTileFromPool();
            // ... 위치, 스프라이트 설정
        }
    }
}
```

---

### 4. ChunkSaveData.cs

```
역할: 청크별 변경사항 저장

데이터 구조:
├── DroppedItemData - 드롭 아이템 (ID, 위치, 수량)
├── DestroyedObjectData - 파괴된 오브젝트 (로컬좌표, 타입)
├── ChunkSaveData - 청크 단위 데이터
└── WorldSaveData - 전체 월드 데이터
```

```csharp
// 왜 로컬 좌표로 저장?
// 월드 좌표: (1025.5, 0, 2048.3) - 큰 숫자
// 로컬 좌표: (1, 0) - 작은 숫자
// → 저장 용량 절약, 청크 단위 관리 용이
```

---

### 5. SaveManager.cs

```
역할: JSON 파일 저장/로드

핵심 기능:
├── SaveGame() - 현재 상태를 JSON 파일로 저장
├── LoadGame() - JSON 파일에서 상태 복원
├── 자동 저장 (60초 간격)
└── 종료/백그라운드 시 저장
```

```csharp
// Dictionary를 JSON으로 저장하는 트릭
// Unity의 JsonUtility는 Dictionary 미지원

// 해결: 직렬화용 래퍼 클래스
[Serializable]
public class WorldSaveDataSerializable
{
    public List<ChunkSaveDataSerializable> chunks; // List로 변환

    public WorldSaveDataSerializable(WorldSaveData data)
    {
        chunks = new List<ChunkSaveDataSerializable>();
        foreach (var kvp in data.chunks) // Dictionary → List
        {
            chunks.Add(new ChunkSaveDataSerializable(kvp.Value));
        }
    }
}
```

---

## 사용법

### Unity 설정

```
1. 빈 GameObject 생성: "GameManager"
   └── ChunkManager 컴포넌트 추가
   └── SaveManager 컴포넌트 추가

2. MapGenerator 오브젝트 생성
   └── MapGenerator 컴포넌트 추가
   └── 스프라이트 배열 설정

3. ChunkManager Inspector 설정:
   - Player Transform: 플레이어 오브젝트
   - Map Generator: MapGenerator 오브젝트
   - Chunk Size: 16
   - View Distance: 2

4. 플레이어 태그 설정:
   - Player 오브젝트 선택 → Tag → "Player"
```

### 코드 사용 예시

```csharp
// 아이템 드롭
public class Enemy : MonoBehaviour
{
    void OnDeath()
    {
        ChunkManager.Instance.RegisterDroppedItem(
            "gold_coin",
            transform.position,
            Random.Range(1, 10)
        );
    }
}

// 오브젝트 파괴
public class Tree : MonoBehaviour
{
    void OnDestroy()
    {
        ChunkManager.Instance.RegisterDestroyedObject(
            transform.position,
            "tree"
        );
    }
}

// 수동 저장
SaveManager.Instance.SaveGame();

// 새 게임
ChunkManager.Instance.StartNewGame();
SaveManager.Instance.DeleteSaveFile();
```

---

## 향후 확장

### 1. 비트마스킹 AutoTile
```
현재: 모든 벽 타일이 동일
개선: 주변 8칸 체크하여 적절한 가장자리 타일 선택

[128][  1][  2]
[ 64][타일][ 4]
[ 32][ 16][  8]

예: 위(1) + 오른쪽(4) = 5 → 코너 타일
```

### 2. Poisson Disk Sampling
```
현재: 오브젝트 배치 없음
개선: 균일하면서 자연스러운 오브젝트 분포

알고리즘:
1. 첫 점 랜덤 배치
2. 주변에 후보점 생성
3. 최소거리 이상인 후보만 채택
4. 반복
```

### 3. 바이옴별 특성
```
현재: 단일 지형 타입
개선: 바이옴별 다른 타일셋, 몬스터, 환경효과

BiomeData:
- 장: 점액 웅덩이, 기생충
- 간: 독성 연못, 유해가스
- 위: 용암, 위액
- 폐: 바이러스, 모래바람
```

### 4. 비동기 청크 로딩
```
현재: 동기 로딩 (프레임 드랍 가능)
개선: 코루틴/Job System으로 비동기 처리

IEnumerator LoadChunkAsync(Vector2Int coord)
{
    // 프레임 분산 처리
    for (int i = 0; i < tilesPerChunk; i++)
    {
        CreateTile(i);
        if (i % 50 == 0) yield return null; // 50개마다 프레임 양보
    }
}
```

---

## 성능 지표

| 항목 | 값 |
|------|-----|
| 청크 크기 | 16x16 = 256 타일 |
| View Distance | 2 (5x5 = 25 청크) |
| 동시 로드 타일 | 6,400개 |
| 풀 기본 크기 | 6,400 |
| 풀 최대 크기 | 10,000 |
| 자동 저장 간격 | 60초 |

---

## 문제 해결

### Q: 청크 경계에서 지형이 끊어져 보여요
```
A: 노이즈는 월드 좌표 기반이라 자동 연결됨
   확인사항:
   - GetNoiseValue(worldX, worldZ) 사용 중인지
   - 청크 로컬 좌표가 아닌 월드 좌표인지
```

### Q: 프레임 드랍이 발생해요
```
A: 청크 전환 시 발생 가능
   해결:
   - View Distance 줄이기
   - 청크 크기 줄이기
   - 비동기 로딩 구현
```

### Q: 저장이 안 돼요
```
A: 확인사항:
   - SaveManager가 씬에 있는지
   - Player 태그가 설정되어 있는지
   - 저장 경로 권한이 있는지
   - Console에서 에러 메시지 확인
```
