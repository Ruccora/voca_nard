using System;
using UnityEngine.InputSystem;

namespace VocaNerd
{
    /// <summary>
    /// ミニゲームのリザルト (勝敗が決まってからの入力待ち) 専用の入力。
    /// コントローラーの A (buttonSouth) で再戦、B (buttonEast) で抜ける。
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
            _retryAction = new InputAction("ResultRetry", InputActionType.Button);
            _retryAction.AddBinding("<Gamepad>/buttonSouth");
            _retryAction.AddBinding("<Keyboard>/enter");
            _retryAction.AddBinding("<Keyboard>/space");
            _retryAction.performed += ctx => InvokeForPlayerOne(ctx, onRetry);

            // キーボードの escape / backspace は MiniGamePanel の Back が拾うのでここでは足さない
            // (両方で拾うと画面遷移が二重に走る)。
            _exitAction = new InputAction("ResultExit", InputActionType.Button);
            _exitAction.AddBinding("<Gamepad>/buttonEast");
            _exitAction.performed += ctx => InvokeForPlayerOne(ctx, onExit);
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

        private static void InvokeForPlayerOne(InputAction.CallbackContext ctx, Action action)
        {
            if (!PlayerDevices.IsPlayerOne(ctx.control.device)) return;
            action?.Invoke();
        }
    }
}
