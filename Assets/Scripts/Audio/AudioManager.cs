using UnityEngine;

namespace DrJones
{
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public AudioClip BoulderLoop { get; private set; }

        AudioSource music;
        AudioSource sfx;
        AudioSource grind;
        AudioClip menu;
        AudioClip level1;
        AudioClip level2;
        AudioClip victory;
        AudioClip defeat;
        AudioClip footstep;
        AudioClip pickup;
        AudioClip hurt;
        AudioClip lightning;
        AudioClip wall;
        AudioClip hammer;
        AudioClip ui;
        AudioClip warning;
        AudioClip stoneGrind;
        AudioClip endSting;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.volume = 0.22f;
            music.playOnAwake = false;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.volume = 0.7f;
            grind = gameObject.AddComponent<AudioSource>();
            grind.playOnAwake = false;
            grind.loop = true;
            grind.volume = 0.48f;
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Build()
        {
            menu = Track("Menu", MenuLead, MenuBass, 0.22f);
            level1 = Track("Level1", BoulderLead, BoulderBass, 0.13f);
            level2 = Track("Level2", ThorLead, ThorBass, 0.2f);
            victory = Track("Victory", VictoryLead, VictoryBass, 0.15f);
            defeat = Track("Defeat", DefeatLead, DefeatBass, 0.36f);
            footstep = Sfx("Step") ?? ChiptuneSynth.SoftStep("Step");
            pickup = Sfx("Pickup") ?? ChiptuneSynth.Arpeggio("Pickup", new[] { 72, 76, 79 });
            hurt = Sfx("Hurt") ?? ChiptuneSynth.Sweep("Hurt", 240f, 70f, 0.22f);
            lightning = Sfx("Lightning") ?? ChiptuneSynth.Noise("Lightning", 0.22f, 0.55f, false, 0.85f);
            wall = Sfx("Break") ?? ChiptuneSynth.Noise("Break", 0.32f, 0.6f, false, 0.55f);
            hammer = ChiptuneSynth.Sweep("Hammer", 180f, 720f, 0.45f);
            ui = ChiptuneSynth.Blip("Ui", 880f, 0.06f);
            warning = ChiptuneSynth.Sweep("Warn", 90f, 50f, 0.45f);
            stoneGrind = Sfx("Closing") ?? ChiptuneSynth.StoneGrind("StoneGrind", 2.8f);
            endSting = Sfx("End");
            BoulderLoop = Sfx("BoulderRoll") ?? ChiptuneSynth.Rumble("Roll", 4f);
        }

        public void PlayMenu() => PlayMusic(Resolve("Menu", menu), true);
        public void PlayLevel(LevelId level) => PlayMusic(Resolve(level == LevelId.BoulderTemple ? "Level1" : "Level2", level == LevelId.BoulderTemple ? level1 : level2), true);
        public void PlayVictory() => PlayMusic(Resolve("Victory", victory), false);
        public void PlayDefeat() => PlayMusic(Resolve("Defeat", defeat), false);

        static AudioClip Track(string id, int[] lead, int[] bass, float beat) => ChiptuneSynth.Arrange(id, lead, bass, beat, true);

        static AudioClip Sfx(string id)
        {
            var clip = Resources.Load<AudioClip>("Sfx/" + id);
            return clip != null ? clip : null;
        }

        static AudioClip Resolve(string id, AudioClip generated)
        {
            var custom = Resources.Load<AudioClip>("Music/" + id);
            return custom != null ? custom : generated;
        }

