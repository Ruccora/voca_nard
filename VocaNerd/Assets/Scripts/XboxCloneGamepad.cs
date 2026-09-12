#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XInput;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VocaNerd
{
    /// <summary>
    /// Xbox 360 互換 (VID 0x045E / PID 0x028E) の USB パッドを macOS で Gamepad として認識させる。
    ///
    /// Input System は macOS の Xbox パッドを Product="Controller" かつ Manufacturer="Microsoft" で
    /// 判定しているが、互換パッドはメーカー名が違う (手元のものは "ZhiXu") のでマッチせず、
    /// 汎用 HID の Joystick になってしまう。そうなると &lt;Gamepad&gt;/... のバインドに乗らない。
    ///
    /// 入力レポートは Xbox 360 標準の 20 バイトで、Unity の <see cref="XboxGamepadMacOS"/>
    /// (XInputControllerOSXState) とビット配置・オフセットが一致するため、それをそのまま流用する。
    ///
    /// macOS 専用。WebGL ではブラウザの Gamepad API 経由になるのでこの経路は通らない。
    /// </summary>
    public static class XboxCloneGamepad
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // 登録すると、すでに Joystick として作られていたデバイスもこのレイアウトで作り直される。
            InputSystem.RegisterLayout<XboxGamepadMacOS>(
                matches: new InputDeviceMatcher()
                    .WithInterface("HID")
                    .WithCapability("vendorId", 0x045E)
                    .WithCapability("productId", 0x028E));
        }
    }
}
#endif
