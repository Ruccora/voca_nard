using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    /// <summary>
    /// 刹那の見切りの開始演出。斜めの黒線を、両側のマスク(カバー)を移動させて
    /// 真ん中に向かって表示する。露出後は表示したままにし、退場(ExitAsync)で
    /// マスクを左右にはけさせる。線とマスクの見た目・配置は prefab 側でリグする。
    /// </summary>
    public class MikiriOpeningEffect : OpeningEffect
    {
        [Header("Masks (両側)")]
        [SerializeField] private RectTransform leftMask;
        [SerializeField] private RectTransform rightMask;
        [SerializeField] private Vector2 leftClosed;  // 線を隠している位置
        [SerializeField] private Vector2 leftOpen;    // 露出しきった位置
        [SerializeField] private Vector2 leftExit;    // 左へはけた位置
        [SerializeField] private Vector2 rightClosed;
        [SerializeField] private Vector2 rightOpen;
        [SerializeField] private Vector2 rightExit;   // 右へはけた位置

        [Header("Timing")]
        [SerializeField] private float revealDuration = 1.2f; // 端→中央の露出
        [SerializeField] private float exitDuration = 0.6f;   // 左右へはける

        // 端→中央に露出。表示したまま戻る (待機/退場は呼び出し側が制御)。
        public override async UniTask PlayAsync(CancellationToken token)
        {
            gameObject.SetActive(true);
            SetMasks(leftClosed, rightClosed);

            var elapsed = 0f;
            while (elapsed < revealDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var e = EaseOutCubic(revealDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / revealDuration));
                SetMasks(Vector2.LerpUnclamped(leftClosed, leftOpen, e),
                         Vector2.LerpUnclamped(rightClosed, rightOpen, e));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetMasks(leftOpen, rightOpen);
        }

        // マスクを左右にはけさせて退場
        public override async UniTask ExitAsync(CancellationToken token)
        {
            var elapsed = 0f;
            while (elapsed < exitDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var e = EaseInCubic(exitDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / exitDuration));
                SetMasks(Vector2.LerpUnclamped(leftOpen, leftExit, e),
                         Vector2.LerpUnclamped(rightOpen, rightExit, e));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetMasks(leftExit, rightExit);
            if (this != null) gameObject.SetActive(false);
        }

        private void SetMasks(Vector2 left, Vector2 right)
        {
            if (leftMask != null) leftMask.anchoredPosition = left;
            if (rightMask != null) rightMask.anchoredPosition = right;
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseInCubic(float t) => t * t * t;
    }
}
