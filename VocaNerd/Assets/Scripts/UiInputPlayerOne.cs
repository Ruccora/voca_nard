using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace VocaNerd
{
    /// <summary>
    /// EventSystem (InputSystemUIInputModule) が拾う入力を 1P のデバイスだけに絞る。
    ///
    /// タイトルとミニゲーム選択は UI の Button + EventSystem のナビゲーションで動いているが、
    /// 既定の UI アクションは Submit が <c>*/{Submit}</c> なので「どのパッドの A でも決定が通る」。
    /// 2P のパッドで画面が進んでしまうのを防ぐため、UI アクション側に
    /// 「キーボード / マウス / 1P パッド」だけのデバイスマスクを掛ける。
    /// 1P の割り当ては <see cref="PlayerDevices"/> と同じく接続順 (<c>Gamepad.all[0]</c>)。
    ///
    /// ミニゲーム中の操作や説明画面 (ExplainPanelBase) は各自が自前の InputAction を
    /// 持っているのでここの影響を受けない。2P はこれまで通り遊べる。
    ///
    /// シーンに置かなくてよいように起動時に自分で常駐オブジェクトを作る。
    /// </summary>
    public class UiInputPlayerOne : MonoBehaviour
    {
        private InputActionAsset _maskedActions;
        private InputActionAsset _ownedActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<UiInputPlayerOne>() != null) return;

            var go = new GameObject(nameof(UiInputPlayerOne));
            go.AddComponent<UiInputPlayerOne>();
            DontDestroyOnLoad(go);
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            Apply();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            Restore();
        }

        // パッドの抜き差しで 1P が繰り上がる / 増えるので、そのたびに掛け直す
        private void OnDeviceChange(InputDevice device, InputDeviceChange change) => Apply();

        private void Apply()
        {
            var module = FindAnyObjectByType<InputSystemUIInputModule>();
            if (module == null) return;

            var actions = EnsureOwnActions(module);
            if (actions == null) return;

            _maskedActions = actions;

            var devices = new List<InputDevice>();
            AddIfNew(devices, Keyboard.current);
            AddIfNew(devices, Mouse.current);
            AddIfNew(devices, Pointer.current);
            if (Gamepad.all.Count > 0) AddIfNew(devices, Gamepad.all[0]);

            actions.devices = devices.ToArray();
        }

        /// <summary>
        /// UI アクションを自前の複製に差し替え、Submit / Cancel のパッド側を
        /// <see cref="GamepadButtons"/> の割り当て (ラベル A で決定 / B でキャンセル) に直す。
        ///
        /// 既定の UI アクションは Submit が <c>*/{Submit}</c> = パッドの下ボタン固定で、
        /// このプロジェクトのパッド (A が右 / B が下) だと「B で決定」になってしまう。
        /// パッケージ同梱のアセットを直接書き換えたくないので複製に対して行う。
        /// </summary>
        private InputActionAsset EnsureOwnActions(InputSystemUIInputModule module)
        {
            var actions = module.actionsAsset;
            if (actions == null) return null;
            if (actions == _ownedActions) return actions;   // 差し替え済み

            var clone = Instantiate(actions);
            clone.name = actions.name + " (VocaNerd)";
            clone.Disable();

            // 1 本しかない */{Submit} / */{Cancel} をパッド用に置き換え、
            // 失われるキーボード操作はバインドを足して戻す。
            Rebind(clone.FindAction("UI/Submit"), GamepadButtons.A, "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space");
            Rebind(clone.FindAction("UI/Cancel"), GamepadButtons.B, "<Keyboard>/escape", "<Keyboard>/backspace");

            module.actionsAsset = clone;
            _ownedActions = clone;
            return module.actionsAsset;
        }

        private static void Rebind(InputAction action, string gamepadPath, params string[] keyboardPaths)
        {
            if (action == null) return;

            if (action.bindings.Count > 0) action.ApplyBindingOverride(0, gamepadPath);
            else action.AddBinding(gamepadPath);

            foreach (var path in keyboardPaths)
                action.AddBinding(path);
        }

        private void Restore()
        {
            if (_maskedActions != null) _maskedActions.devices = null;
            _maskedActions = null;
        }

        private static void AddIfNew(List<InputDevice> list, InputDevice device)
        {
            if (device == null || list.Contains(device)) return;
            list.Add(device);
        }
    }
}
