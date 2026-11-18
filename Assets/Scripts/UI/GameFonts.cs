using UnityEngine;

namespace DrJones
{
    public static class GameFonts
    {
        static Font ui;
        static Font title;
        static Font Builtin => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Font Ui
        {
            get
            {
                if (ui == null)
                    ui = Resources.Load<Font>("modula");
                if (ui == null)
                    ui = Builtin;
                return ui;
            }
        }

        public static Font Title
        {
            get
            {
                if (title == null)
                    title = Resources.Load<Font>("RoughRunners-BLKmG");
                if (title == null)
                    title = Ui;
                return title;
            }
        }
    }
}
