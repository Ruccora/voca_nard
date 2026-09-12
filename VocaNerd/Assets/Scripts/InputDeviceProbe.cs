#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace VocaNerd
{
    /// <summary>
    /// 「どのパッドがどのレイアウトで認識されていて、押したボタンがどの control に化けているか」を
    /// Console に出すだけの調査用。エディタと development build でのみ動く。
    ///
    /// パッドのラベル (A/B) と Unity の control 名 (buttonSouth = 下 / buttonEast = 右) が
    /// 食い違うときに、レイアウトを直すのかバインドを直すのかを判断するために使う。
    /// </summary>
    public class InputDeviceProbe : MonoBehaviour
    {
        private System.IDisposable _anyButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<InputDeviceProbe>() != null) return;

            var go = new GameObject(nameof(InputDeviceProbe));
            go.AddComponent<InputDeviceProbe>();
            DontDestroyOnLoad(go);
        }

        private void OnEnable()
        {
            LogDevices();
            InputSystem.onDeviceChange += OnDeviceChange;
            _anyButton = InputSystem.onAnyButtonPress.Call(OnAnyButton);
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            _anyButton?.Dispose();
            _anyButton = null;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            Debug.Log($"[InputProbe] {change}: {Describe(device)}");
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed)
                LogDevices();
        }

        private static void OnAnyButton(InputControl control)
        {
            var device = control.device;
            var player = PlayerDevices.IsForPlayer(device, 1) ? "1P"
                : PlayerDevices.IsForPlayer(device, 2) ? "2P" : "-";

            Debug.Log($"[InputProbe] 押された control = {control.path}  " +
                      $"(name: {control.name}, 表示名: {control.displayName})  " +
                      $"device: {device.displayName} / layout: {device.layout} / {player}");
        }

        private static void LogDevices()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[InputProbe] 接続中のデバイス");
            foreach (var device in InputSystem.devices)
                sb.AppendLine("  - " + Describe(device));

            sb.AppendLine($"  Gamepad.all = {Gamepad.all.Count} 台 (接続順 [0] が 1P)");
            for (var i = 0; i < Gamepad.all.Count; i++)
                sb.AppendLine($"    [{i}] {Describe(Gamepad.all[i])}");

            Debug.Log(sb.ToString());
        }

        private static string Describe(InputDevice device)
        {
            if (device == null) return "(null)";

            var d = device.description;
            return $"{device.displayName} | layout: {device.layout} | interface: {d.interfaceName} | " +
                   $"product: {d.product} | manufacturer: {d.manufacturer} | capabilities: {d.capabilities}";
        }
    }
}
#endif
