using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    public class MiniGamePanel : PanelBase
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button backButton;
        [SerializeField] private RectTransform gameContainer;

        [Header("Animated Rects")]
        [SerializeField] private RectTransform titleTextRect;
        [SerializeField] private RectTransform backButtonRect;

        public RectTransform TitleTextRect => titleTextRect;
        public RectTransform BackButtonRect => backButtonRect;

        private MiniGameData _current;
        private GameObject _spawned;
        private PanelBase _innerPanel;

        protected override void Awake()
        {
            base.Awake();
            backButton.onClick.AddListener(OnBack);
        }

        public void Bind(MiniGameData data)
        {
            _current = data;
            if (titleText != null) titleText.text = data.Title;

            if (_spawned != null)
            {
                Destroy(_spawned);
                _spawned = null;
                _innerPanel = null;
            }
            if (data.MiniGamePrefab != null && gameContainer != null)
            {
                _spawned = Instantiate(data.MiniGamePrefab, gameContainer);
                _innerPanel = _spawned.GetComponent<PanelBase>();
            }
        }

        public override async UniTask SetupAsync(CancellationToken token)
        {
            await base.SetupAsync(token);
            if (_innerPanel != null) await _innerPanel.SetupAsync(token);
        }

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            await base.OnPanelInAsync(token);
            if (_innerPanel != null) await _innerPanel.PanelInAsync(token);
        }

        protected override async UniTask OnPanelOutAsync(CancellationToken token)
        {
            if (_innerPanel != null) await _innerPanel.PanelOutAsync(token);
            await base.OnPanelOutAsync(token);
        }

        private void OnBack()
        {
            if (IsAnimating) return;
            ScreenController.Instance.ShowAsync(ScreenType.Select).Forget();
        }
    }
}
