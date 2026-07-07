using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Sources")]
        [SerializeField] private AudioSource bgmSourceA;
        [SerializeField] private AudioSource bgmSourceB;
        [SerializeField] private AudioSource seSource;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float seVolume = 1f;

        private AudioSource _activeBgm;
        private CancellationTokenSource _bgmCts;

        public float MasterVolume
        {
            get => masterVolume;
            set { masterVolume = Mathf.Clamp01(value); ApplyBgmVolume(); }
        }
        public float BgmVolume
        {
            get => bgmVolume;
            set { bgmVolume = Mathf.Clamp01(value); ApplyBgmVolume(); }
        }
        public float SeVolume
        {
            get => seVolume;
            set { seVolume = Mathf.Clamp01(value); }
        }

        public AudioClip CurrentBgmClip => _activeBgm != null ? _activeBgm.clip : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (bgmSourceA != null) { bgmSourceA.volume = 0f; bgmSourceA.loop = true; bgmSourceA.playOnAwake = false; }
            if (bgmSourceB != null) { bgmSourceB.volume = 0f; bgmSourceB.loop = true; bgmSourceB.playOnAwake = false; }
            if (seSource != null) { seSource.playOnAwake = false; seSource.loop = false; seSource.volume = 1f; }
        }

        // -------- BGM --------
        public async UniTask PlayBgmAsync(AudioClip clip, float fadeDuration = 0.5f, CancellationToken cancellationToken = default)
        {
            if (clip == null) return;
            if (_activeBgm != null && _activeBgm.clip == clip && _activeBgm.isPlaying) return;

            _bgmCts?.Cancel();
            _bgmCts?.Dispose();
            _bgmCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _bgmCts.Token;

            var newSrc = (_activeBgm == bgmSourceA) ? bgmSourceB : bgmSourceA;
            if (newSrc == null) return;

            newSrc.clip = clip;
            newSrc.loop = true;
            newSrc.volume = 0f;
            newSrc.Play();

            var oldSrc = _activeBgm;
            _activeBgm = newSrc;

            await CrossFadeAsync(newSrc, oldSrc, fadeDuration, token);
            if (oldSrc != null) oldSrc.Stop();
        }

        public async UniTask StopBgmAsync(float fadeDuration = 0.5f, CancellationToken cancellationToken = default)
        {
            _bgmCts?.Cancel();
            _bgmCts?.Dispose();
            _bgmCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _bgmCts.Token;

            var src = _activeBgm;
            _activeBgm = null;
            if (src == null) return;

            await FadeVolumeAsync(src, src.volume, 0f, fadeDuration, token);
            src.Stop();
        }

        // -------- SE --------
        public void PlaySE(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || seSource == null) return;
            seSource.PlayOneShot(clip, masterVolume * seVolume * Mathf.Clamp01(volumeScale));
        }

        public void PlaySEAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, masterVolume * seVolume * Mathf.Clamp01(volumeScale));
        }

        // -------- Internal --------
        private void ApplyBgmVolume()
        {
            if (_activeBgm != null) _activeBgm.volume = masterVolume * bgmVolume;
        }

        private async UniTask FadeVolumeAsync(AudioSource src, float from, float to, float duration, CancellationToken token)
        {
            if (duration <= 0f) { src.volume = to; return; }
            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            src.volume = to;
        }

        private async UniTask CrossFadeAsync(AudioSource newSrc, AudioSource oldSrc, float duration, CancellationToken token)
        {
            var targetNew = masterVolume * bgmVolume;
            var startNew = newSrc.volume;
            var startOld = oldSrc != null ? oldSrc.volume : 0f;

            if (duration <= 0f)
            {
                newSrc.volume = targetNew;
                if (oldSrc != null) oldSrc.volume = 0f;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                newSrc.volume = Mathf.Lerp(startNew, targetNew, t);
                if (oldSrc != null) oldSrc.volume = Mathf.Lerp(startOld, 0f, t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            newSrc.volume = targetNew;
            if (oldSrc != null) oldSrc.volume = 0f;
        }

        private void OnDestroy()
        {
            _bgmCts?.Cancel();
            _bgmCts?.Dispose();
            _bgmCts = null;
            if (Instance == this) Instance = null;
        }
    }
}
