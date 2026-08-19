using System;
using System.Collections.Generic;
using UnityEngine;

namespace VocaNerd
{
    /// <summary>
    /// BGM / SE のキーと AudioClip の対応表を持つ ScriptableObject。
    /// <see cref="AudioManager"/> がこれを参照するので、再生側は文字列キーだけを知っていればよい。
    /// キー文字列は <see cref="BgmKey"/> / <see cref="SeKey"/> の定数と一致させる。
    /// </summary>
    [CreateAssetMenu(menuName = "VocaNerd/AudioLibrary", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private AudioClip clip;

            [Tooltip("この音だけ個別に音量を下げ/上げしたいときの倍率。最終音量 = Master x (Bgm|Se) x これ x 呼び出し側の倍率")]
            [SerializeField, Range(0f, 2f)] private float volumeScale = 1f;

            public string Key => key;
            public AudioClip Clip => clip;
            public float VolumeScale => volumeScale;
        }

        [Header("BGM (ループ再生・クロスフェード)")]
        [SerializeField] private Entry[] bgmEntries = Array.Empty<Entry>();

        [Header("SE (ワンショット)")]
        [SerializeField] private Entry[] seEntries = Array.Empty<Entry>();

        private Dictionary<string, Entry> _bgmMap;
        private Dictionary<string, Entry> _seMap;

        /// <summary>キーに対応する BGM を引く。未登録 / AudioClip 未アサインなら false。</summary>
        public bool TryGetBgm(string key, out Entry entry)
            => TryGet(ref _bgmMap, bgmEntries, key, out entry);

        /// <summary>キーに対応する SE を引く。未登録 / AudioClip 未アサインなら false。</summary>
        public bool TryGetSe(string key, out Entry entry)
            => TryGet(ref _seMap, seEntries, key, out entry);

        private bool TryGet(ref Dictionary<string, Entry> map, Entry[] source, string key, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(key)) return false;

            map ??= BuildMap(source);
            return map.TryGetValue(key, out entry) && entry.Clip != null;
        }

        private Dictionary<string, Entry> BuildMap(Entry[] source)
        {
            var map = new Dictionary<string, Entry>(source != null ? source.Length : 0);
            if (source == null) return map;

            foreach (var e in source)
            {
                if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                if (map.ContainsKey(e.Key))
                {
                    Debug.LogWarning($"[AudioLibrary] '{name}' にキー '{e.Key}' が重複している。最初の行を使う。", this);
                    continue;
                }
                map[e.Key] = e;
            }
            return map;
        }

        private void OnValidate()
        {
            // Inspector で行を編集したらキャッシュを捨てて次回引き直させる
            _bgmMap = null;
            _seMap = null;
        }
    }
}
