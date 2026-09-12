using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VocaNerd
{
    /// <summary>
    /// 入力デバイスを 1P / 2P に振り分ける共通ルール。
    ///
    /// - キーボードは 1 台しかないので 1P/2P 両方の入力として通す (P1 = A/D 系、P2 = 矢印系でキーを分けている)。
    /// - Gamepad は「L + R を同時押しした順」で割り当てる。先に押したパッドが 1P、次が 2P。
    ///   同じボタン (A/B) を 1P/2P 両方にバインドしておき、ここでデバイスを見て弾く。
    /// - まだ誰も参加していないパッドは、空いている枠に接続順で仮に割り当てる。
    ///   (参加操作を知らないまま遊び始めても動くようにするため。L+R を押せばその時点で確定する)
    ///
    /// パッドが抜かれたらその枠は空き、次に L+R を押したパッドが入る。
    /// </summary>
    public static class PlayerDevices
    {
        /// <summary>参加が確定したときに飛ぶ (player = 1 or 2)。参加演出や表示に使う。</summary>
        public static event Action<int, InputDevice> Joined;

        private static InputDevice _p1;
        private static InputDevice _p2;

        public static InputDevice DeviceOf(int player) => player == 1 ? _p1 : _p2;

        public static bool IsForPlayer(InputDevice device, int player)
        {
            if (device is Keyboard) return true;
            if (device is Gamepad gamepad)
            {
                var assigned = player == 1 ? _p1 : _p2;
                if (assigned != null) return assigned == gamepad;

                // 枠が空いている間は接続順で仮割り当て。もう片方の枠が確定済みならそのパッドは除外する。
                return FallbackPlayerOf(gamepad) == player;
            }
            return false;
        }

        public static bool IsForPlayer(InputAction.CallbackContext ctx, int player)
            => IsForPlayer(ctx.control.device, player);

        /// <summary>1P の操作か。1P だけに許す操作 (リザルトの再戦 / 退出) で使う。</summary>
        public static bool IsPlayerOne(InputDevice device) => IsForPlayer(device, 1);

        /// <summary>割り当てを白紙に戻す。タイトルに戻ったときなど、参加をやり直したいときに呼ぶ。</summary>
        public static void Reset()
        {
            _p1 = null;
            _p2 = null;
            Debug.Log("[Input] player assignment reset");
        }

        // -------- 参加 (L + R 同時押し) --------

        /// <summary>L+R を押したパッドを空いている若い枠に入れる。すでに入っていれば何もしない。</summary>
        private static void TryJoin(Gamepad gamepad)
        {
            if (gamepad == null) return;
            if (_p1 == gamepad || _p2 == gamepad) return;

            if (_p1 == null) Assign(1, gamepad);
            else if (_p2 == null) Assign(2, gamepad);
        }

        private static void Assign(int player, InputDevice device)
        {
            if (player == 1) _p1 = device; else _p2 = device;
            Debug.Log($"[Input] Player {player} = [{device.deviceId}] {device.displayName} (layout={device.layout})");
            Joined?.Invoke(player, device);
        }

        // 未割り当てのパッドを Gamepad.all の順に、空いている枠へ若い順に当てる。
        // (例: 2P が確定済みで 1P が空なら、未割り当ての 1 台目だけが 1P として通る)
        private static int FallbackPlayerOf(Gamepad gamepad)
        {
            // gamepad が「未割り当てのうち何台目」か
            var order = -1;
            var count = 0;
            var all = Gamepad.all;
            for (var i = 0; i < all.Count; i++)
            {
                var pad = all[i];
                if (pad == _p1 || pad == _p2) continue;   // 確定済みのパッドは数えない
                if (pad == gamepad) { order = count; break; }
                count++;
            }
            if (order < 0) return 0;

            // 空き枠を若い順に見て、order 番目の枠を返す
            var seen = 0;
            for (var player = 1; player <= 2; player++)
            {
                if ((player == 1 ? _p1 : _p2) != null) continue;
                if (seen == order) return player;
                seen++;
            }
            return 0;
        }

        // -------- 参加受付とデバイス増減の監視 --------
        // シーンに置かなくても動くよう、起動時に常駐オブジェクトを 1 つ作って Update を回す。

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            _p1 = null;
            _p2 = null;
            Joined = null;

            // Enter Play Mode Options (ドメインリロード無効) でも二重登録にならないよう、外してから付ける。
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;

            var go = new GameObject("PlayerDeviceJoinWatcher");
            go.AddComponent<JoinWatcher>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected) return;
            if (_p1 == device) { _p1 = null; Debug.Log("[Input] Player 1 device removed"); }
            if (_p2 == device) { _p2 = null; Debug.Log("[Input] Player 2 device removed"); }
        }

        private class JoinWatcher : MonoBehaviour
        {
            private void Update()
            {
                if (_p1 != null && _p2 != null) return;   // 2 枠とも埋まっていれば受け付けない

                foreach (var pad in Gamepad.all)
                {
                    if (pad.leftShoulder.isPressed && pad.rightShoulder.isPressed)
                        TryJoin(pad);
                }
            }
        }
    }
}
