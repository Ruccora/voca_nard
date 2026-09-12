using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    /// <summary>
    /// 説明画面のルール枠で流す演出の基底。ミニゲームごとに必要な組み合わせが違う
    /// (SpriteAnimation が 1 個だったり 2 個だったり、別 Image のパラパラ切替が要ったり)
    /// ので、共通の部品だけここに置き、組み立ては派生クラスが <see cref="RunAsync"/> に書く。
    ///
    /// prefab 上では ExplainPanel とは別の component として付け、
    /// <see cref="ExplainPanelBase.presentation"/> に割り当てる。
    /// 再生開始は In のスライドが終わってから、停止は Out / 破棄のタイミング。
    /// </summary>
    public abstract class ExplainPresentation : MonoBehaviour
    {
        private CancellationTokenSource _cts;

        /// <summary>演出を開始する。ループし続ける前提なので待たずに投げっぱなしにする。</summary>
        public void Play(CancellationToken token)
        {
            Stop();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(
                token, this.GetCancellationTokenOnDestroy());
            RunLoopAsync(_cts.Token).Forget();
        }

        public void Stop()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        private void OnDisable() => Stop();

        private async UniTaskVoid RunLoopAsync(CancellationToken token)
        {
            try
            {
                await RunAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>演出本体。基本は「ずっと回り続ける」形で書き、停止はキャンセルに任せる。</summary>
        protected abstract UniTask RunAsync(CancellationToken token);

        // -------- 部品 --------

        /// <summary>
        /// 頭から 1 周だけ再生し、最後のフレームで止める。
        /// prefab 側の loop 設定に左右されないよう、尺ぶん待ってから最終フレームを当てている。
        /// </summary>
        protected static async UniTask PlayOnceAsync(SpriteAnimation anim, CancellationToken token)
        {
            if (anim == null || anim.Length == 0) return;

            anim.Play();
            var duration = anim.TotalDuration;
            if (duration > 0f)
                await DelayAsync(duration, token);
            if (anim != null) anim.SetFrame(anim.Length - 1);
        }

        /// <summary>1 周 → pause 秒待機 → もう一度、をキャンセルされるまで繰り返す。</summary>
        protected static async UniTask LoopWithPauseAsync(SpriteAnimation anim, float pause, CancellationToken token)
        {
            if (anim == null || anim.Length == 0) return;

            while (true)
            {
                await PlayOnceAsync(anim, token);
                if (pause > 0f) await DelayAsync(pause, token);
                else await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        /// <summary>Image の sprite を interval 秒ごとに順番に差し替え続ける。</summary>
        protected static async UniTask FlipImageAsync(Image image, Sprite[] sprites, float interval, CancellationToken token)
        {
            if (image == null || sprites == null || sprites.Length == 0) return;

            var index = 0;
            while (true)
            {
                var sprite = sprites[index % sprites.Length];
                if (sprite != null) image.sprite = sprite;
                index++;

                if (interval > 0f) await DelayAsync(interval, token);
                else await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        protected static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            image.sprite = sprite;
        }

        protected static UniTask DelayAsync(float seconds, CancellationToken token)
            => UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, cancellationToken: token);
    }
}
