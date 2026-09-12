using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    /// <summary>
    /// けんけんぱ の説明演出。
    /// SpriteAnimation 2 つを **交互に 1 つずつ** 再生し (同時には動かない)、
    /// どちらの番かに合わせて別の Image の画像を差し替える。
    /// </summary>
    public class HopscotchExplainPresentation : ExplainPresentation
    {
        [SerializeField] private SpriteAnimation animA;
        [SerializeField] private SpriteAnimation animB;

        [Header("Switch Image (A/B の番に合わせて差し替える)")]
        [SerializeField] private Image switchImage;
        [SerializeField] private Sprite spriteForA;
        [SerializeField] private Sprite spriteForB;

        [Tooltip("片方が終わってからもう片方を始めるまでの待機秒数")]
        [SerializeField] private float turnInterval = 0.2f;

        protected override async UniTask RunAsync(CancellationToken token)
        {
            if (animA == null && animB == null) return;

            while (true)
            {
                SetSprite(switchImage, spriteForA);
                await PlayOnceAsync(animA, token);
                await WaitTurnAsync(token);

                SetSprite(switchImage, spriteForB);
                await PlayOnceAsync(animB, token);
                await WaitTurnAsync(token);
            }
        }

        private UniTask WaitTurnAsync(CancellationToken token)
            => turnInterval > 0f
                ? DelayAsync(turnInterval, token)
                : UniTask.Yield(PlayerLoopTiming.Update, token);
    }
}
