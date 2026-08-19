using Coffee.UIEffects;
using UnityEngine;

namespace VocaNerd
{
    /// <summary>
    /// UIEffect の Edge Shiny を使い、枠 (SelectFrame 等) のアウトライン上を
    /// 光がくるくる周回する演出を付ける。枠の Graphic (Image) と同じ GameObject に付けて使う。
    /// UIEffect コンポーネントが無ければ自動追加する。
    /// </summary>
    [DisallowMultipleComponent]
    public class ShinyOutline : MonoBehaviour
    {
        [SerializeField] private UIEffect uiEffect;

        [Header("Outline")]
        [SerializeField, Range(0f, 1f)] private float edgeWidth = 0.15f;
        [SerializeField] private bool useCustomColor = true;
        [SerializeField] private Color edgeColor = Color.white;
        [SerializeField] private bool glow = true;

        [Header("Shiny")]
        [SerializeField, Range(0f, 1f)] private float shinyWidth = 0.5f;
        [SerializeField, Range(-5f, 5f)] private float speed = 1.5f; // 周回速度 (UIEffect の autoPlay 上限 ±5)

        private void Reset()
        {
            uiEffect = GetComponent<UIEffect>();
        }

        private void Awake()
        {
            if (uiEffect == null) uiEffect = GetComponent<UIEffect>();
            if (uiEffect == null) uiEffect = gameObject.AddComponent<UIEffect>();
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (uiEffect == null) uiEffect = GetComponent<UIEffect>();
            if (uiEffect != null) Apply();
        }
#endif

        /// <summary>UIEffect に Edge Shiny 設定を反映する。</summary>
        public void Apply()
        {
            if (uiEffect == null) return;

            uiEffect.edgeMode = EdgeMode.Shiny;
            uiEffect.edgeWidth = edgeWidth;
            uiEffect.edgeShinyWidth = shinyWidth;
            uiEffect.edgeColorGlow = glow;
            uiEffect.edgeShinyAutoPlaySpeed = speed; // 縁に沿って光が自動周回

            if (useCustomColor)
            {
                uiEffect.edgeColorFilter = ColorFilter.Replace;
                uiEffect.edgeColor = edgeColor;
            }
        }

        /// <summary>周回速度を実行時に変更する。</summary>
        public void SetSpeed(float value)
        {
            speed = Mathf.Clamp(value, -5f, 5f);
            if (uiEffect != null) uiEffect.edgeShinyAutoPlaySpeed = speed;
        }
    }
}
