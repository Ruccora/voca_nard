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
        [SerializeField] private Button backButton;
        [SerializeField] private RectTransform gameContainer;

        [Header("Animated Rects")]
        [SerializeField] private RectTransform backButtonRect;

        public RectTransform BackButtonRect => backButtonRect;

        private string _bgmKey;
        private GameObject _spawned;
        private PanelBase _innerPanel;
        private InputAction _backAction;

        protected override void Awake()
        {
            base.Awake();
            backButton.onClick.AddListener(OnBack);

            // ゲームパッドの B / select はここでは拾わない。ミニゲームのリザルトで
            // 「1P の B で抜ける」を各ゲーム側 (ResultInput) が持っていて、二重に効いてしまうため。
            // ここに残すのは開発用のキーボード Back だけ。
            _backAction = new InputAction("Back", InputActionType.Button);
            _backAction.AddBinding("<Keyboard>/escape");
            _backAction.AddBinding("<Keyboard>/backspace");
            _backAction.performed += _ => OnBack();
        }

        /// <summary>
        /// 起動するミニゲームを差し込む。呼び出し元は説明画面 (<see cref="ExplainPanelBase"/>)。
        /// </summary>
        public void Bind(GameObject miniGamePrefab, string bgmKey)
        {
            _bgmKey = bgmKey;

            if (_spawned != null)
            {
                Destroy(_spawned);
                _spawned = null;
                _innerPanel = null;
            }
            if (miniGamePrefab != null && gameContainer != null)
            {
                _spawned = Instantiate(miniGamePrefab, gameContainer);
                _innerPanel = _spawned.GetComponent<PanelBase>();
            }
        }

        public override async UniTask SetupAsync(CancellationToken token)
        {
            await base.SetupAsync(token);

            // BGM はミニゲームごとに説明画面 prefab で決める（ScreenController の MiniGame 枠は空にしておく）
            Audio.PlayBgm(_bgmKey);

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
