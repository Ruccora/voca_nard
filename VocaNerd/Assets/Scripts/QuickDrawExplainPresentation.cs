using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    /// <summary>
    /// 刹那の見切り の説明演出。
    /// SpriteAnimation 2 つを並行でループさせ、どちらも「最後まで再生 → 一定秒待機 → 再開」。
    /// </summary>
    public class QuickDrawExplainPresentation : ExplainPresentation
    {
        [SerializeField] private SpriteAnimation animA;
        [SerializeField] private SpriteAnimation animB;
        [Tooltip("最後まで再生してから次の周を始めるまでの待機秒数")]
        [SerializeField] private float loopPause = 2f;

        protected override UniTask RunAsync(CancellationToken token)
        {
            // 2 つは独立に回る (尺が違っても互いを待たない)
            return UniTask.WhenAll(
                LoopWithPauseAsync(animA, loopPause, token),
                LoopWithPauseAsync(animB, loopPause, token));
        }
    }
}
