using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    /// <summary>
    /// 開始演出。別 prefab として作り、ゲーム側の SerializeField に当てこんで使う。
    /// 既定は duration ぶん表示して終わる。独自演出は PlayAsync をオーバーライドする。
    /// </summary>
    public class OpeningEffect : MonoBehaviour
    {
        [SerializeField] private float duration = 1.5f;

        /// <summary>事前準備 (一度だけ)。必要なら派生でオーバーライドする。</summary>
        public virtual UniTask SetupAsync(CancellationToken token) => UniTask.CompletedTask;

        /// <summary>退場演出 (マスクが左右にはける等)。既定は何もしない。</summary>
        public virtual UniTask ExitAsync(CancellationToken token) => UniTask.CompletedTask;

        /// <summary>演出を再生し、完了で戻る。</summary>
        public virtual async UniTask PlayAsync(CancellationToken token)
        {
            gameObject.SetActive(true);
            try
            {
                // TODO: 派生 or 子オブジェクト(SpriteAnimation/TMP/SE 等)で実際の演出を作る
                if (duration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);
            }
            finally
            {
                if (this != null) gameObject.SetActive(false);
            }
        }
    }
}
