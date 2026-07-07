using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class PanelBase : MonoBehaviour
    {
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected float fadeDuration = 0.25f;

        public bool IsAnimating { get; private set; }

        protected virtual void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            SetInteractable(false);
        }

        public virtual UniTask SetupAsync(CancellationToken token) => UniTask.CompletedTask;

        public async UniTask PanelInAsync(CancellationToken token)
        {
            IsAnimating = true;
            SetInteractable(false);
            try
            {
                await OnPanelInAsync(token);
            }
            finally
            {
                IsAnimating = false;
                if (this != null) SetInteractable(true);
            }
        }

        public async UniTask PanelOutAsync(CancellationToken token)
        {
            IsAnimating = true;
            SetInteractable(false);
            try
            {
                await OnPanelOutAsync(token);
            }
            finally
            {
                IsAnimating = false;
            }
        }

        protected virtual async UniTask OnPanelInAsync(CancellationToken token)
        {
            canvasGroup.alpha = 0f;
            await FadeAsync(canvasGroup, 0f, 1f, fadeDuration, token);
        }

        protected virtual async UniTask OnPanelOutAsync(CancellationToken token)
        {
            await FadeAsync(canvasGroup, canvasGroup.alpha, 0f, fadeDuration, token);
        }

        protected void SetInteractable(bool value)
        {
            if (canvasGroup == null) return;
            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        private static async UniTask FadeAsync(CanvasGroup group, float from, float to, float duration, CancellationToken token)
        {
            if (duration <= 0f)
            {
                group.alpha = to;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            group.alpha = to;
        }
    }
}