        static readonly int[] MenuLead =
        {
            69, 65, 62, 65, 67, 69, 72, 69, 67, 65, 62, 60, 62, 65, 69, 67,
            65, 64, 62, 65, 69, 72, 74, 72, 69, 67, 65, 62, 60, 62, 65, 69,
            72, 69, 65, 62, 67, 69, 72, 74, 72, 69, 67, 65, 64, 62, 60, 57,
            62, 65, 69, 67, 65, 62, 60, 62, 65, 69, 72, 69, 67, 65, 62, 65
        };
        static readonly int[] MenuBass =
        {
            50, 50, 53, 53, 45, 45, 48, 48, 50, 50, 43, 43, 45, 45, 41, 41,
            50, 50, 46, 46, 45, 45, 48, 48, 43, 43, 45, 45, 41, 41, 38, 38
        };
        static readonly int[] BoulderLead =
        {
            64, 67, 64, 60, 64, 67, 72, 67, 64, 60, 57, 60, 64, 67, 69, 67,
            64, 62, 64, 67, 72, 76, 72, 67, 64, 67, 64, 60, 62, 64, 67, 64,
            72, 69, 67, 64, 67, 72, 76, 72, 69, 67, 64, 60, 64, 67, 72, 64,
            60, 64, 67, 69, 67, 64, 62, 60, 57, 60, 64, 67, 64, 60, 57, 52,
            67, 64, 67, 72, 76, 74, 72, 67, 69, 72, 67, 64, 62, 64, 67, 60
        };
        static readonly int[] BoulderBass =
        {
            40, 40, 47, 47, 36, 36, 43, 43, 38, 38, 45, 45, 40, 40, 36, 36,
            35, 35, 38, 38, 40, 40, 43, 43, 36, 36, 40, 40, 31, 31, 36, 36
        };
        static readonly int[] ThorLead =
        {
            64, 60, 67, 72, 71, 67, 64, 60, 67, 71, 74, 72, 67, 64, 60, 64,
            72, 76, 74, 71, 67, 64, 67, 71, 74, 72, 67, 64, 60, 62, 64, 67,
            59, 62, 67, 71, 74, 71, 67, 62, 64, 67, 71, 72, 67, 64, 60, 64,
            72, 67, 64, 60, 67, 71, 67, 64, 62, 59, 62, 67, 64, 60, 57, 52
        };
        static readonly int[] ThorBass =
        {
            40, 40, 43, 43, 35, 35, 38, 38, 40, 40, 36, 36, 43, 43, 40, 40,
            35, 35, 38, 38, 31, 31, 35, 35, 36, 36, 40, 40, 28, 28, 31, 31
        };
        static readonly int[] VictoryLead =
        {
            72, 76, 79, 84, 79, 76, 84, 88, 84, 79, 76, 72, 76, 79, 84, 79
        };
        static readonly int[] VictoryBass = { 48, 48, 43, 43, 41, 41, 48, 48 };
        static readonly int[] DefeatLead =
        {
            64, 62, 60, 59, 57, 55, 53, 52, 50, 48, 47, 45, 43, 45, 47, 52
        };
        static readonly int[] DefeatBass = { 40, 40, 38, 38, 36, 36, 35, 35 };
        public void PlayFootstep() => Play(footstep, 0.16f);
        public void PlayPickup() => Play(pickup, 0.6f);
        public void PlayHurt() => Play(hurt, 0.7f);
        public void PlayLightning() => Play(lightning, 0.55f);
        public void PlayBreak() => Play(wall, 0.7f);
        public void PlayHammer() => Play(hammer, 0.65f);
        public void PlayUi() => Play(ui, 0.45f);
        public void PlayWarning() => Play(warning, 0.7f);
        public void PlayEnd() => Play(endSting, 0.85f);

        public void PlayStoneGrind()
        {
            if (grind == null || stoneGrind == null)
                return;
            grind.clip = stoneGrind;
            if (!grind.isPlaying)
                grind.Play();
        }

        public void StopStoneGrind()
        {
            if (grind != null && grind.isPlaying)
                grind.Stop();
        }

        void PlayMusic(AudioClip clip, bool loop)
        {
            if (clip == null)
                return;
            music.loop = loop;
            if (music.clip == clip && music.isPlaying)
                return;
            music.clip = clip;
            music.Play();
        }

        void Play(AudioClip clip, float volume)
        {
            if (clip != null)
                sfx.PlayOneShot(clip, volume);
        }
    }
}
