using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 스프라이트가 항상 카메라를 향하게 (2.5D 빌보드)
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Header("빌보드 모드")]
        [SerializeField] private BillboardMode mode = BillboardMode.FaceCamera;

        [Header("오프셋")]
        [SerializeField] private float yOffset = 0.5f;  // 바닥에서 띄우기

        [Header("최적화")]
        [SerializeField] private bool updateOnlyWhenDirty = true;
        [SerializeField] private UpdateMode updateMode = UpdateMode.Once;

        public enum BillboardMode
        {
            FaceCamera,         // 카메라를 완전히 바라봄
            FaceCameraYOnly,    // Y축 회전만 (직립 유지)
            FixedRotation       // 고정 회전 (카메라 각도만 따라감)
        }

        public enum UpdateMode
        {
            Continuous,
            Once
        }

        private Camera mainCamera;
        private Quaternion lastCameraRotation;
        private Vector3 lastCameraPosition;
        private Vector3 lastPosition;
        private bool hasUpdated;

        private void Start()
        {
            mainCamera = Camera.main;

            // Y 오프셋 적용
            Vector3 pos = transform.localPosition;
            pos.y += yOffset;
            transform.localPosition = pos;
            lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            if (updateMode == UpdateMode.Once && hasUpdated)
            {
                enabled = false;
                return;
            }

            if (updateOnlyWhenDirty && hasUpdated && !IsDirty())
            {
                return;
            }

            switch (mode)
            {
                case BillboardMode.FaceCamera:
                    // 카메라를 완전히 바라봄 (돈스타브 스타일)
                    transform.rotation = mainCamera.transform.rotation;
                    break;

                case BillboardMode.FaceCameraYOnly:
                    // Y축 회전만 (직립 유지, 좌우로만 회전)
                    Vector3 dirToCamera = mainCamera.transform.position - transform.position;
                    dirToCamera.y = 0;
                    if (dirToCamera != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(-dirToCamera);
                    }
                    break;

                case BillboardMode.FixedRotation:
                    // 카메라 X 각도만 따라감 (스프라이트가 기울어짐)
                    float cameraAngleX = mainCamera.transform.eulerAngles.x;
                    transform.rotation = Quaternion.Euler(cameraAngleX, 0, 0);
                    break;
            }

            lastCameraRotation = mainCamera.transform.rotation;
            lastCameraPosition = mainCamera.transform.position;
            lastPosition = transform.position;
            hasUpdated = true;

            if (updateMode == UpdateMode.Once)
            {
                enabled = false;
            }
        }

        private bool IsDirty()
        {
            if (mode == BillboardMode.FaceCamera || mode == BillboardMode.FixedRotation)
            {
                return mainCamera.transform.rotation != lastCameraRotation;
            }

            if (mainCamera.transform.position != lastCameraPosition)
            {
                return true;
            }

            return transform.position != lastPosition;
        }

        public void SetUpdateMode(UpdateMode mode)
        {
            updateMode = mode;
            enabled = true;
            hasUpdated = false;
        }
    }
}
