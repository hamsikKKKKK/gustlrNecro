# Necrocis 최적화 및 바이옴 확장 구조 정리

## 개요
이번 작업의 목적은 두 가지였다.
1) **성능/메모리 최적화가 실제 코드에서 안정적으로 동작**하도록 구조를 다듬는 것  
2) **바이옴을 데이터(설정)만 바꿔서 확장**할 수 있도록 설계를 일반화하는 것

아래 내용은 **현재 코드 기준으로 적용되어 있는 최적화**와, **범용 바이옴 구조로 확장한 부분**을 상세히 정리한 것이다.

---

## 1. 최적화 정리 (현 코드 기준)

### 1) 청크 업데이트 컬렉션 재사용
- **파일**: `Assets/02.Scripts/Biome/BiomeManager.cs`
- **내용**: `chunksToLoadCache`, `chunksToUnloadCache`, `objectsToLoadCache`를 필드로 두고 `Clear()`로 재사용.
- **효과**: 프레임마다 컬렉션 할당을 줄여 **GC 스파이크 감소**.

### 2) 청크 타일/버퍼 재사용
- **파일**: `Assets/02.Scripts/Biome/BiomeManager.cs`
- **내용**: `Chunk` 내부에 `baseTiles`, `heightLevels`, `cliffLevels`, `tileBuffer`를 한번 생성 후 재사용.  
  `useCliffOverlayTilemaps` 설정에 따라 `cliffBuffer` 또는 `colorBuffer`를 유지/재사용.
- **효과**: 청크 로드/언로드 반복 시 **배열 재할당 감소**.
- **메모리 영향**: 청크 수가 많으면 버퍼가 메모리에 남아 있어 **총 메모리 증가 가능성**.

### 3) 청크 루트 풀링
- **파일**: `Assets/02.Scripts/Biome/BiomeManager.cs`
- **내용**: `useChunkRootPooling`, `chunkRootPool`, `maxChunkRootPoolSize`로 Tilemap 루트 GameObject 재사용.
- **효과**: GameObject 생성/파괴 비용 감소 → **프레임 스파이크 완화**.

### 4) 오브젝트 풀 상한 (타입별 + 전체 상한)
- **파일**: `Assets/02.Scripts/Biome/BiomeManager.cs`
- **변경점**:
  - 타입별 상한: `PoolLimit` + `defaultMaxPoolSizePerType`
  - 전체 상한: `maxTotalPoolSize` + `pooledObjectCount`
- **효과**: 탐험 길이에 비례해 풀 메모리가 무한 증가하는 문제를 제어.
- **주의**: 상한을 너무 낮게 잡으면 Instantiate/Destroy가 늘어 CPU 스파이크 가능.

### 5) 풀 키 enum화 (타입 안정화)
- **파일**: `Assets/02.Scripts/Biome/BiomeManager.cs`
- **변경점**: 풀 키를 `int` → `BiomeObjectKind` enum으로 전환.
- **효과**: 타입 실수로 인한 풀 폭발/메모리 누수를 방지, 인스펙터 설정도 안정화.

### 6) 지역/높이 캐시를 청크 단위로 축소
- **파일**: `Assets/02.Scripts/Biome/RegionPoissonBiomeManager.cs`
- **변경점**:
  - 과거: 맵 전체 `int[,]`/`bool[,]` 캐시  
  - 현재: **청크 단위 캐시(`ChunkCache`)** + **언로드 시 삭제**
- **효과**:
  - 맵이 커져도 캐시 메모리가 **로드된 청크 범위로 제한**
  - 바이옴 이동/청크 언로드 시 캐시 해제 가능

### 7) 포아송 배치 후보 방식
- **파일**: `Assets/02.Scripts/Biome/RegionPoissonBiomeManager.cs`
- **방식 요약**:
  1) 셀 단위로 후보 1개를 결정 (hash)
  2) 현재 좌표가 후보일 때만 검사 진행
  3) 주변 셀 후보만 거리 체크
- **효과**: 전수 검사 대신 **지역 후보만 확인** → 연산량 감소.

### 8) 오브젝트 생성 예산 분산
- **파일**: `Assets/02.Scripts/Biome/RegionPoissonBiomeManager.cs`
- **내용**: `objectGenerationBudget` 기반으로 처리량을 나누고, 최소 16 단위로 `yield`하며 프레임 예산 분산.
- **효과**: 대량 배치 시 프레임 드랍 감소.

---

## 2. 바이옴 확장 구조 (범용화)

### 1) 공통 베이스: RegionPoissonBiomeManager
- **파일**: `Assets/02.Scripts/Biome/RegionPoissonBiomeManager.cs`
- **역할**:
  - Voronoi 기반 지역 샘플링
  - 높이 계산 + 노이즈 처리
  - Poisson 배치
  - 청크 단위 캐시 관리
