# Necrocis Enemy System - 핵심 알고리즘 & 구현 정리

> 왜 이 기술이 필요한지, 어떤 원리인지, 실제로 어떻게 적용했는지를 정리한 문서.

---

## 전체 흐름

```
BiomeConfig에 enemySpawnRules 정의
  → ConfigurableBiomeManager가 청크 생성 시 Poisson Disk Sampling으로 스포너 위치 결정
    → EnemySpawner 오브젝트 생성 (청크 오브젝트로 관리)
      → 플레이어가 activationRadius 안에 들어오면 EnemyController 생성 (풀링)
        → FSM 시작 (Idle 상태)
          → 배회/추격/복귀/공격/사망 상태 전환 (Separation 적용)
            → 청크 언로드 또는 사망 시 풀로 반환
```

---

## 파일 구조

```
Assets/02.Scripts/
├── Enemy/
│   ├── EnemySpawner.cs              ← 스포너 (플레이어 근처에서 적 활성화/리스폰)
│   ├── EnemyController.cs           ← 적 AI (FSM + 풀링 + Separation)
│   └── FSM/
│       ├── IEnemyState.cs           ← 상태 인터페이스 (Enter/Update/Exit)
│       ├── EnemyIdleState.cs        ← 대기 → 타임아웃시 Wander, 감지시 Chase
│       ├── EnemyWanderState.cs      ← 배회 → 감지시 Chase, leash 이탈시 Return
│       ├── EnemyChaseState.cs       ← 추격 → 사정거리 진입시 Attack, leash 이탈시 Return
│       ├── EnemyReturnState.cs      ← 앵커 복귀 → 도착시 Idle
│       ├── EnemyAttackState.cs      ← 공격 → 범위 이탈시 Chase, 체력 0시 Dead
│       └── EnemyDeadState.cs        ← 사망 → 풀로 반환
└── Biome/
    └── BiomeConfig.cs               ← EnemySpawnRuleConfig 정의
```

---
---

## 1. Instance Pooling (인스턴스 풀링)

### 왜 필요한가

게임에서 몹은 청크가 로드될 때 생성되고, 언로드될 때 제거된다.
Unity에서 `Instantiate()`와 `Destroy()`는 생각보다 무거운 작업이다.

몹 10마리가 동시에 사라지고 생성될 때 내부적으로 일어나는 일:
1. 힙 메모리 새로 할당 (Heap allocation)
2. 모든 컴포넌트 초기화 (Awake, Start 호출)
3. 사라진 오브젝트의 메모리는 GC(Garbage Collector)가 수거

문제는 **GC가 수거할 때 게임이 잠깐 멈춘다**는 것이다.
청크 경계를 왔다갔다 하거나, 넓은 맵을 빠르게 이동하면 이 생성/파괴가 반복되면서
프레임 드랍이 주기적으로 발생한다. 이걸 **GC 스파이크**라고 부른다.

---

### 핵심 아이디어

> "오브젝트를 삭제하지 말고, 꺼두었다가 필요할 때 다시 켜라."

```
[풀에 없을 때]                    [풀에 있을 때]
Instantiate() → 새로 생성  vs.   Pool에서 꺼내서 활성화
Destroy() → 메모리 해제    vs.   Pool에 반납하고 비활성화
```

비유하자면 **렌터카 회사**와 같다.
차가 필요할 때마다 공장에서 새로 만들고 쓰고 폐차하는 게 아니라,
차고에 차를 모아두고 빌려주고 반납받는 방식이다.

---

### 구조

```
__EnemyPool (씬에 항상 존재하는 루트 오브젝트)
  ├── [비활성] Enemy_Macrophage 인스턴스
  ├── [비활성] Enemy_Macrophage 인스턴스
  ├── [비활성] Enemy_NKCell 인스턴스
  └── ...

Dictionary<int, Stack<EnemyController>>
  archetype_hash_A → [인스턴스A, 인스턴스B]
  archetype_hash_B → [인스턴스C]
```

- **Dictionary**: 몹 종류(archetype)별로 풀을 나눔
- **Stack**: 가장 최근에 반납된 것을 먼저 꺼냄 (캐시 친화적)
- **int 키**: `EnemySpawnRuleConfig`의 이름과 salt로 해시 생성

---

### 흐름

```
스폰 요청
  → 풀에 해당 종류 인스턴스 있음?
      YES → 꺼내서 위치/상태 초기화 후 활성화
      NO  → new GameObject + AddComponent (최초 1회만)

제거 요청 (사망, 청크 언로드, 플레이어 이탈)
  → Destroy() 대신 비활성화 후 풀에 반납
```

