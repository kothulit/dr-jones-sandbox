using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DrJones.EditorTools
{
    [InitializeOnLoad]
    static class PlayProbe
    {
        static int frames;
        static int errors;
        static readonly StringBuilder log = new StringBuilder();

        static PlayProbe()
        {
            if (!SessionState.GetBool("DrJonesProbe", false))
                return;
            EditorApplication.update += Tick;
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception)
                {
                    errors++;
                    log.AppendLine("ERR " + msg);
                }
            };
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;
            frames++;
            var game = Object.FindAnyObjectByType<GameManager>();
            if (game == null || game.Built == null || game.Player == null)
                return;
            if (frames >= 60 && frames <= 360 && frames % 60 == 0)
            {
                var items = game.Built.Items;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] == null)
                        continue;
                    game.Player.transform.position = items[i].transform.position + Vector3.up * 0.18f;
                    break;
                }
            }

            if (frames == 420)
            {
                int launched = 0;
                for (int i = 0; i < game.Built.Boulders.Length; i++)
                {
                    var b = game.Built.Boulders[i];
                    if (b.IsLaunched)
                        launched++;
                    log.AppendLine("boulder " + i + " special=" + b.Special + " launched=" + b.IsLaunched + " rolling=" + b.IsRolling + " mult=" + b.SpeedMultiplier.ToString("0.00"));
                }

                var hud = Object.FindAnyObjectByType<ExplorationHud>();
                log.AppendLine("launched=" + launched + " objective=" + game.ObjectiveText + " hp=" + game.HealthCurrent + " exitRevealed=" + game.ExitRevealed + " hud=" + (hud != null));
                var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                int bad = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    var mat = all[i].sharedMaterial;
                    if (mat == null || mat.shader == null || mat.shader.name.Contains("Error"))
                        bad++;
                }

                log.AppendLine("renderers=" + all.Length + " bad=" + bad + " errors=" + errors);
                File.WriteAllText("Temp/drjones-play.txt", log.ToString());
                SessionState.SetBool("DrJonesProbe", false);
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
            }
        }
    }
}
