using UnityEngine;

namespace VocaNerd
{
    public class CursorController : MonoBehaviour
    {
        [SerializeField] private bool hideOnAwake = true;

        private void Awake()
        {
            if (hideOnAwake) HideCursor();
        }

        public static void HideCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        public static void ShowCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
