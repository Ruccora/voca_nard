using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    // 画像 (Assets/Texture/Common/num-00〜num-09, num-colon) で数字を表示する。
    // 1 文字ぶんの Image を子として生成/再利用し、中央揃えで横に並べる。
    public class SpriteNumber : MonoBehaviour
    {
        [Tooltip("1 文字ぶんの Image prefab。マテリアル/エフェクト等の見た目はこの prefab 側で持つ。" +
                 "未設定なら素の Image を生成する")]
        [SerializeField] private Image glyphPrefab;
        [Tooltip("0〜9 の順に並べた数字画像")]
        [SerializeField] private Sprite[] digitSprites = new Sprite[10];
        [Tooltip("':' と '.' に使う画像")]
        [SerializeField] private Sprite colonSprite;
        [Tooltip("1 文字の高さ (px)。幅はスプライトのアスペクト比から決まる")]
        [SerializeField] private float glyphHeight = 96f;
        [Tooltip("文字間 (px)。負の値で詰める")]
        [SerializeField] private float spacing = -40f;

        private readonly List<Image> _glyphs = new List<Image>();
        // Rebuild の作業用。毎フレーム更新されても確保し直さないよう使い回す。
        private readonly List<Sprite> _sprites = new List<Sprite>();
        private readonly List<float> _widths = new List<float>();
        private string _current;

        // 数字/コロン以外の文字は無視される。同じ文字列なら作り直さない。
        public void SetText(string text)
        {
            text ??= string.Empty;
            if (_current == text) return;
            _current = text;
            Rebuild(text);
        }

        public void Clear() => SetText(string.Empty);

#if UNITY_EDITOR
        // コンポーネント追加時 / コンテキストメニューから Assets/Texture/Common の画像を割り当てる
        [ContextMenu("Load Common Num Sprites")]
        private void Reset()
        {
            const string dir = "Assets/Texture/Common";
            var digits = new Sprite[10];
            for (var i = 0; i < digits.Length; i++)
                digits[i] = LoadEditorSprite($"{dir}/num-{i:00}.png");
            digitSprites = digits;
            colonSprite = LoadEditorSprite($"{dir}/num-colon.png");
        }

        private static Sprite LoadEditorSprite(string path)
        {
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite s) return s;
            return null;
        }
#endif

        private void Rebuild(string text)
        {
            // 表示する文字とその幅を先に確定させる (合計幅から中央揃えの開始 x を出す)
            var sprites = _sprites;
            var widths = _widths;
            sprites.Clear();
            widths.Clear();
            var total = 0f;
            foreach (var c in text)
            {
                var sprite = ResolveSprite(c);
                if (sprite == null) continue;
                var w = GlyphWidth(sprite);
                sprites.Add(sprite);
                widths.Add(w);
                total += w;
            }
            if (sprites.Count > 1) total += spacing * (sprites.Count - 1);

            EnsureGlyphCount(sprites.Count);

            var x = -total * 0.5f;
            for (var i = 0; i < sprites.Count; i++)
            {
                var image = _glyphs[i];
                image.gameObject.SetActive(true);
                image.sprite = sprites[i];
                var rt = image.rectTransform;
                rt.sizeDelta = new Vector2(widths[i], glyphHeight);
                rt.anchoredPosition = new Vector2(x + widths[i] * 0.5f, 0f);
                x += widths[i] + spacing;
            }
            for (var i = sprites.Count; i < _glyphs.Count; i++)
                _glyphs[i].gameObject.SetActive(false);
        }

        private Sprite ResolveSprite(char c)
        {
            if (c >= '0' && c <= '9')
            {
                var index = c - '0';
                return digitSprites != null && index < digitSprites.Length ? digitSprites[index] : null;
            }
            // 小数点も同じコロン画像で表す (num-* に '.' が無い)
            return c == ':' || c == '.' ? colonSprite : null;
        }

        private float GlyphWidth(Sprite sprite)
        {
            var h = sprite.rect.height;
            if (h <= 0f) return glyphHeight;
            return glyphHeight * (sprite.rect.width / h);
        }

        private void EnsureGlyphCount(int count)
        {
            while (_glyphs.Count < count)
            {
                var image = glyphPrefab != null ? Instantiate(glyphPrefab, transform) : CreatePlainGlyph();
                image.gameObject.name = $"Glyph{_glyphs.Count}";

                // 配置は anchoredPosition で行うので、アンカー/pivot だけは中央に揃える。
                // 見た目 (色・マテリアル・エフェクト等) は prefab 側の設定をそのまま使う。
                var rt = image.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                _glyphs.Add(image);
            }
        }

        // glyphPrefab 未設定時のフォールバック
        private Image CreatePlainGlyph()
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }
    }
}
