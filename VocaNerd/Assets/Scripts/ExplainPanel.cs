using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace VocaNerd
{
    public class ExplainPanel : PanelBase
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoDisplay;
        [SerializeField] private Button playButton;
        [SerializeField] private Button backButton;

        private MiniGameData _current;

        protected override void Awake()
        {
            base.Awake();
            playButton.onClick.AddListener(OnPlay);
            backButton.onClick.AddListener(OnBack);
        }

        public void Bind(MiniGameData data)
        {
            _current = data;
            titleText.text = data.Title;
            descriptionText.text = data.Description;
        }

        public override async UniTask SetupAsync(CancellationToken token)
        {
            if (_current == null || videoPlayer == null) return;

            videoPlayer.Stop();
            videoPlayer.clip = _current.VideoClip;
            videoPlayer.isLooping = true;
            videoPlayer.Prepare();
            await UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: token);

            if (videoDisplay != null && videoPlayer.targetTexture != null)
                videoDisplay.texture = videoPlayer.targetTexture;

            videoPlayer.Play();
        }

        private void OnPlay()
        {
            if (IsAnimating) return;
            if (_current == null) return;
            var data = _current;
            ScreenController.Instance.ShowAsync(
                ScreenType.MiniGame,
                go => go.GetComponentInChildren<MiniGamePanel>().Bind(data)
            ).Forget();
        }

        private void OnBack()
        {
            if (IsAnimating) return;
            CloseAsync().Forget();
        }

        private async UniTaskVoid CloseAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                await PanelOutAsync(token);
            }
            catch (System.OperationCanceledException) { }
            if (this != null) Destroy(gameObject);
        }
    }
}
