using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VocaNerd
{
    /// <summary>
    /// ZhiXu の USB パッド (VID 0x0079 / PID 0x181C) を Gamepad として認識させるレイアウト。
    ///
    /// 何もしないと Unity は汎用 HID として Joystick を作ってしまい、&lt;Gamepad&gt;/... のバインドに
    /// 一切乗らない (Gamepad.all が 0 のまま)。ここで HID の入力レポートを Gamepad の control に
    /// 割り当てて登録することで、既存のバインドと 1P/2P 判定 (<see cref="PlayerDevices"/>) をそのまま使う。
    ///
    /// 入力レポート (9 バイト、ReportID なし) は実機の HID ディスクリプタから:
    ///   bit  0-14 : Button 1-15
    ///   bit  15   : Consumer AC Home
    ///   byte 2    : 下位 4bit = Hat switch (0-7、離すと 8 以上)
    ///   byte 3-6  : X / Y / Z / Rz (0-255、中央 128)
    ///   byte 7-8  : Accelerator / Brake (0-255)
    ///
    /// ボタン番号 → A/B/X/Y などの対応はディスクリプタからは分からないので、
    /// この手のパッドで一般的な並びを当てて実機で確認したもの。
    ///
    /// ここでの名前は Unity の control 名 = **位置** (South = 下 / East = 右) であって、
    /// パッドに印刷されたラベルではない。ラベル基準の割り当ては <see cref="GamepadButtons"/> を見ること。
    ///
    /// なお WebGL ではブラウザの Gamepad API 経由になり HID として見えないので、このレイアウトは使われない。
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 9)]
    public struct ZhiXuGamepadState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('H', 'I', 'D');

        [FieldOffset(0)]
        [InputControl(name = "buttonSouth", displayName = "South", bit = 0)]
        [InputControl(name = "buttonEast", displayName = "East", bit = 1)]
        [InputControl(name = "button3", layout = "Button", displayName = "Button 3", bit = 2)]
        [InputControl(name = "buttonWest", displayName = "X", bit = 3)]
        [InputControl(name = "buttonNorth", displayName = "Y", bit = 4)]
        [InputControl(name = "button6", layout = "Button", displayName = "Button 6", bit = 5)]
        [InputControl(name = "leftShoulder", displayName = "L", bit = 6)]
        [InputControl(name = "rightShoulder", displayName = "R", bit = 7)]
        [InputControl(name = "leftTriggerButton", layout = "Button", displayName = "L2 (digital)", bit = 8)]
        [InputControl(name = "rightTriggerButton", layout = "Button", displayName = "R2 (digital)", bit = 9)]
        [InputControl(name = "select", displayName = "Select", bit = 10)]
        [InputControl(name = "start", displayName = "Start", bit = 11)]
        [InputControl(name = "button13", layout = "Button", displayName = "Button 13", bit = 12)]
        [InputControl(name = "leftStickPress", displayName = "L3", bit = 13)]
        [InputControl(name = "rightStickPress", displayName = "R3", bit = 14)]
        [InputControl(name = "home", layout = "Button", displayName = "Home", bit = 15)]
        public ushort buttons;

        [FieldOffset(2)]
        [InputControl(name = "dpad", format = "BIT", layout = "Dpad", sizeInBits = 4, defaultState = 8)]
        [InputControl(name = "dpad/up", format = "BIT", layout = "DiscreteButton", bit = 0, sizeInBits = 4,
            parameters = "minValue=7,maxValue=1,nullValue=8,wrapAtValue=7")]
        [InputControl(name = "dpad/right", format = "BIT", layout = "DiscreteButton", bit = 0, sizeInBits = 4,
            parameters = "minValue=1,maxValue=3")]
        [InputControl(name = "dpad/down", format = "BIT", layout = "DiscreteButton", bit = 0, sizeInBits = 4,
            parameters = "minValue=3,maxValue=5")]
        [InputControl(name = "dpad/left", format = "BIT", layout = "DiscreteButton", bit = 0, sizeInBits = 4,
            parameters = "minValue=5,maxValue=7")]
        public byte hat;

        [FieldOffset(3)]
        [InputControl(name = "leftStick", layout = "Stick", format = "VC2B")]
        [InputControl(name = "leftStick/x", offset = 0, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "leftStick/left", offset = 0, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
        [InputControl(name = "leftStick/right", offset = 0, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1")]
        [InputControl(name = "leftStick/y", offset = 1, format = "BYTE",
            parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "leftStick/up", offset = 1, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
        [InputControl(name = "leftStick/down", offset = 1, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1,invert=false")]
        public byte leftStickX;

        [FieldOffset(4)] public byte leftStickY;

        [FieldOffset(5)]
        [InputControl(name = "rightStick", layout = "Stick", format = "VC2B")]
        [InputControl(name = "rightStick/x", offset = 0, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "rightStick/left", offset = 0, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
        [InputControl(name = "rightStick/right", offset = 0, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1")]
        [InputControl(name = "rightStick/y", offset = 1, format = "BYTE",
            parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "rightStick/up", offset = 1, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
        [InputControl(name = "rightStick/down", offset = 1, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1,invert=false")]
        public byte rightStickX;

        [FieldOffset(6)] public byte rightStickY;

        [FieldOffset(7)]
        [InputControl(name = "rightTrigger", format = "BYTE", displayName = "R2")]
        public byte accelerator;

        [FieldOffset(8)]
        [InputControl(name = "leftTrigger", format = "BYTE", displayName = "L2")]
        public byte brake;
    }

    [InputControlLayout(stateType = typeof(ZhiXuGamepadState), displayName = "ZhiXu Gamepad (HID)")]
    public class ZhiXuGamepad : Gamepad
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // 登録すると、すでに Joystick として作られていたデバイスもこのレイアウトで作り直される。
            InputSystem.RegisterLayout<ZhiXuGamepad>(
                matches: new InputDeviceMatcher()
                    .WithInterface("HID")
                    .WithCapability("vendorId", 0x0079)
                    .WithCapability("productId", 0x181C));
        }
    }
}
