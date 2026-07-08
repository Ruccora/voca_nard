using UnityEngine;
using UnityEngine.EventSystems;

namespace VocaNerd
{
    public class SelectionKeeper : MonoBehaviour
    {
        private GameObject _lastSelected;

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
