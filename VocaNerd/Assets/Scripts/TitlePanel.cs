using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    public class TitlePanel : PanelBase
    {
        [SerializeField] private Button startButton;

        protected override void Awake()
        {
            base.Awake();
            startButton.onClick.AddListener(OnStart);
        }

        private void OnStart()
        {
            if (IsAnimating) return;
            ScreenController.Instance.ShowAsync(ScreenType.Select).Forget();
        }
    }
}
