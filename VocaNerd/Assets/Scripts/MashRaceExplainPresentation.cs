using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    /// <summary>
    /// ダブルラリアット の説明演出。
    /// SpriteAnimation 1 つをループさせつつ、それとは別の Image を
    /// 2 枚の画像で一定間隔 (既定 1 秒) ごとに切り替える。
    /// </summary>
    public class MashRaceExplainPresentation : ExplainPresentation
    {
        [SerializeField] private SpriteAnimation anim;
        [Tooltip("最後まで再生してから次の周を始めるまでの待機秒数。0 なら途切れずに回り続ける")]
        [SerializeField] private float loopPause = 0f;

        [Header("Flip Image (一定間隔で切り替える別 Image)")]
        [SerializeField] private Image flipImage;
        [Tooltip("順番に切り替える画像。2 枚なら交互になる")]
        [SerializeField] private Sprite[] flipSprites = new Sprite[2];
        [SerializeField] private float flipInterval = 1f;

        protected override UniTask RunAsync(CancellationToken token)
        {
            return UniTask.WhenAll(
                LoopWithPauseAsync(anim, loopPause, token),
                FlipImageAsync(flipImage, flipSprites, flipInterval, token));
        }
    }
}
