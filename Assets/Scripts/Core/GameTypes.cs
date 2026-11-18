using UnityEngine;

namespace DrJones
{
    public enum LevelId
    {
        BoulderTemple = 0,
        ThorTemple = 1
    }

    public enum CellKind
    {
        Wall = 0,
        Floor = 1,
        Breakable = 2
    }

    public enum FailReason
    {
        Health,
        Time,
        Sealed
    }

    public enum Difficulty
    {
        Novice = 0,
        Professional = 1,
        Raider = 2
    }

    public static class GameSettings
    {
        public const string DifficultyKey = "DrJones.Difficulty";

        public static Difficulty Current
        {
            get => (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 0), 0, 2);
            set
            {
                PlayerPrefs.SetInt(DifficultyKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static float SecondsPerLevel(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Professional: return 120f;
                case Difficulty.Raider: return 60f;
                default: return 180f;
            }
        }

        public static string Title(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Professional: return "Профессионал";
                case Difficulty.Raider: return "Расхититель";
                default: return "Новичок";
            }
        }

        public static string BestTimeKey(Difficulty difficulty)
        {
            return "DrJones.BestTime." + difficulty;
        }
    }

    public struct BoulderDef
    {
        public UnityEngine.Vector2Int Idle;
        public UnityEngine.Vector2Int Entry;
        public UnityEngine.Vector2Int PatrolA;
        public UnityEngine.Vector2Int PatrolB;
        public UnityEngine.Vector2Int ChargeTo;
        public UnityEngine.Vector2Int DashTo;
        public float Speed;
        public bool Special;
    }

    public struct LightningDef
    {
        public UnityEngine.Vector2Int Origin;
        public UnityEngine.Vector2Int Direction;
    }
}
