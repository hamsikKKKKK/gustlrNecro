# Necrocis - 기획 및 구현 문서

## 1. 프로젝트 개요

**Necrocis**는 Unity 기반 2.5D 로그라이크 게임입니다.
- **테마**: 인체 내부 (장, 간, 위, 폐 바이옴)
- **스타일**: 돈스타브(Don't Starve) 스타일 2.5D 탑다운 뷰
- **핵심 루프**: Hub에서 4개 바이옴 탐험 → 보스 부산물 수집 → 재단에서 최종 보스 진입

---

## 2. 게임 흐름 (단계별)

```
┌─────────────────────────────────────────────────────────────────┐
│                        [게임 시작]                              │
│                            ↓                                    │
│                   ┌───────────────┐                             │
│                   │   Hub (중간방) │ ← 게임의 중심                │
│                   └───────────────┘                             │
│                    ↙   ↓   ↓   ↘                                │
│              ┌────┐┌────┐┌────┐┌────┐                           │
│              │ 장 ││ 간 ││ 위 ││ 폐 │  ← 4개 바이옴              │
│              └────┘└────┘└────┘└────┘                           │
│                 ↓     ↓     ↓     ↓                             │
│             [보스 처치 → 부산물 획득]                            │
│                         ↓                                       │
│              [4개 부산물 수집 완료]                              │
│                         ↓                                       │
│               ┌─────────────────┐                               │
│               │  재단 활성화     │                               │
│               └─────────────────┘                               │
│                         ↓                                       │
│               ┌─────────────────┐                               │
│               │ 대뇌 맵 (최종보스)│                              │
│               └─────────────────┘                               │
└─────────────────────────────────────────────────────────────────┘
```

### 단계 1: Hub (중간방)
- 플레이어 시작 위치
- 4개 바이옴 포털 배치 (상단에 나란히)
- 중앙에 재단 배치
- 벽 콜라이더로 맵 경계 설정

### 단계 2: 바이옴 탐험
- 포털 진입 시 해당 바이옴 씬으로 전환
- 청크 기반 오픈월드 맵 생성
- 귀환 포털을 통해 Hub로 복귀 가능

### 단계 3: 보스 처치 & 부산물 수집
- 각 바이옴 보스 처치 시 부산물 획득
- GameManager에서 수집 현황 추적

### 단계 4: 재단 활성화
- 4개 부산물 수집 시 재단 활성화
- 재단 상호작용으로 대뇌 맵 진입

---

## 3. 핵심 시스템 구현

### 3.1 Core 시스템

#### GameManager.cs
**역할**: 게임 전체 상태 관리 (싱글톤)

| 기능 | 설명 |
|------|------|
| `SetGameState()` | 게임 상태 변경 (InHub, InBiome, InBossRoom, InFinalBoss) |
| `EnterBiome()` | 바이옴 진입 처리, 진입 횟수 증가 |
| `ReturnToHub()` | Hub로 귀환 |
| `CollectRelic()` | 부산물 획득 |
| `EnterFinalBoss()` | 최종 보스 맵 진입 |

```csharp
// 상태 관리
public enum GameState { InHub, InBiome, InBossRoom, InFinalBoss }

// 부산물 수집 추적
private bool hasIntestineRelic, hasLiverRelic, hasStomachRelic, hasLungRelic;

// 바이옴 진입 횟수 (몬스터 강화용)
private int intestineEntryCount, liverEntryCount, stomachEntryCount, lungEntryCount;
```

#### BiomeType.cs
**역할**: 바이옴 타입 및 데이터 정의

| 바이옴 | 맵 크기 | 취약 클래스 | 디버프 |
|--------|---------|-------------|--------|
| 장 (Intestine) | 120x120 | 없음 | 없음 |
| 간 (Liver) | 150x150 | 마법사 | 방어력 10% 감소 |
| 위 (Stomach) | 90x90 | 궁수 | 방어력 5% 감소 |
| 폐 (Lung) | 135x135 | 전사 | 피격시 넉백 |

#### TileType.cs
**역할**: 타일 종류 및 속성 정의

| 타일 | 이동 가능 | 이동 속도 | 초당 데미지 |
|------|-----------|-----------|-------------|
| Floor | O | 1.0 | 0 |
| Wall | X | 0 | 0 |
| Water | O | 0.7 | 0 |
| Lava | O | 0.5 | 2 |
| Acid | O | 0.8 | 1 |
| Slime | O | 0.8 | 0 |

#### SceneLoader.cs
**역할**: 씬 전환 관리 (싱글톤)

```csharp
// 씬 이름 상수
SCENE_HUB = "Hub"
SCENE_INTESTINE = "Biome"
SCENE_LIVER = "Biome_Liver"
// ...

// 주요 메서드
LoadBiome(BiomeType biome)  // 바이옴 씬 로드
ReturnToHub()               // Hub 씬으로 복귀
```

#### GameInitializer.cs
**역할**: 게임 초기화 (플레이어, 카메라 생성)

```
초기화 순서:
1. GameManager 확인/생성
2. HubRoom 찾기
3. 플레이어 생성 (프리팹 또는 기본)
4. 카메라 생성 및 타겟 설정
```

---

### 3.2 Hub 시스템

#### HubRoom.cs
**역할**: 중간방 관리자 (싱글톤)

```
생성 순서:
1. 오브젝트 부모 생성
2. HubMapRenderer 초기화
3. 벽 콜라이더 생성 (북/남/동/서)
4. 포털 4개 생성 (장/간/위/폐)
5. 재단 생성 (중앙)
```

| 메서드 | 설명 |
|--------|------|
| `GenerateHub()` | Hub 전체 생성 |
| `SetupWallColliders()` | 보이지 않는 벽 생성 |
| `SetupPortals()` | 4개 포털 생성 |
| `SetupAltar()` | 재단 생성 |
| `GetPlayerSpawnPosition()` | 플레이어 스폰 위치 반환 |

#### HubMapRenderer.cs
**역할**: 통이미지 배경 렌더링 + 충돌 영역 관리

```csharp
// 맵 설정
mapWidth = 30, mapHeight = 30, tileSize = 1f

// 배경 설정
- 3D Quad 사용 (스프라이트 정렬 충돌 방지)
- Y = -0.01에 배치 (바닥)
- Unlit/Texture 셰이더 사용

// 이동 가능 영역
- 가장자리 2칸은 벽 (이동 불가)
- walkableMap[,] 배열로 관리
```

#### Portal.cs
**역할**: 바이옴 진입 포털

```
구성 요소:
- SpriteRenderer (포털 이미지)
- Billboard (2.5D 효과)
- SpriteYSort (Y 기반 정렬)
- BoxCollider (트리거)

진입 로직:
1. OnTriggerEnter 감지
2. 게임 시작 1초 이내면 무시
3. Player 태그 확인
4. GameManager.EnterBiome() 호출
5. SceneLoader.LoadBiome() 호출
```

#### Altar.cs
**역할**: 재단 시스템 (4개 부산물 바침 → 대뇌 진입)

```
상태:
- 비활성화: 부산물 부족
- 준비 완료: 4개 수집됨 (노란색)
- 활성화: 바침 완료 (마젠타색)

상호작용:
1. 부산물 4개 미만 → "부족합니다" 메시지
2. 부산물 4개 보유 → 재단 활성화
3. 활성화 상태 → 대뇌 맵 진입
```

---

### 3.3 Player & Camera 시스템

#### PlayerController.cs
**역할**: 플레이어 이동 + 방향별 스프라이트 애니메이션

```
입력 처리:
- Unity New Input System 사용
- WASD / 방향키 지원
- 포커스 없거나 게임 시작 0.5초 이내면 무시

방향:
enum Direction { Down, Up, Left, Right }

애니메이션:
- 대기: idleSprites[] (4fps)
- 이동: walkDownSprites[], walkUpSprites[] 등 (8fps)

이동 방식:
1. CharacterController (있으면)
2. Rigidbody.MovePosition (있으면)
3. transform.position 직접 변경
```

#### DontStarveCamera.cs
**역할**: 돈스타브 스타일 2.5D 카메라

```
설정:
- Orthographic 모드 (기본)
- height = 10, distance = 5, angle = 45도
- smoothSpeed = 5 (부드러운 추적)

줌:
- 마우스 스크롤로 조절
- minZoom = 3, maxZoom = 10
- Orthographic: orthographicSize 조절
- Perspective: height 조절
```

#### Billboard.cs
**역할**: 스프라이트가 항상 카메라를 향하게

```
모드:
- FaceCamera: 카메라를 완전히 바라봄 (돈스타브 스타일)
- FaceCameraYOnly: Y축 회전만 (직립 유지)
- FixedRotation: 카메라 X 각도만 따라감
```

#### SpriteYSort.cs
**역할**: Y 위치에 따른 스프라이트 정렬

```csharp
// 정렬 로직 (Z가 작을수록 카메라에 가까움)
float sortValue = -transform.position.z * sortingMultiplier;
spriteRenderer.sortingOrder = baseSortingOrder + Mathf.RoundToInt(sortValue);
```

---

### 3.4 Biome 시스템

#### BiomeManager.cs (추상 클래스)
**역할**: 청크 기반 오픈월드 맵 생성 기본 클래스

```
청크 시스템:
- chunkSize = 16 (기본)
- loadDistance = 2 (플레이어 주변 로드)
- unloadDistance = 3 (언로드 거리)
- chunkUpdateInterval = 0.5초

청크 라이프사이클:
1. 플레이어 위치 변경 감지
2. 로드 범위 내 청크 확인
3. 저장된 데이터 있으면 RestoreChunk()
4. 없으면 GenerateChunk() (추상)
5. 언로드 범위 벗어나면 SaveChunk() 후 제거
```

```csharp
// 하위 클래스에서 구현 필수
protected abstract void GenerateChunk(int chunkX, int chunkY);
protected abstract void PlaceTile(int x, int y, BiomeTile tile, Chunk chunk);
protected abstract void RestoreObject(int x, int y, ObjectSaveData objData, Chunk chunk);
protected abstract string GetTileSpriteKey(BiomeTile tile);
protected abstract Sprite GetSpriteByKey(string key);
```

#### IntestineBiomeManager.cs
**역할**: 장(Intestine) 바이옴 구현

```
맵 크기: 90x90
청크 크기: 16x16

타일:
- mudTile1: 진흙바닥재 (기본)
- mudTile2: 진흙타일맵 (구멍 패턴)
- mossTile: 이끼바닥재 (특정 구역)

오브젝트 밀도:
- slimePuddle: 3%
- moldPlant: 5%
- rock: 2%
- moldTree: 1.5%
- parasite: 1% (6프레임 애니메이션)
- item: 0.5%

노이즈 생성:
- Perlin 노이즈로 타일 배치
- mudNoiseThreshold = 0.4
- mossNoiseThreshold = 0.7
```

#### ChunkSaveSystem.cs
**역할**: 청크 저장/로드 시스템

```
저장 경로: Application.persistentDataPath/ChunkData/{BiomeType}/

파일 형식: JSON
파일명: chunk_{x}_{y}.json

저장 데이터:
- ChunkSaveData: 청크 메타데이터
- TileSaveData: 타일 정보 (localX, localY, tileType, spriteKey)
- ObjectSaveData: 오브젝트 정보 (localX, localY, objectType, isDestroyed, isCollected)
```

#### AnimatedSprite.cs
**역할**: 스프라이트 애니메이션 (프레임 기반)

```csharp
// 설정
Sprite[] frames;
float frameRate = 0.15f;  // 프레임 간격
bool loop = true;

// 메서드
SetFrames(Sprite[] newFrames, float newFrameRate)
Play()
Stop()
Pause()
```

#### ReturnPortal.cs
**역할**: 허브로 돌아가는 귀환 포털

```
특징:
- activationDelay = 1초 (씬 로드 직후 오작동 방지)
- OnTriggerEnter로 Player 감지
- GameManager.ReturnToHub() + SceneLoader.ReturnToHub() 호출
```

---

## 4. 오브젝트 구조도

```
[Hierarchy 구조]

GameManager (DontDestroyOnLoad)
├── 게임 상태 관리
├── 부산물 수집 추적
└── 바이옴 진입 횟수

SceneLoader (DontDestroyOnLoad)
└── 씬 전환 관리

Player (DontDestroyOnLoad)
├── PlayerController
├── Rigidbody / CharacterController
├── CapsuleCollider
└── Sprite (자식)
    ├── SpriteRenderer
    ├── Billboard
    └── SpriteYSort

MainCamera (DontDestroyOnLoad)
├── Camera
└── DontStarveCamera

=== Hub 씬 ===

HubRoom
├── HubMapRenderer
│   └── MapBackground (Quad)
└── HubObjects
    ├── WallColliders
    │   ├── Wall_North
    │   ├── Wall_South
    │   ├── Wall_East
    │   └── Wall_West
    ├── Portal_장
    │   ├── SpriteRenderer
    │   ├── Billboard
    │   ├── SpriteYSort
    │   ├── BoxCollider (Trigger)
    │   └── Portal
    ├── Portal_간 (동일 구조)
    ├── Portal_위 (동일 구조)
    ├── Portal_폐 (동일 구조)
    └── Altar
        ├── SpriteRenderer
        ├── Billboard
        ├── BoxCollider (Trigger)
        └── Altar

=== Biome 씬 ===

IntestineBiomeManager
├── Tiles (타일 부모)
│   └── Tile_x_y (MeshFilter + MeshRenderer)
└── Objects (오브젝트 부모)
    ├── SlimePuddle_x_y
    ├── MoldPlant_x_y
    ├── Rock_x_y (BoxCollider 포함)
    ├── MoldTree_x_y (BoxCollider 포함)
    ├── Parasite_x_y (AnimatedSprite)
    ├── Item_x_y (BoxCollider Trigger)
    └── ReturnPortal
```

---

## 5. 데이터 흐름

### 5.1 씬 전환 흐름

```
[Hub에서 바이옴 진입]

Player → Portal (OnTriggerEnter)
       → Portal.TryEnterPortal()
       → Portal.EnterBiome()
       → GameManager.EnterBiome(biomeType)
          ├── currentBiome 설정
          ├── 진입 횟수 증가
          └── OnBiomeEntered 이벤트
       → SceneLoader.LoadBiome(biomeType)
       → SceneManager.LoadSceneAsync()
       → BiomeManager.Start() (새 씬)
          ├── Initialize()
          ├── SetupPlayer() (스폰 위치 이동)
          ├── SetupCamera()
          └── UpdateChunks() (초기 청크 로드)
```

```
[바이옴에서 Hub 귀환]

Player → ReturnPortal (OnTriggerEnter)
       → ReturnPortal.ReturnToHub()
       → GameManager.ReturnToHub()
          ├── currentBiome = None
          └── SetGameState(InHub)
       → SceneLoader.ReturnToHub()
       → SceneManager.LoadSceneAsync("Hub")
       → HubRoom.Start() (새 씬)
          └── GenerateHub()
```

### 5.2 청크 로드/언로드 흐름

```
Update() → chunkUpdateTimer 체크
         → UpdateChunks()

UpdateChunks():
1. 플레이어 청크 위치 계산
2. 로드 범위 내 청크 목록 생성
3. 언로드 대상 청크 찾기 (unloadDistance 초과)
4. UnloadChunk() 실행
   ├── SaveChunk() → JSON 저장
   └── DestroyChunkObjects()
5. LoadChunk() 실행
   ├── ChunkSaveSystem.LoadChunk() 시도
   ├── 저장 데이터 있으면 RestoreChunk()
   └── 없으면 GenerateChunk()
```

---

## 6. 주요 설정값

### 6.1 Hub 설정
| 항목 | 값 |
|------|-----|
| 맵 크기 | 30 x 30 |
| 타일 크기 | 1.0 |
| 벽 두께 | 2 타일 |
| 포털 스케일 | 0.5 |
| 재단 스케일 | 0.5 |

### 6.2 카메라 설정
| 항목 | 값 |
|------|-----|
| Orthographic Size | 5 |
| Height | 10 |
| Distance | 5 |
| Angle | 45도 |
| Smooth Speed | 5 |
| Zoom 범위 | 3 ~ 10 |

### 6.3 플레이어 설정
| 항목 | 값 |
|------|-----|
| 이동 속도 | 5 |
| 대기 애니메이션 FPS | 4 |
| 이동 애니메이션 FPS | 8 |
| Rigidbody Drag | 10 |

### 6.4 장 바이옴 설정
| 항목 | 값 |
|------|-----|
| 맵 크기 | 90 x 90 |
| 청크 크기 | 16 x 16 |
| 청크 로드 거리 | 2 청크 |
| 청크 언로드 거리 | 3 청크 |
| 청크 갱신 간격 | 0.5초 |

---

## 7. 확장 포인트

### 7.1 새 바이옴 추가
1. `BiomeManager`를 상속받는 새 클래스 생성
2. `GenerateChunk()`, `PlaceTile()` 등 추상 메서드 구현
3. 바이옴별 스프라이트 및 오브젝트 정의
4. `SceneLoader`에 씬 이름 추가
5. 새 씬 생성 및 Build Settings 등록

### 7.2 보스 시스템 추가
1. `GameManager.CollectRelic()` 호출하여 부산물 획득
2. 보스룸 씬 또는 영역 구현
3. `GameState.InBossRoom` 상태 활용

### 7.3 아이템 시스템 추가
1. `ObjectSaveData.isCollected` 활용
2. 아이템 픽업 로직 구현
3. 인벤토리 시스템 연동

---

## 8. 네임스페이스

모든 스크립트는 `Necrocis` 네임스페이스 사용:

```csharp
namespace Necrocis
{
    // 클래스 정의
}
```

---

## 9. 파일 구조

```
Assets/
└── 02.Scripts/
    ├── Core/
    │   ├── GameManager.cs
    │   ├── GameInitializer.cs
    │   ├── BiomeType.cs
    │   ├── TileType.cs
    │   └── SceneLoader.cs
    ├── Hub/
    │   ├── HubRoom.cs
    │   ├── HubTileGenerator.cs
    │   ├── HubMapRenderer.cs
    │   ├── Portal.cs
    │   └── Altar.cs
    ├── Player/
    │   └── PlayerController.cs
    ├── Camera/
    │   ├── DontStarveCamera.cs
    │   ├── Billboard.cs
    │   └── SpriteYSort.cs
    ├── Biome/
    │   ├── BiomeManager.cs
    │   ├── IntestineBiomeManager.cs
    │   ├── ChunkSaveSystem.cs
    │   ├── AnimatedSprite.cs
    │   └── ReturnPortal.cs
    └── PerlinNoise.cs (외부 라이브러리)
```
