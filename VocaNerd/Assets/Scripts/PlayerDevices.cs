using UnityEngine.InputSystem;

namespace VocaNerd
{
    /// <summary>
    /// 入力デバイスを 1P / 2P に振り分ける共通ルール。
    ///
    /// - キーボードは 1 台しかないので 1P/2P 両方の入力として通す (P1 = A/D 系、P2 = 矢印系でキーを分けている)。
    /// - Gamepad は接続順 (<see cref="Gamepad.all"/>) で割り当てる。[0] = 1P、[1] = 2P。
    ///   同じボタン (A/B) を 1P/2P 両方にバインドしておき、ここでデバイスを見て弾く。
    ///
    /// 接続順ベースなので、1P のパッドを抜き差しすると 2P のパッドが 1P に繰り上がる点は注意。
    /// </summary>
    public static class PlayerDevices
    {
        public static bool IsForPlayer(InputDevice device, int player)
        {
            if (device is Keyboard) return true;
            if (device is Gamepad gamepad) return GamepadIndexOf(gamepad) == player - 1;
            return false;
        }

        public static bool IsForPlayer(InputAction.CallbackContext ctx, int player)
            => IsForPlayer(ctx.control.device, player);

        /// <summary>1P の操作か。1P だけに許す操作 (リザルトの再戦 / 退出) で使う。</summary>
        public static bool IsPlayerOne(InputDevice device) => IsForPlayer(device, 1);

        private static int GamepadIndexOf(Gamepad gamepad)
        {
            var all = Gamepad.all;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] == gamepad) return i;
            }
            return -1;
        }
    }
}
