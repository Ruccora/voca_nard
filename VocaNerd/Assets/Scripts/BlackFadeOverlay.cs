using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    [RequireComponent(typeof(CanvasGroup))]
    public class BlackFadeOverlay : MonoBehaviour
    {
        public static BlackFadeOverlay Instance { get; private set; }

        [SerializeField] private CanvasGroup canvasGroup;

        private CancellationTokenSource _cts;

        public float CurrentAlpha => canvasGroup != null ? canvasGroup.alpha : 0f;
        public bool IsBlocking => canvasGroup != null && canvasGroup.blocksRaycasts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public async UniTask FadeInAsync(float duration, CancellationToken cancellationToken = default)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;
            await FadeAsync(canvasGroup.alpha, 1f, duration, cancellationToken);
        }

        public async UniTask FadeOutAsync(float duration, CancellationToken cancellationToken = default)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            await FadeAsync(canvasGroup.alpha, 0f, duration, cancellationToken);
        }

        public void SetImmediate(float alpha, bool blockInput)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.blocksRaycasts = blockInput;
            canvasGroup.interactable = false;
        }

        private async UniTask FadeAsync(float from, float to, float duration, CancellationToken external)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(external);
            var token = _cts.Token;

            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            canvasGroup.alpha = to;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (Instance == this) Instance = null;
        }
    }
}