---

### Necrocis에서의 적용

| 호출 시점 | 동작 |
|-----------|------|
| 플레이어 접근, 리스폰 | `EnemyController.Acquire(parent, name, archetypeId)` |
| 플레이어 이탈, 청크 언로드 | `enemy.ReleaseToPool()` |
| 몹 사망 (Dead 상태) | `enemy.ReleaseToPool()` |

---
---

## 2. FSM (Finite State Machine, 유한 상태 기계)

### 왜 필요한가

FSM 없이 몹 AI를 짜면 보통 이렇게 된다:

```csharp
void Update()
{
    if (플레이어 감지 && 공격 범위 밖)
        Chase();
    else if (공격 범위 안)
        Attack();
    else if (체력 0)
        Die();
    else
        Wander();

    // 나중에 복귀 상태 추가하면?
    // 피격 경직 추가하면?
    // 특수 패턴 추가하면?
    // → if/else가 계속 쌓이고, 조건 조합이 기하급수적으로 늘어남
}
```

상태가 늘어날수록 조건 분기가 엉키고, 어느 조건에서 어느 동작을 하는지 추적하기 어려워진다.
이런 구조를 **스파게티 코드**라고 부른다.

---

### 핵심 아이디어

> "몹은 항상 단 하나의 상태에만 존재한다.
> 각 상태는 Enter/Update/Exit으로 완전히 독립되어 있다.
> 상태 전환 조건만 명확히 정의하면 된다."

```
[Idle] ──감지──→ [Chase] ──사정거리──→ [Attack]
                    ↑                      |
                    └──────────────────────┘ (이탈)
                    ↓
               [Return] ←── leash 이탈
                    ↓
                 [Idle]
```

---

### IEnemyState 인터페이스

```
Enter()  : 이 상태로 진입할 때 딱 1번 실행
           예) Chase 진입 → 이동 애니메이션 전환

Update() : 매 프레임 실행
           예) 플레이어와 거리 계산, 상태 전환 조건 검사, 이동

Exit()   : 이 상태에서 나갈 때 딱 1번 실행
           예) 타이머 리셋, 애니메이션 정리
```

**왜 Enter/Update/Exit을 나누는가**

한 상태 안에서 "처음 한 번", "계속", "마지막 한 번" 처리를 분리해서
상태 진입/이탈 시 발생하는 부수효과(side effect)를 명확히 관리하기 위해서다.
예를 들어 Attack 상태에서 나갈 때 공격 판정을 확실히 끊어야 하는 경우,
Exit에 딱 한 줄만 써두면 된다.

---

### static readonly 인스턴스

```csharp
public static readonly EnemyIdleState Instance = new EnemyIdleState();
```

모든 몹이 같은 상태 인스턴스를 공유한다.
몹이 100마리라도 `EnemyChaseState` 객체는 딱 1개만 존재한다.
상태 클래스 자체는 데이터를 들고 있지 않고, `EnemyController enemy` 인자로 받아서 처리하기 때문에 가능하다.
→ `new`를 100번 하지 않아도 되므로 GC 부담 없음.

---

### Necrocis FSM 전체 상태 전환

```
[Idle]
  대기 타임아웃 → [Wander]
  감지범위 진입 → [Chase]

[Wander]
  감지범위 진입 → [Chase]
  leash 이탈   → [Return]
  목적지 도착   → [Idle]

[Chase]
  leashRadius 이탈  → [Return]
  감지범위 이탈      → [Wander]
  attackRange 진입  → [Attack]
  체력 0            → [Dead]

[Return]
  앵커 도착 → [Idle]

[Attack]
  범위 이탈 → [Chase]
  체력 0   → [Dead]

[Dead]
  → ReleaseToPool()
```

### 왜 BT(Behavior Tree) 대신 FSM인가

BT가 표현력은 더 높지만, Necrocis 잡몹 수준에서는 과하다.
잡몹은 단순하고 빠르게 많이 돌아가야 하니까 FSM이 훨씬 적합하다.
BT는 나중에 보스몹처럼 복잡한 패턴이 필요할 때 쓸 예정.

---
---

## 3. Separation (분리 조향)

### 왜 필요한가

여러 몹이 같은 플레이어를 목표로 이동하면 물리적으로 겹치지 않더라도
같은 지점을 향해 몰려오면서 **한 점에 겹쳐 서는 현상**이 발생한다.

Unity의 Collider가 있어도 이 문제가 완전히 해결되지 않는 이유:
- Rigidbody.MovePosition은 물리 충돌을 완전히 해결하지 않는다
- 몹들이 서로 밀어내는 힘보다 플레이어를 향하는 힘이 강하면 Collider가 파고든다
- 결과적으로 몹이 겹쳐 보이거나, 한 점에서 진동하는 현상이 생긴다

