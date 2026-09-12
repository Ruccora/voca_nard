using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    /// <summary>
    /// リザルトの勝者表示。「1P / 2P」の画像 (Assets/Texture/Common/1P.png / 2P.png) を出すだけ。
    /// 文言 (旧 WinnerText) は廃止した。
    ///
    /// 4 つのミニゲームで作りが同じなので、prefab の Result にこれを 1 つ付けて、
    /// 各ゲームからは <see cref="Show"/> / <see cref="Clear"/> だけ呼ぶ。
    /// </summary>
    public class WinnerLabel : MonoBehaviour
    {
        [Tooltip("1P / 2P の画像を出す Image")]
        [SerializeField] private Image playerImage;
        [Tooltip("1P の画像 (Assets/Texture/Common/1P.png)")]
        [SerializeField] private Sprite player1Sprite;
        [Tooltip("2P の画像 (Assets/Texture/Common/2P.png)")]
        [SerializeField] private Sprite player2Sprite;

        /// <summary>勝者を表示する。player は 1 / 2。</summary>
        public void Show(int player)
        {
            if (playerImage == null) return;
            var sprite = player == 1 ? player1Sprite : player2Sprite;
            playerImage.sprite = sprite;
            // 画像未設定のまま enabled にすると白い四角が出るので、その場合は消しておく
            playerImage.enabled = sprite != null;
        }

        /// <summary>表示を消す。ラウンド開始時の初期化用。</summary>
        public void Clear()
        {
            if (playerImage != null) playerImage.enabled = false;
        }

#if UNITY_EDITOR
        // コンポーネント追加時 / コンテキストメニューから Assets/Texture/Common の画像を割り当てる
        // (SpriteNumber と同じ作法)
        [ContextMenu("Load Common 1P/2P Sprites")]
        private void Reset()
        {
            const string dir = "Assets/Texture/Common";
            player1Sprite = LoadEditorSprite($"{dir}/1P.png");
            player2Sprite = LoadEditorSprite($"{dir}/2P.png");
            if (playerImage == null) playerImage = GetComponentInChildren<Image>(true);
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
    }
}
