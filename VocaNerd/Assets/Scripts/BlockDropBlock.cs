using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    [RequireComponent(typeof(RectTransform))]
    public class BlockDropBlock : MonoBehaviour
    {
        public enum BlockType
        {
            Normal,
            StickLeft,
            StickRight,
        }

        [SerializeField] private Image body;
        [SerializeField] private GameObject leftStick;
        [SerializeField] private GameObject rightStick;

        [SerializeField] private Color normalColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] private Color stickColor = new Color(0.9f, 0.75f, 0.2f);

        private RectTransform _rt;
        public RectTransform Rect => _rt != null ? _rt : (_rt = (RectTransform)transform);
        public BlockType Type { get; private set; }

        public void Setup(BlockType type)
        {
            Type = type;
            if (leftStick != null) leftStick.SetActive(type == BlockType.StickLeft);
            if (rightStick != null) rightStick.SetActive(type == BlockType.StickRight);
            if (body != null) body.color = type == BlockType.Normal ? normalColor : stickColor;
        }

        public async UniTask FlyAwayAsync(bool toRight, float distance, float duration, CancellationToken token)
        {
            var start = Rect.anchoredPosition;
            var end = new Vector2(start.x + (toRight ? distance : -distance), start.y);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 2f);
                Rect.anchoredPosition = Vector2.Lerp(start, end, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            Rect.anchoredPosition = end;
        }

        // 次の段が落ちてくる演出は1フレームで即着地させる
        public async UniTask DropAsync(float dropAmount, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var pos = Rect.anchoredPosition;
            Rect.anchoredPosition = new Vector2(pos.x, pos.y - dropAmount);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }
}
