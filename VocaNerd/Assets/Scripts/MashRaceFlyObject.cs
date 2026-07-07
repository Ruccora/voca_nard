using UnityEngine;

namespace VocaNerd
{
    [RequireComponent(typeof(RectTransform))]
    public class MashRaceFlyObject : MonoBehaviour
    {
        [SerializeField] private float swayAmplitude = 8f;
        [SerializeField] private float swayFrequency = 1.5f;

        private RectTransform _rt;
        private Vector2 _basePos;
        private bool _swaying;
        private float _swayPhase;

        public float Y => _rt != null ? _rt.anchoredPosition.y : 0f;

        private void Awake()
        {
            _rt = (RectTransform)transform;
        }

        public void Init(Vector2 pos)
        {
            _rt = (RectTransform)transform;
            _rt.anchoredPosition = pos;
            _basePos = pos;
            _swaying = false;
        }

        public void MoveDown(float distance)
        {
            if (_swaying) return;
            var pos = _rt.anchoredPosition;
            pos.y -= distance;
            _rt.anchoredPosition = pos;
            _basePos = pos;
        }

        public void StartSway()
        {
            _basePos = _rt.anchoredPosition;
            _swayPhase = Random.value * Mathf.PI * 2f;
            _swaying = true;
        }

        private void Update()
        {
            if (!_swaying) return;
            _swayPhase += Time.deltaTime * swayFrequency * Mathf.PI * 2f;
            var pos = _rt.anchoredPosition;
            pos.y = _basePos.y + Mathf.Sin(_swayPhase) * swayAmplitude;
            _rt.anchoredPosition = pos;
        }
    }
}
