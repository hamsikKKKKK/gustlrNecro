using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.125f; // 카메라가 타겟에 도달하는데 걸리는 시간 /값이 낮을수록 빠름
    private Vector3 velocity = Vector3.zero; // SmoothDamp 내부 계산
    
    public Vector3 offset = new Vector3(0, 0, -10);
    public float rotationAngle = 55f;

    void Start()
    {
        transform.rotation = Quaternion.Euler(rotationAngle, 0, 0);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        // Vector3.Lerp 대신 SmoothDamp 사용 
        transform.position = Vector3.SmoothDamp(
            transform.position,   // 현재 위치
            desiredPosition,      // 목표 위치
            ref velocity,         // 현재 속도
            smoothTime            // 목표까지 가는데 걸리는 시간
        );

        transform.rotation = Quaternion.Euler(rotationAngle, 0, 0);
    }
}