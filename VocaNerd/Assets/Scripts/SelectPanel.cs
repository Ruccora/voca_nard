using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    public class SelectPanel : PanelBase
    {
        [SerializeField] private MiniGameData[] miniGames = new MiniGameData[4];
        [SerializeField] private Button[] miniGameButtons = new Button[4];
        [SerializeField] private Image[] miniGameThumbnails = new Image[4];
        [SerializeField] private ExplainPanel explainPanelPrefab;
        [SerializeField] private RectTransform explainRoot;

        private ExplainPanel _activeExplain;

        protected override void Awake()
        {
            base.Awake();
            for (var i = 0; i < miniGameButtons.Length; i++)
            {
                var index = i;
                if (miniGameButtons[i] != null)
                    miniGameButtons[i].onClick.AddListener(() => OnSelect(index));

                if (miniGameThumbnails[i] != null && miniGames[i] != null)
                    miniGameThumbnails[i].sprite = miniGames[i].Thumbnail;
            }
        }

        private void OnSelect(int index)
        {
            if (IsAnimating) return;
            if (_activeExplain != null) return;
            if (index < 0 || index >= miniGames.Length) return;
            var data = miniGames[index];
            if (data == null || explainPanelPrefab == null) return;

            OpenExplainAsync(data).Forget();
        }

        private async UniTaskVoid OpenExplainAsync(MiniGameData data)
        {
            var parent = explainRoot != null ? explainRoot : (RectTransform)transform;
            var explain = Instantiate(explainPanelPrefab, parent);
            _activeExplain = explain;
            explain.Bind(data);

            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                await explain.SetupAsync(token);
                await explain.PanelInAsync(token);
            }
            catch (System.OperationCanceledException)
            {
                if (explain != null) Destroy(explain.gameObject);
                _activeExplain = null;
            }
        }
    }
}