---

### 핵심 아이디어

> "주변에 너무 가까운 개체가 있으면, 그 반대 방향으로 살짝 밀어내는 힘을 이동 벡터에 더한다."

이건 Craig Reynolds가 1987년에 발표한 **Boids 알고리즘**의 세 가지 규칙 중 하나다.
(나머지 둘은 Alignment=같은 방향 정렬, Cohesion=무리 중심으로 뭉치기)

Necrocis에서는 몹들이 무리 지어 다니는 게 아니라
각자 플레이어를 쫓거나 배회하는 구조이므로 Separation만 사용한다.

---

### 계산 방식

```
나 (●) 주변 separationDistance 안에 있는 모든 몹에 대해:

  밀어내는 방향 = (내 위치 - 상대 위치).normalized
  밀어내는 강도 = 1 - (거리 / separationDistance)   (가까울수록 강하게)

  separation 벡터 += 밀어내는 방향 × 강도

최종 이동 방향 = (목표 방향 + separation 벡터 × separationStrength).normalized
```

시각화:
```
       ← sep      목표 →
  [몹B]    [나●]   →→→
              ↑
           sep 합산 결과: 오른쪽 + 위쪽으로 약간 치우친 방향으로 이동
```

---

### 거리에 반비례하는 이유

단순히 normalized만 쓰면 1cm 거리와 90cm 거리나 밀어내는 힘이 같아진다.
`1 - (거리 / separationDistance)`를 곱하면 **가까울수록 강하게, 멀수록 약하게** 밀어내서 자연스럽다.

---

### Necrocis 파라미터

| 파라미터 | 의미 | 값이 크면 |
|----------|------|-----------|
| `separationDistance` | 이 거리 안에 들어온 몹만 계산 | 더 넓게 퍼짐 |
| `separationStrength` | separation 벡터의 최종 배율 | 더 강하게 밀어냄 (너무 크면 튕겨나감) |

---

### 성능 주의점

현재 구현은 **활성 몹 전체를 순회**한다.
몹이 N마리면 각 몹이 N-1번 계산 → **O(N²)** 복잡도.

몹 수가 적을 때(현재 Necrocis 수준)는 문제없지만,
나중에 몹이 많아지면 `Physics.OverlapSphere`로 주변 몹만 먼저 걸러내서 순회 비용을 줄일 수 있다.

---
---

## 4. Poisson Disk Sampling (포아송 디스크 샘플링)

### 왜 필요한가

청크 내 몹 스폰 위치를 순수 랜덤으로 뽑으면 이런 문제가 생긴다:

```
순수 랜덤:           포아송 분포:
● ●●                 ●   ●
    ●                  ●
●●●   ●             ●    ●
  ●                   ●   ●
```

랜덤은 운이 나쁘면 한쪽에 몰리고 다른 쪽은 텅 빈다.
게임에서 이건 **특정 구역에서만 몹이 잔뜩 나오는** 이상한 경험을 만든다.

---

### 포아송 디스크 샘플링이란

통계에서 포아송 분포는 "단위 시간/공간에 사건이 발생하는 횟수"를 모델링한다.
게임 스폰에서 사용하는 건 정확히는 **포아송 디스크 샘플링(Poisson Disk Sampling)** 이다.

> "샘플 간 최소 거리를 보장하면서, 공간을 균등하게 채운다."

핵심 성질:
- 샘플 간 최소 거리 `minDistance` 보장 → 너무 가까이 붙지 않음
- 공간 전체를 비교적 고르게 채움 → 너무 편향되지 않음
- 완전 격자(grid)가 아니라서 인위적으로 보이지 않음 → 자연스러움

---

### Bridson's Algorithm (구현에 사용한 방법)

1995년에 나온 단순하고 효율적인 포아송 디스크 샘플링 알고리즘이다.

```
1. 첫 씨앗 포인트를 청크 중심 근처에서 뽑아 활성 목록에 추가

2. 활성 목록이 빌 때까지 반복:
   a. 활성 목록에서 포인트 p를 하나 꺼냄
   b. p 주변 minDist ~ 2*minDist 사이 환형 영역에서 후보 30개 시도
   c. 후보가 조건 통과(청크 안 + 걸을 수 있음 + 기존 샘플과 minDist 이상)하면 추가
   d. 30번 모두 실패하면 p를 활성 목록에서 제거 (이 포인트 주변은 더 못 채움)

3. 최종 샘플 목록 = 스포너 배치 위치
```

