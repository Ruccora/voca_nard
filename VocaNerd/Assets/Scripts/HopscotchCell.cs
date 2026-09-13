using Coffee.UIEffects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    [RequireComponent(typeof(RectTransform))]
    public class HopscotchCell : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private GameObject secondaryPlatform;
        [SerializeField] private Image secondaryImage;
        [SerializeField] private RectTransform secondaryRect;

        [Header("Layout")]
        [SerializeField] private Vector2 secondaryOffset = new Vector2(110f, 0f);

        private RectTransform _rt;
        private bool _isToggle;
        private bool _isTypeA;

        public RectTransform Rect => _rt != null ? _rt : (_rt = (RectTransform)transform);

        // sprite / color はコース生成側 (HopscotchRaceGame) が決めた「わっか画像」と「色」。
        // ぱ のマスは わっかを 2 枚使うので、色は 1 枚目 / 2 枚目で別々に受け取る。
        // (色は マス単位ではなく わっか画像 単位で送られる)
        public void Setup(bool isTypeA, bool isToggle, Sprite sprite, Color color, Color secondaryColor)
        {
            _isToggle = isToggle;
            _isTypeA = isTypeA;
            if (background != null)
            {
                if (sprite != null) background.sprite = sprite;
                background.color = color;
            }
            if (secondaryPlatform != null)
                secondaryPlatform.SetActive(!isTypeA);
            if (secondaryImage != null)
            {
                if (sprite != null) secondaryImage.sprite = sprite;
                secondaryImage.color = secondaryColor;
            }
            if (secondaryRect != null && !isTypeA)
            {
                secondaryRect.anchoredPosition = secondaryOffset;
            }

            // 生成直後は「飛べる」状態から始める
            SetToggleState(true);
        }

        // わっかの表示状態。点滅マス (isToggle) が飛べないタイミングのときだけ わっか自体を消す。
        // on = true で飛べる (表示)、false で飛べない (非表示)。点滅マスでないセルは常に表示。
        //
        // 呼ばれた時点の状態だけで表示を決め切る (早期 return しない) ので、毎フレーム呼んでよく、
        // 前ラウンドで消えた状態がそのまま残る、といった取りこぼしが起きない。
        public void SetToggleState(bool on)
        {
            var show = !_isToggle || on;
            if (background != null) background.gameObject.SetActive(show);
            // けん (TypeA) のマスは 2 枚目のわっかをそもそも使わないので出さない
            if (secondaryPlatform != null) secondaryPlatform.SetActive(show && !_isTypeA);
        }

        private UIEffect[] _depthEffects;
        private float _darken = -1f;

        // 奥行きの明暗。配下の全 Graphic に UIEffect(Multiply) を当てて暗くする。
        // multiplier = 1 で通常、小さいほど暗い。毎フレーム呼ばれるので変化がなければ何もしない。
        public void SetDarken(float multiplier)
        {
            if (Mathf.Approximately(_darken, multiplier)) return;
            _darken = multiplier;

            if (_depthEffects == null)
            {
                var graphics = GetComponentsInChildren<Graphic>(true);
                _depthEffects = new UIEffect[graphics.Length];
                for (var i = 0; i < graphics.Length; i++)
                {
                    var fx = graphics[i].GetComponent<UIEffect>();
                    if (fx == null) fx = graphics[i].gameObject.AddComponent<UIEffect>();
                    _depthEffects[i] = fx;
                }
            }

            var m = Mathf.Clamp01(multiplier);
            var darken = m < 0.999f;
            foreach (var fx in _depthEffects)
            {
                if (fx == null) continue;
                if (darken)
                {
                    fx.colorFilter = ColorFilter.Multiply;
                    fx.color = new Color(m, m, m, 1f);
                }
                else
                {
                    fx.colorFilter = ColorFilter.None;
                }
            }
        }
    }
}