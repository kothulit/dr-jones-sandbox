using UnityEngine;
using UnityEngine.InputSystem;

namespace DrJones
{
    public sealed class CheatCodes : MonoBehaviour
    {
        const float MaxGap = 1.6f;

        string buffer = "";
        float lastAt = -10f;

        void Update()
        {
            var keyboard = Keyboard.current;
            var game = GameManager.Instance;
            if (keyboard == null || game == null)
                return;

            char? digit = ReadDigit(keyboard);
            if (digit == null)
                return;

            if (Time.unscaledTime - lastAt > MaxGap)
                buffer = "";
            lastAt = Time.unscaledTime;
            buffer += digit.Value;
            if (buffer.Length > 4)
                buffer = buffer.Substring(buffer.Length - 4);
            if (buffer.Length < 4)
                return;

            switch (buffer)
            {
                case "0000":
                    game.CheatGodMode();
                    buffer = "";
                    break;
                case "1111":
                    game.CheatSkipFirstLevel();
                    buffer = "";
                    break;
                case "2222":
                    game.CheatFastHammer();
                    buffer = "";
                    break;
                case "3333":
                    game.CheatWin();
                    buffer = "";
                    break;
            }
        }

        static char? ReadDigit(Keyboard keyboard)
        {
            if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
                return '0';
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                return '1';
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                return '2';
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                return '3';
            return null;
        }
    }
}
