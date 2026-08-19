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

            [Tooltip("この画面へ遷移したときに流す BGM キー（BgmKey の定数）。空なら BGM を触らない")]
            public string bgmKey;
        }

        [SerializeField] private ScreenEntry[] screens;
        [SerializeField] private RectTransform root;
        [SerializeField] private float bgmFadeDuration = 0.8f;

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

            var outgoing = _current;
            var outPanel = outgoing != null ? outgoing.GetComponent<PanelBase>() : null;

            // 1) 新Panel 生成 (旧 Panel はまだ生きている)
            var entry = FindEntry(next);
            var prefab = entry != null ? entry.prefab : null;
            if (prefab == null) throw new InvalidOperationException($"Prefab not registered for screen: {next}");
            var instance = Instantiate(prefab, root != null ? root : (RectTransform)transform);
            _current = instance;

            onInstantiated?.Invoke(instance);

            // BGM は遷移演出と並行してクロスフェードさせる（待たない）。
            // 空キーなら据え置き = Panel 側 (MiniGamePanel など) が自分で決める。
            Audio.PlayBgm(entry.bgmKey, bgmFadeDuration);

            var inPanel = instance.GetComponent<PanelBase>();
            if (inPanel != null)
            {
                await inPanel.SetupAsync(token);
            }

            // 2) 旧 Panel の Out 前フック
            try
            {
                if (outPanel != null) await outPanel.PanelPreOutAsync(token);
            }
            catch (OperationCanceledException) { }

            // 3) Out と In を並列実行（クロスフェード/クロススライド）
            try
            {
                await UniTask.WhenAll(
                    outPanel != null ? outPanel.PanelOutAsync(token) : UniTask.CompletedTask,
                    inPanel != null ? inPanel.PanelInAsync(token) : UniTask.CompletedTask
                );
            }
            catch (OperationCanceledException) { }

            // 4) 旧 Panel を破棄
            if (outgoing != null) Destroy(outgoing);
        }

        private ScreenEntry FindEntry(ScreenType type)
        {
            foreach (var entry in screens)
            {
                if (entry.type == type) return entry;
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