- **확장 포인트**:
  - `BuildObjectRules()`
  - `SpawnObject()`
  - `GetRegionHeight()`
  - `OnAfterObjectsGenerated()`

### 2) 데이터 기반 설정: BiomeConfig
- **파일**: `Assets/02.Scripts/Biome/BiomeConfig.cs`
- **구성 요소**:
  - `regions`: 지역별 기본/변형 타일 + 높이 + 변형 임계값
  - `regionCellSize`, `regionBlendWidth`, `detailNoiseScale`
  - `heightNoiseScale`, `heightNoiseAmplitude`
  - `tileMappings`: `BiomeTileType` → `TileBase` 매핑
  - `objectRules`: 밀도/거리/스프라이트(결정론 선택)/애니메이션/콜라이더/빌보드/YSort/정렬/높이 오프셋
  - `returnPortal`: 활성화 여부/커스텀 위치/스프라이트/스케일/콜라이더
  - `margin`: 스폰 허용 영역 패딩

### 3) 범용 실행기: ConfigurableBiomeManager
- **파일**: `Assets/02.Scripts/Biome/ConfigurableBiomeManager.cs`
- **특징**:
  - BiomeConfig를 읽어 모든 생성 규칙을 구성
  - 스프라이트 선택/애니메이션/콜라이더/빌보드/YSort/포털 생성 자동 처리
  - 새로운 바이옴은 **Config만 추가하면 즉시 적용 가능**

### 4) 씬 적용 현황
- **파일**: `Assets/01. Scenes/Intestine.unity`, `Assets/01. Scenes/Stomach.unity`, `Assets/01. Scenes/Lung.unity`, `Assets/01. Scenes/Liver.unity`
- **변경점**:
  - 모두 `ConfigurableBiomeManager` 사용
  - `Intestine.unity`만 `config`가 `Assets/13. Biome config/IntestineBiomeConfig.asset`에 연결
  - `Stomach/Lung/Liver`는 `config`가 비어 있음

### 5) 바이옴 설정 에셋
- **파일**: `Assets/13. Biome config/IntestineBiomeConfig.asset`
- **내용**:
  - 기존 장 바이옴 타일/노이즈/포아송/오브젝트/포털 설정 이식
  - 이후 다른 바이옴은 이 에셋을 복제하여 값만 수정하면 됨
  - 추가 에셋: `Assets/13. Biome config/StomachBiomeConfig.asset`, `Assets/13. Biome config/LungBiomeConfig.asset`, `Assets/13. Biome config/LiverBiomeConfig.asset`

---

## 3. 새 바이옴을 추가하는 방법
1) `Assets/13. Biome config`에서 **BiomeConfig 에셋 생성**
2) `regions`에 지역 정의(타일/높이/변형 기준)
3) `tileMappings`에 `BiomeTileType` 매핑 등록
4) `objectRules`에 스폰 규칙 추가 (스프라이트/밀도/거리/콜라이더/애니메이션 등)
5) 씬에 `ConfigurableBiomeManager` 추가 → `config` 연결 (비어 있으면 Awake에서 비활성화됨)
6) `PoolLimit`에서 카테고리별 풀 상한 재설정

---

## 4. 주의사항 / 후속 체크
- `PoolLimit`이 enum 카테고리로 바뀌면서 기존 값이 초기화될 수 있음.
- 서로 다른 오브젝트라도 같은 `BiomeObjectKind`를 쓰면 풀 공유됨  
  → 재사용 시 컴포넌트 설정이 매번 덮어쓰이므로 문제는 적지만,  
    특수 컴포넌트가 필요한 경우 규칙에서 명시적으로 추가해야 함.
- 포털 위치가 `gridPosition`으로 고정인 경우 맵 크기 변경 시 조정 필요.
- 스프라이트 배열이 비어 있으면 해당 룰은 스폰되지 않음.
- `ConfigurableBiomeManager`는 `config`가 없거나 `regions`가 비어 있으면 자동 비활성화됨.

---

## 5. 변경 요약 (경로)
- 공통 베이스: `Assets/02.Scripts/Biome/RegionPoissonBiomeManager.cs`
- 범용 매니저: `Assets/02.Scripts/Biome/ConfigurableBiomeManager.cs`
- 설정 데이터: `Assets/02.Scripts/Biome/BiomeConfig.cs`
- 바이옴 설정: `Assets/13. Biome config/IntestineBiomeConfig.asset`
- 씬: `Assets/01. Scenes/Intestine.unity`, `Assets/01. Scenes/Stomach.unity`, `Assets/01. Scenes/Lung.unity`, `Assets/01. Scenes/Liver.unity`
