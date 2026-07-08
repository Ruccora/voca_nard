using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CanvasGroupBlinker : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
#endif

        public async UniTask BlinkAsync(float duration, CancellationToken cancellationToken = default)
        {
            if (canvasGroup == null) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cts.Token;

            try
            {
                var elapsed = 0f;
                var toggle = false;
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    canvasGroup.alpha = toggle ? 0f : 1f;
                    toggle = !toggle;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    elapsed += Time.unscaledDeltaTime;
                }
                canvasGroup.alpha = 1f;
            }
            catch (OperationCanceledException) { }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
