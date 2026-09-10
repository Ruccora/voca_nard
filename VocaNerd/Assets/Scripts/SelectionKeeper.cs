using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace VocaNerd
{
    public class SelectionKeeper : MonoBehaviour
    {
        [Header("Gamepad/Keyboard navigation repeat")]
        [Tooltip("方向入力を押しっぱなしにしてから連続移動が始まるまでの待ち (秒)。大きいほど1入力=1マスになりやすい")]
        [SerializeField] private float moveRepeatDelay = 0.5f;

        [Tooltip("連続移動中の1マスあたりの間隔 (秒)。大きいほど1回のスティック倒しで飛びにくい")]
        [SerializeField] private float moveRepeatRate = 0.25f;

        [Tooltip("スティックの不感帯。中立付近のドリフト入力で選択が勝手に動くのを防ぐ (既定 0.125 は狭い)")]
        [SerializeField] private float stickDeadzoneMin = 0.3f;

        private GameObject _lastSelected;

        private void Start()
        {
            // 既定 (delay 0.5 / rate 0.1 = 10連射/秒) だとスティックを一瞬倒しただけで
            // 複数マス飛ぶので、EventSystem に同居するこのコンポーネントから連射を抑える。
            var module = GetComponent<InputSystemUIInputModule>();
            if (module != null)
            {
                module.moveRepeatDelay = moveRepeatDelay;
                module.moveRepeatRate = moveRepeatRate;
            }

            // スティックのドリフト(中立位置のわずかな入力)で選択が勝手に下端へ張り付く/
            // 戻るのを防ぐため、全体の不感帯を広げる。
            var settings = InputSystem.settings;
            if (settings != null && settings.defaultDeadzoneMin < stickDeadzoneMin)
                settings.defaultDeadzoneMin = stickDeadzoneMin;
        }

        private void Update()
        {
            var es = EventSystem.current;
            if (es == null) return;

            var current = es.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy)
            {
                _lastSelected = current;
                return;
            }

            if (_lastSelected != null && _lastSelected.activeInHierarchy)
            {
                es.SetSelectedGameObject(_lastSelected);
            }
        }
    }
}
