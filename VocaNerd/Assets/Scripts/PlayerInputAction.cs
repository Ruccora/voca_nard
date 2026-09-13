using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace VocaNerd
{
    /// <summary>
    /// ミニゲーム / 説明画面 / リザルトが使う「1P か 2P の押下」を作る共通部品。
    ///
    /// このプロジェクトは 1P と 2P に同じバインドパス (<see cref="GamepadButtons"/> など) を張り、
    /// どちらのパッドかは <see cref="PlayerDevices"/> で振り分ける方式を取っている。
    /// このとき <see cref="InputActionType.Button"/> にすると Input System の衝突解決
    /// (conflict resolution) が働いてしまう:
    ///
    ///   - <c>&lt;Gamepad&gt;/...</c> は接続中の **全パッド** に解決されるので、1 アクションに
    ///     複数の control がぶら下がる。複数 control を持つ Button アクションは衝突解決が有効になる。
    ///   - 衝突解決は「今アクションを駆動している control より強く押されたか」しか通さない。
    ///     ボタンはどちらも 1.0 なので、2P が押している最中に 1P が押すと
    ///     「アクションの状態が変わらない」と判断されて **押下が捨てられる**。
    ///
    /// 2 人が同じボタンを叩く連打ゲームや早押しでは、これで片方の入力が恒常的に消える。
    /// そこで PassThrough にして衝突解決を切り、control ごとに独立して performed を受ける。
    ///
    /// PassThrough は「値が変わるたび」に performed が飛ぶ (離したときも来る) ので、
    /// 押した瞬間だけを取り出すのはここの役目。
    /// </summary>
    public static class PlayerInputAction
    {
        /// <summary>衝突解決を切った押下用アクションを作る。受け取り側は <see cref="OnPress"/> を使う。</summary>
        public static InputAction Make(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.PassThrough);
            foreach (var binding in bindings) action.AddBinding(binding);
            return action;
        }

        /// <summary>
        /// player のデバイスで押された瞬間だけ onPress を呼ぶ。
        /// player は 1 / 2、0 を渡すとデバイスを問わない。
        /// </summary>
        public static void OnPress(InputAction action, int player, Action onPress)
        {
            if (action == null || onPress == null) return;

            // control ごとに「今押されているか」を持つ。スティックのように値が連続で変わる
            // control でも、押下が 1 回だけ通るようにするため。
            var held = new HashSet<InputControl>();

            action.performed += ctx =>
            {
                var control = ctx.control;
                if (control == null) return;

                if (!ctx.ReadValueAsButton())
                {
                    held.Remove(control);
                    return;
                }

                if (!held.Add(control)) return;   // 押しっぱなしの途中変化
                if (player != 0 && !PlayerDevices.IsForPlayer(control.device, player)) return;

                onPress();
            };

            // Disable / Reset でアクションが止まると離した通知が来ないので、
            // 押しっぱなしの記録が残らないようここで捨てる (PassThrough では
            // canceled は値の変化では飛ばず、停止したときだけ飛ぶ)。
            action.canceled += _ => held.Clear();
        }
    }
}
