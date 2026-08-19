using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VocaNerd
{
    /// <summary>
    /// BGM / SE の再生を一手に引き受ける singleton。
    /// 呼び出し側は基本 <see cref="Audio"/> のスタティック API 経由で使い、AudioClip ではなく
    /// <see cref="BgmKey"/> / <see cref="SeKey"/> の文字列キーを渡す（対応表は <see cref="AudioLibrary"/>）。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const string MasterVolumeSaveKey = "Audio.MasterVolume";
        private const string BgmVolumeSaveKey = "Audio.BgmVolume";
        private const string SeVolumeSaveKey = "Audio.SeVolume";

        public static AudioManager Instance { get; private set; }

        [Header("Library")]
        [Tooltip("キー→AudioClip の対応表。未設定でも動くが、キー指定の再生は全て無音になる")]
        [SerializeField] private AudioLibrary library;

        [Header("Sources")]
        [SerializeField] private AudioSource bgmSourceA;
        [SerializeField] private AudioSource bgmSourceB;
        [SerializeField] private AudioSource seSource;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float seVolume = 1f;

        [Header("Options")]
        [Tooltip("ボリュームを SaveData(PlayerPrefs) に永続化する")]
        [SerializeField] private bool persistVolumes = true;

        [Tooltip("シーンを跨いでも生存させる")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Tooltip("AudioLibrary に無いキーで再生を試みたら警告を出す（同じキーにつき 1 回だけ）")]
        [SerializeField] private bool warnOnMissingKey = true;

        private AudioSource _activeBgm;
        private string _activeBgmKey;
        private float _activeBgmScale = 1f;
        private CancellationTokenSource _bgmCts;
        private readonly HashSet<string> _warnedKeys = new HashSet<string>();

        public float MasterVolume
        {
            get => masterVolume;
            set => SetVolume(ref masterVolume, value, MasterVolumeSaveKey);
        }

        public float BgmVolume
        {
            get => bgmVolume;
            set => SetVolume(ref bgmVolume, value, BgmVolumeSaveKey);
        }

        public float SeVolume
        {
            get => seVolume;
            set => SetVolume(ref seVolume, value, SeVolumeSaveKey);
        }

        public AudioLibrary Library => library;

        /// <summary>現在鳴っている BGM のキー。AudioClip 直指定で再生した場合は null。</summary>
        public string CurrentBgmKey => _activeBgmKey;

        public AudioClip CurrentBgmClip => _activeBgm != null ? _activeBgm.clip : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            if (bgmSourceA != null) { bgmSourceA.volume = 0f; bgmSourceA.loop = true; bgmSourceA.playOnAwake = false; }
            if (bgmSourceB != null) { bgmSourceB.volume = 0f; bgmSourceB.loop = true; bgmSourceB.playOnAwake = false; }
            if (seSource != null) { seSource.playOnAwake = false; seSource.loop = false; seSource.volume = 1f; }

            LoadVolumes();
        }

        // -------- BGM --------

        /// <summary>
        /// キー指定で BGM をクロスフェード再生する。同じキーが既に鳴っていれば何もしない。
        /// </summary>
        public UniTask PlayBgmAsync(string key, float fadeDuration = 0.8f, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key)) return UniTask.CompletedTask;
            if (_activeBgmKey == key && _activeBgm != null && _activeBgm.isPlaying) return UniTask.CompletedTask;
            if (!TryGetBgmEntry(key, out var entry)) return UniTask.CompletedTask;

            return PlayBgmInternalAsync(key, entry.Clip, entry.VolumeScale, fadeDuration, cancellationToken);
        }

        /// <summary>AudioClip 直指定版。ライブラリを経由しないので個別音量倍率は 1 固定。</summary>
        public UniTask PlayBgmAsync(AudioClip clip, float fadeDuration = 0.8f, CancellationToken cancellationToken = default)
        {
            if (clip == null) return UniTask.CompletedTask;
            if (_activeBgm != null && _activeBgm.clip == clip && _activeBgm.isPlaying) return UniTask.CompletedTask;

            return PlayBgmInternalAsync(null, clip, 1f, fadeDuration, cancellationToken);
        }

        public async UniTask StopBgmAsync(float fadeDuration = 0.8f, CancellationToken cancellationToken = default)
        {
            var token = ResetBgmToken(cancellationToken);

            var src = _activeBgm;
            _activeBgm = null;
            _activeBgmKey = null;
            _activeBgmScale = 1f;
            if (src == null) return;

            await FadeVolumeAsync(src, src.volume, 0f, fadeDuration, token);
            src.Stop();
        }

        private async UniTask PlayBgmInternalAsync(string key, AudioClip clip, float clipScale, float fadeDuration, CancellationToken cancellationToken)
        {
            var token = ResetBgmToken(cancellationToken);

            var newSrc = (_activeBgm == bgmSourceA) ? bgmSourceB : bgmSourceA;
            if (newSrc == null) return;

            newSrc.clip = clip;
            newSrc.loop = true;
            newSrc.volume = 0f;
            newSrc.Play();

            var oldSrc = _activeBgm;
            _activeBgm = newSrc;
            _activeBgmKey = key;
            _activeBgmScale = clipScale;

            await CrossFadeAsync(newSrc, oldSrc, fadeDuration, token);
            if (oldSrc != null) oldSrc.Stop();
        }

        // -------- SE --------

        /// <summary>キー指定で SE をワンショット再生する。多重再生可。</summary>
        public void PlaySE(string key, float volumeScale = 1f)
        {
            if (!TryGetSeEntry(key, out var entry)) return;
            PlaySE(entry.Clip, entry.VolumeScale * volumeScale);
        }

        public void PlaySE(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || seSource == null) return;
            seSource.PlayOneShot(clip, Mathf.Clamp01(masterVolume * seVolume * Mathf.Max(0f, volumeScale)));
        }

        /// <summary>キー指定で 3D 位置つき SE を再生する。</summary>
        public void PlaySEAt(string key, Vector3 position, float volumeScale = 1f)
        {
            if (!TryGetSeEntry(key, out var entry)) return;
            PlaySEAt(entry.Clip, position, entry.VolumeScale * volumeScale);
        }

        public void PlaySEAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(masterVolume * seVolume * Mathf.Max(0f, volumeScale)));
        }

        // -------- Library lookup --------

        private bool TryGetBgmEntry(string key, out AudioLibrary.Entry entry)
        {
            entry = null;
            if (library != null && library.TryGetBgm(key, out entry)) return true;
            WarnMissing("BGM", key);
            return false;
        }

        private bool TryGetSeEntry(string key, out AudioLibrary.Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (library != null && library.TryGetSe(key, out entry)) return true;
            WarnMissing("SE", key);
            return false;
        }

        private void WarnMissing(string kind, string key)
        {
            if (!warnOnMissingKey || string.IsNullOrEmpty(key)) return;
            if (!_warnedKeys.Add($"{kind}:{key}")) return;
            Debug.LogWarning($"[AudioManager] {kind} キー '{key}' に対応する AudioClip が AudioLibrary に無い。", this);
        }

        // -------- Volume --------

        private void SetVolume(ref float field, float value, string saveKey)
        {
            field = Mathf.Clamp01(value);
            ApplyBgmVolume();
            if (persistVolumes) SaveData.SetFloat(saveKey, field);
        }

        private void LoadVolumes()
        {
            if (!persistVolumes) return;
            masterVolume = Mathf.Clamp01(SaveData.GetFloat(MasterVolumeSaveKey, masterVolume));
            bgmVolume = Mathf.Clamp01(SaveData.GetFloat(BgmVolumeSaveKey, bgmVolume));
            seVolume = Mathf.Clamp01(SaveData.GetFloat(SeVolumeSaveKey, seVolume));
        }

        private float BgmTargetVolume => Mathf.Clamp01(masterVolume * bgmVolume * _activeBgmScale);

        private void ApplyBgmVolume()
        {
            if (_activeBgm != null) _activeBgm.volume = BgmTargetVolume;
        }

        // -------- Internal --------

        private CancellationToken ResetBgmToken(CancellationToken cancellationToken)
        {
            _bgmCts?.Cancel();
            _bgmCts?.Dispose();
            _bgmCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return _bgmCts.Token;
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
            var targetNew = BgmTargetVolume;
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
