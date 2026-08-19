using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    /// <summary>
    /// どこからでも音を鳴らすためのスタティック窓口。
    /// <see cref="AudioManager"/> がシーンに居なければ全て無処理になるので、
    /// AudioManager の有無を呼び出し側で気にしなくてよい。
    ///
    /// <code>
    /// Audio.PlaySE(SeKey.Decide);
    /// Audio.PlayBgm(BgmKey.Title);
    /// await Audio.PlayBgmAsync(BgmKey.Select, 1f, token);
    /// </code>
    /// </summary>
    public static class Audio
    {
        /// <summary>AudioManager がシーンに存在し、再生要求が実際に届く状態か。</summary>
        public static bool IsReady => AudioManager.Instance != null;

        // -------- SE --------

        public static void PlaySE(string key, float volumeScale = 1f)
            => AudioManager.Instance?.PlaySE(key, volumeScale);

        public static void PlaySE(AudioClip clip, float volumeScale = 1f)
            => AudioManager.Instance?.PlaySE(clip, volumeScale);

        public static void PlaySEAt(string key, Vector3 position, float volumeScale = 1f)
            => AudioManager.Instance?.PlaySEAt(key, position, volumeScale);

        public static void PlaySEAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
            => AudioManager.Instance?.PlaySEAt(clip, position, volumeScale);

        // -------- BGM --------

        /// <summary>BGM を投げっぱなしで切り替える（フェード完了を待たない）。</summary>
        public static void PlayBgm(string key, float fadeDuration = 0.8f)
            => PlayBgmAsync(key, fadeDuration).Forget();

        public static UniTask PlayBgmAsync(string key, float fadeDuration = 0.8f, CancellationToken cancellationToken = default)
        {
            var manager = AudioManager.Instance;
            return manager != null
                ? manager.PlayBgmAsync(key, fadeDuration, cancellationToken)
                : UniTask.CompletedTask;
        }

        public static void StopBgm(float fadeDuration = 0.8f)
            => StopBgmAsync(fadeDuration).Forget();

        public static UniTask StopBgmAsync(float fadeDuration = 0.8f, CancellationToken cancellationToken = default)
        {
            var manager = AudioManager.Instance;
            return manager != null
                ? manager.StopBgmAsync(fadeDuration, cancellationToken)
                : UniTask.CompletedTask;
        }

        /// <summary>現在鳴っている BGM のキー。未再生 / AudioManager 不在なら null。</summary>
        public static string CurrentBgmKey => AudioManager.Instance != null ? AudioManager.Instance.CurrentBgmKey : null;
    }
}
