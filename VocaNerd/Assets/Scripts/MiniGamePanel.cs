using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private InputAction _backAction;

        protected override void Awake()
        {
            base.Awake();
            backButton.onClick.AddListener(OnBack);

            _backAction = new InputAction("Back", InputActionType.Button);
            _backAction.AddBinding("<Keyboard>/escape");
            _backAction.AddBinding("<Keyboard>/backspace");
            _backAction.AddBinding("<Gamepad>/buttonEast");
            _backAction.AddBinding("<Gamepad>/select");
            _backAction.performed += _ => OnBack();
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

            // BGM はミニゲームごとに MiniGameData で決める（ScreenController の MiniGame 枠は空にしておく）
            if (_current != null) Audio.PlayBgm(_current.BgmKey);

            if (_innerPanel != null) await _innerPanel.SetupAsync(token);
        }

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            _backAction?.Enable();
            await base.OnPanelInAsync(token);
            if (_innerPanel != null) await _innerPanel.PanelInAsync(token);
        }

        protected override async UniTask OnPanelOutAsync(CancellationToken token)
        {
            _backAction?.Disable();
            if (_innerPanel != null) await _innerPanel.PanelOutAsync(token);
            await base.OnPanelOutAsync(token);
        }

        private void OnDestroy()
        {
            _backAction?.Dispose();
            _backAction = null;
        }

        private void OnBack()
        {
            if (IsAnimating) return;
            if (_innerPanel != null && !_innerPanel.CanAcceptBack) return;
            ScreenController.Instance.ShowAsync(ScreenType.Select).Forget();
        }
    }
}
