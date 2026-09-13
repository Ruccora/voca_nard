using System;
using UnityEngine.InputSystem;

namespace VocaNerd
{
    /// <summary>
    /// ミニゲームのリザルト (勝敗が決まってからの入力待ち) 専用の入力。全ミニゲーム共通で
    /// パッドは A で再戦 / B で戻る (割り当ては GamepadButtons)、キーボードは Enter で再戦 / X で戻る。
    ///
    /// 操作できるのは 1P だけ (<see cref="PlayerDevices.IsPlayerOne"/> で判定)。
    /// 2P のパッドから A/B を押しても無視する。
    /// </summary>
    public sealed class ResultInput : IDisposable
    {
        private readonly InputAction _retryAction;
        private readonly InputAction _exitAction;
        private bool _enabled;

        public ResultInput(Action onRetry, Action onExit)
        {
            // 2P のパッドも同じパスに解決されるので PassThrough (理由は PlayerInputAction)。
            _retryAction = PlayerInputAction.Make("ResultRetry",
                GamepadButtons.A, "<Keyboard>/enter", "<Keyboard>/numpadEnter");
            PlayerInputAction.OnPress(_retryAction, 1, onRetry);

            // 戻るは X。escape / backspace は MiniGamePanel の Back が拾うのでここでは足さない
            // (両方で拾うと画面遷移が二重に走る)。
            _exitAction = PlayerInputAction.Make("ResultExit", GamepadButtons.B, "<Keyboard>/x");
            PlayerInputAction.OnPress(_exitAction, 1, onExit);
        }

        /// <summary>リザルトの入力待ちに入るタイミングで呼ぶ。</summary>
        public void Enable()
        {
            if (_enabled) return;
            _enabled = true;
            _retryAction.Enable();
            _exitAction.Enable();
        }

        /// <summary>再戦 / 退出が確定したら呼ぶ。二重入力を防ぐ。</summary>
        public void Disable()
        {
            if (!_enabled) return;
            _enabled = false;
            _retryAction.Disable();
            _exitAction.Disable();
        }

        public void Dispose()
        {
            _retryAction.Dispose();
            _exitAction.Dispose();
        }
    }
}
