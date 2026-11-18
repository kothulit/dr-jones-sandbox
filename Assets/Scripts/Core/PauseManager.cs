using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DrJones
{
    public sealed class PauseManager : MonoBehaviour
    {
        public bool IsPaused { get; private set; }

        DepthOfField depthOfField;

        public void Bind(Volume volume)
        {
            if (volume != null && volume.profile != null)
                volume.profile.TryGet(out depthOfField);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame)
                return;
            if (GameManager.Instance == null || !GameManager.Instance.CanPause)
                return;
            SetPaused(!IsPaused);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (depthOfField != null)
            {
                depthOfField.active = paused;
                if (paused)
                {
                    depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
                    depthOfField.gaussianStart.Override(0.1f);
                    depthOfField.gaussianEnd.Override(2.5f);
                    depthOfField.gaussianMaxRadius.Override(1.5f);
                }
            }

            if (UIManager.Instance != null)
                UIManager.Instance.ShowPause(paused);
        }
    }
}
