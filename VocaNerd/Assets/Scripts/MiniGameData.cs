using UnityEngine;
using UnityEngine.Video;

namespace VocaNerd
{
    [CreateAssetMenu(menuName = "VocaNerd/MiniGameData", fileName = "MiniGameData")]
    public class MiniGameData : ScriptableObject
    {
        [SerializeField] private string title;
        [SerializeField, TextArea(3, 8)] private string description;
        [SerializeField] private Sprite thumbnail;
        [SerializeField] private VideoClip videoClip;
        [SerializeField] private GameObject miniGamePrefab;

        public string Title => title;
        public string Description => description;
        public Sprite Thumbnail => thumbnail;
        public VideoClip VideoClip => videoClip;
        public GameObject MiniGamePrefab => miniGamePrefab;
    }
}
