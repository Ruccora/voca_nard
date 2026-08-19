using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    /// <summary>
    /// ボタン押下時の SE を既定（<see cref="SeKey.Decide"/>）から差し替えるためのマーカー。
    /// 実際に onClick を購読するのは <see cref="PanelBase"/> 側で、このコンポーネントはキーを持つだけ。
    /// Key を空文字にするとそのボタンは無音になる。
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class ButtonSeKey : MonoBehaviour
    {
        [Tooltip("押下時に鳴らす SE キー。空にすると無音")]
        [SerializeField] private string key = SeKey.Cancel;

        public string Key => key;
    }
}
