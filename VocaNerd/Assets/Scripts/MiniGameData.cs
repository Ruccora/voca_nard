using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine.Video;
#endif

namespace VocaNerd
{
    [CreateAssetMenu(menuName = "VocaNerd/MiniGameData", fileName = "MiniGameData")]
    public class MiniGameData : ScriptableObject
    {
        [SerializeField] private string title;
        [SerializeField, TextArea(3, 8)] private string description;
        [SerializeField] private Sprite thumbnail;
        [SerializeField] private string videoFileName;
#if !UNITY_WEBGL || UNITY_EDITOR
        [SerializeField] private VideoClip videoClip;
#endif
        [SerializeField] private GameObject miniGamePrefab;

        [Tooltip("このミニゲーム中に流す BGM キー（BgmKey の定数）。空なら直前の BGM を継続")]
        [SerializeField] private string bgmKey;

        public string Title => title;
        public string Description => description;
        public Sprite Thumbnail => thumbnail;
        public string VideoFileName => videoFileName;
#if !UNITY_WEBGL || UNITY_EDITOR
        public VideoClip VideoClip => videoClip;
#endif
        public GameObject MiniGamePrefab => miniGamePrefab;
        public string BgmKey => bgmKey;
    }
}
