using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 스프라이트 애니메이션 (프레임 기반)
    /// </summary>
    public class AnimatedSprite : MonoBehaviour
    {
        [Header("애니메이션 설정")]
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float frameRate = 0.15f;  // 프레임 간격 (초)
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnStart = true;

        private SpriteRenderer spriteRenderer;
        private int currentFrame = 0;
        private float timer = 0f;
        private bool isPlaying = false;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Start()
        {
            if (playOnStart && frames != null && frames.Length > 0)
            {
                Play();
            }
        }

        private void Update()
        {
            if (!isPlaying || frames == null || frames.Length == 0) return;

            timer += Time.deltaTime;

            if (timer >= frameRate)
            {
                timer -= frameRate;
                currentFrame++;

                if (currentFrame >= frames.Length)
                {
                    if (loop)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        currentFrame = frames.Length - 1;
                        isPlaying = false;
                        return;
                    }
                }

                if (spriteRenderer != null && currentFrame < frames.Length)
                {
                    spriteRenderer.sprite = frames[currentFrame];
                }
            }
        }

        /// <summary>
        /// 애니메이션 프레임 설정
        /// </summary>
        public void SetFrames(Sprite[] newFrames, float newFrameRate = 0.15f)
        {
            frames = newFrames;
            frameRate = newFrameRate;
            currentFrame = 0;
            timer = 0f;

            if (spriteRenderer != null && frames != null && frames.Length > 0)
            {
                spriteRenderer.sprite = frames[0];
            }
        }

        /// <summary>
        /// 재생
        /// </summary>
        public void Play()
        {
            isPlaying = true;
            currentFrame = 0;
            timer = 0f;

            if (spriteRenderer != null && frames != null && frames.Length > 0)
            {
                spriteRenderer.sprite = frames[0];
            }
        }

        /// <summary>
        /// 정지
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
        }

        /// <summary>
        /// 일시정지/재개
        /// </summary>
        public void Pause()
        {
            isPlaying = !isPlaying;
        }
    }
}
