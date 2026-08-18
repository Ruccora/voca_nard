using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    [RequireComponent(typeof(Image))]
    public class SpriteAnimation : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private Sprite[] sprites;
        [SerializeField] private float frameDuration = 0.1f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnAwake = true;

        private float _elapsed;
        private int _currentIndex;
        private bool _isPlaying;
        private int _fixedIndex = -1;

        public int Length => sprites != null ? sprites.Length : 0;
        public float FrameDuration => frameDuration;
        public float TotalDuration => Length * Mathf.Max(0f, frameDuration); // 1周ぶんの秒数
        public bool IsPlaying => _isPlaying && _fixedIndex < 0;
        public int CurrentIndex => _fixedIndex >= 0 ? _fixedIndex : _currentIndex;

        private void Awake()
        {
            if (image == null) image = GetComponent<Image>();
            if (playOnAwake && Length > 0) Play();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            image = GetComponent<Image>();
        }
#endif

        private void Update()
        {
            if (!_isPlaying || _fixedIndex >= 0) return;
            if (Length == 0 || frameDuration <= 0f) return;

            _elapsed += Time.deltaTime;
            while (_elapsed >= frameDuration)
            {
                _elapsed -= frameDuration;
                _currentIndex++;
                if (_currentIndex >= sprites.Length)
                {
                    if (loop)
                    {
                        _currentIndex = 0;
                    }
                    else
                    {
                        _currentIndex = sprites.Length - 1;
                        _isPlaying = false;
                        break;
                    }
                }
            }
            ApplySprite(_currentIndex);
        }

        public void Play()
        {
            if (Length == 0) return;
            _fixedIndex = -1;
            _isPlaying = true;
            _elapsed = 0f;
            _currentIndex = 0;
            ApplySprite(_currentIndex);
        }

        public void Stop()
        {
            _isPlaying = false;
        }

        public void SetFrame(int index)
        {
            if (Length == 0) return;
            _fixedIndex = Mathf.Clamp(index, 0, sprites.Length - 1);
            _isPlaying = false;
            ApplySprite(_fixedIndex);
        }

        public void SetFrameDuration(float seconds)
        {
            frameDuration = Mathf.Max(0f, seconds);
        }

        public void SetSprites(Sprite[] newSprites)
        {
            sprites = newSprites;
            _currentIndex = 0;
            _fixedIndex = -1;
            _elapsed = 0f;
            ApplySprite(0);
        }

        private void ApplySprite(int index)
        {
            if (image == null) return;
            if (sprites == null || index < 0 || index >= sprites.Length) return;
            image.sprite = sprites[index];
        }
    }
}
