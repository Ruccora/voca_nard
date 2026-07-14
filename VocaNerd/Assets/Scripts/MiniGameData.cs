using UnityEngine;

namespace VocaNerd
{
    [CreateAssetMenu(menuName = "VocaNerd/MiniGameData", fileName = "MiniGameData")]
    public class MiniGameData : ScriptableObject
    {
        [SerializeField] private string title;
        [SerializeField, TextArea(3, 8)] private string description;
        [SerializeField] private Sprite thumbnail;
        [SerializeField] private string videoFileName;
        [SerializeField] private GameObject miniGamePrefab;

        public string Title => title;
        public string Description => description;
        public Sprite Thumbnail => thumbnail;
        public string VideoFileName => videoFileName;
        public GameObject MiniGamePrefab => miniGamePrefab;
    }
}