환형 영역(minDist ~ 2*minDist)을 쓰는 이유:
- minDist 안쪽은 이미 너무 가까워서 선택 불가
- 2*minDist 바깥은 너무 멀어서 공간을 낭비 없이 채우기 어려움
- 그 사이 영역이 가장 효율적으로 공간을 메울 수 있는 범위

---

### 포아송 vs 순수 랜덤 vs 격자 비교

| 방식 | 균등함 | 자연스러움 | 구현 복잡도 |
|------|--------|-----------|------------|
| 순수 랜덤 | 낮음 (뭉침) | 높음 | 매우 쉬움 |
| 격자(Grid) | 매우 높음 | 낮음 (인위적) | 쉬움 |
| 포아송 디스크 | 높음 | 높음 | 중간 |

게임 맵 생성, 식물 배치, 별자리, 점묘화 등 "자연스럽게 퍼뜨려야 하는 모든 곳"에 쓰인다.

---

### Necrocis에서의 적용

```
ConfigurableBiomeManager.BuildSpawnManifest()
  → EnemySpawnRuleConfig마다 Poisson 샘플링
    → 통과한 위치에 ChunkSpawnRecord(category=EnemySpawner) 기록
      → 청크 오브젝트 로드 시 EnemySpawner 생성
```

`density`를 크게 하면 스포너가 촘촘하게, `minDistance`를 크게 하면 성기게 퍼진다.
적 종류별로 다른 density/minDistance를 설정할 수 있어서, 강한 적은 드물게 배치 가능.

---
---

## 요약

| 알고리즘 | 해결하는 문제 | 핵심 아이디어 |
|----------|--------------|--------------|
| **Instance Pooling** | Instantiate/Destroy 반복으로 인한 GC 스파이크 | 껐다 켜기, 메모리 재사용 |
| **FSM** | 상태 분기가 복잡해지는 스파게티 코드 | 상태를 클래스로 분리, 전환 조건만 관리 |
| **Separation** | 몹이 한 점에 몰리는 현상 | 가까운 개체를 거리에 반비례해서 밀어냄 |
| **Poisson Disk Sampling** | 스폰 위치가 편향되는 현상 | 최소 거리를 보장하면서 공간을 균등하게 채움 |

---
---

## EnemySpawnRuleConfig 전체 파라미터

BiomeConfig ScriptableObject의 `Enemy Spawn Rules` 리스트에서 설정.

### Poisson (스포너 배치)
| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| density | 0.0025 | 스포너 배치 밀도 |
| minDistance | 8 | 스포너 간 최소 거리 |
| poissonSalt | 400 | 결정적 해시 salt |
| allowedRegions | [] | 허용 지역 (비면 전체) |

### Spawner (스포너 동작)
| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| maxAlive | 1 | 동시 최대 마릿수 |
| activationRadius | 20 | 활성화 거리 |
| respawnCooldown | 8 | 리스폰 대기(초) |
| spawnRadius | 1.5 | 스폰 반경 |

### Movement (이동)
| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| moveSpeed | 1.5 | 이동 속도 |
| wanderRadius | 4 | 배회 범위 |
| chaseRadius | 6 | 추격 시작 거리 |
| leashRadius | 8 | 앵커 최대 이탈 거리 |
| idleDelayRange | (0.5, 1.5) | 대기 시간 범위 |

### Combat (전투)
| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| maxHealth | 30 | 최대 체력 |
| attackDamage | 10 | 공격력 |
| attackRange | 1.5 | 공격 사거리 |
| attackCooldown | 1 | 공격 쿨다운(초) |

### Separation (분리)
| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| separationDistance | 1.1 | 밀어내기 감지 거리 |
| separationStrength | 1 | 밀어내기 강도 |

---

## 씬 배치

```
Hub.unity        → 몹 없음 (안전지대)
Intestine.unity  → BiomeConfig에 enemySpawnRules 설정
Liver.unity      → 바이옴별 다른 적 종류/밀도 가능
Stomach.unity    → 〃
Lung.unity       → 〃
```

코드 수정 없이 BiomeConfig Inspector에서 적 추가/제거/밸런싱 가능.

---

## TODO

- [ ] 플레이어 데미지 연동 (PlayerController.TakeDamage)
- [ ] 피격 이펙트 / 넉백
- [ ] 아이템 드롭 시스템
- [ ] 스프라이트 방향 전환 (상하좌우)
- [ ] 공격/피격/사망 애니메이션
- [ ] 바이옴별 전용 적 스프라이트/스탯
- [ ] 보스몹 (BT 기반, 별도 시스템)
