using UnityEngine;

namespace Necrocis
{
    [CreateAssetMenu(fileName = "AnimationSet", menuName = "Necrocis/Animation Set")]
    public class AnimationSet : ScriptableObject
    {
        [SerializeField] private Sprite[] idleSprites;
        [SerializeField] private Sprite[] walkDownSprites;
        [SerializeField] private Sprite[] walkUpSprites;
        [SerializeField] private Sprite[] walkLeftSprites;
        [SerializeField] private Sprite[] walkRightSprites;

        public Sprite[] IdleSprites => idleSprites;
        public Sprite[] WalkDownSprites => walkDownSprites;
        public Sprite[] WalkUpSprites => walkUpSprites;
        public Sprite[] WalkLeftSprites => walkLeftSprites;
        public Sprite[] WalkRightSprites => walkRightSprites;
    }
}