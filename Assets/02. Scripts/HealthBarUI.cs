using UnityEngine;
using UnityEngine.UI; // UI (Slider)를 사용하기 위해 꼭 필요합니다!

// 이 스크립트는 Slider 컴포넌트가 있는 곳에만 붙일 수 있습니다.
[RequireComponent(typeof(Slider))]
public class HealthBarUI : MonoBehaviour
{
    private Slider slider;

    void Awake()
    {
        // 이 스크립트가 붙어있는 오브젝트의 Slider 컴포넌트를 가져옴
        slider = GetComponent<Slider>();
    }

    void OnEnable()
    {
        // PlayerController가 "체력 바뀌었다!"(OnHealthChanged)고 방송(Invoke)하면,
        // "UpdateHealthBar" 함수를 실행하도록 구독(등록)합니다.
        PlayerController.OnHealthChanged += UpdateHealthBar;
    }

    void OnDisable()
    {
        // 오브젝트가 꺼질 때는 구독을 해제합니다. (메모리 누수 방지)
        PlayerController.OnHealthChanged -= UpdateHealthBar;
    }

    // 플레이어가 체력 정보를 보내주면 이 함수가 실행됩니다.
    private void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        // 1. 슬라이더의 최대값을 플레이어의 최대 체력으로 설정
        slider.maxValue = maxHealth;
        // 2. 슬라이더의 현재값을 플레이어의 현재 체력으로 설정
        slider.value = currentHealth;
    }
}