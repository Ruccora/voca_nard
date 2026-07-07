using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    public enum ScreenType
    {
        Title,
        Select,
        MiniGame,
    }

    public class ScreenController : MonoBehaviour
    {
        [Serializable]
        private class ScreenEntry
        {
            public ScreenType type;
            public GameObject prefab;
        }

        [SerializeField] private ScreenEntry[] screens;
        [SerializeField] private RectTransform root;
        [SerializeField] private float blackFadeDuration = 0.3f;

        private GameObject _current;
        private CancellationTokenSource _transitionCts;

        public static ScreenController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ShowAsync(ScreenType.Title).Forget();
        }

        public async UniTask ShowAsync(ScreenType next, Action<GameObject> onInstantiated = null, CancellationToken cancellationToken = default)
        {
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            _transitionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _transitionCts.Token;

            var hasCurrent = _current != null;

            // 1) 現Panel の退出演出（ユーザーに見える）
            if (hasCurrent)
            {
                var outPanel = _current.GetComponent<PanelBase>();
                if (outPanel != null)
                {
                    try { await outPanel.PanelOutAsync(token); }
                    catch (OperationCanceledException) { }
                }

                // 2) 遷移イン演出（黒フェード）
                await TransitionInAsync(token);

                // 3) 破棄（黒画面裏）
                Destroy(_current);
                _current = null;
            }

            // 4) 新Panel 生成 + Bind + Setup（黒画面裏 or 初回はそのまま）
            var prefab = FindPrefab(next);
            if (prefab == null) throw new InvalidOperationException($"Prefab not registered for screen: {next}");

            var instance = Instantiate(prefab, root != null ? root : (RectTransform)transform);
            _current = instance;

            onInstantiated?.Invoke(instance);

            var inPanel = instance.GetComponent<PanelBase>();
            if (inPanel != null)
            {
                await inPanel.SetupAsync(token);
            }

            // 5) 遷移アウト演出（黒フェード解除）
            if (hasCurrent)
            {
                await TransitionOutAsync(token);
            }

            // 6) 新Panel の登場演出（ユーザーに見える）
            if (inPanel != null)
            {
                await inPanel.PanelInAsync(token);
            }
        }

        // 遷移演出をここに集約。差し替え時はこの2メソッドを触るだけ。
        protected virtual async UniTask TransitionInAsync(CancellationToken token)
        {
            if (BlackFadeOverlay.Instance == null) return;
            await BlackFadeOverlay.Instance.FadeInAsync(blackFadeDuration, token);
        }

        protected virtual async UniTask TransitionOutAsync(CancellationToken token)
        {
            if (BlackFadeOverlay.Instance == null) return;
            await BlackFadeOverlay.Instance.FadeOutAsync(blackFadeDuration, token);
        }

        private GameObject FindPrefab(ScreenType type)
        {
            foreach (var entry in screens)
            {
                if (entry.type == type) return entry.prefab;
            }
            return null;
        }

        private void OnDestroy()
        {
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
