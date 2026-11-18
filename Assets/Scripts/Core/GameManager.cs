using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DrJones
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Game";

        [Header("Карта")]
        public int MapWidth = 41;
        public int MapHeight = 31;
        public int Seed;

        [Header("Персонаж")]
        public float PlayerSpeed = 5.2f;
        public float PlayerRadius = 0.26f;
        public int MaxHealth = 3;
        public float InvulnerabilityTime = 1f;
        public float TorchRange = 9.2f;
        public float TorchIntensity = 13f;

        [Header("Ловушки")]
        public float BoulderDashSpeed = 12f;
        [Tooltip("Прибавка к скорости всех валунов за каждый подобранный идол (0.1 = +10%).")]
        public float BoulderRampPerIdol = 0.1f;
        public float LightningDelay = 2f;
        public float LightningInterval = 2f;
        public float LightningWarning = 0.4f;

        [Header("Правила")]
        public float LevelSeconds = 180f;
        public float ViewWidth = 20f;
        public PrefabLibrary Library;

        public PlayerController Player { get; private set; }
        public LevelTimer Timer { get; private set; }
        public string ObjectiveText { get; private set; } = "";
        public int HealthCurrent => health != null ? health.Current : MaxHealth;
        public int HealthMax => health != null ? health.Max : MaxHealth;
        public bool CanPause => phase == Phase.Play && playerControl;
        public bool SimulationFrozen => phase != Phase.Play || !playerControl;
        public GridMap Map => map;
        public LevelBuilder.BuiltLevel Built => built;
        public bool ExitRevealed => revealStarted;
        public bool Invincible { get; private set; }
        public bool UsedCheat { get; private set; }
        public const string BestTimeKey = "DrJones.BestTime";
        public Difficulty Difficulty { get; private set; }
        public bool CanContinueAfterFail => Difficulty != Difficulty.Raider;

        struct ResumeState
        {
            public bool Active;
            public LevelId Level;
            public int Seed;
            public float Total;
            public bool UsedCheat;
            public bool Invincible;
            public bool FastHammer;
        }

        static ResumeState resume;

        MonoBehaviour cinematicActor;

        public bool IsCinematicActor(MonoBehaviour actor)
        {
            return actor != null && cinematicActor == actor;
        }

        enum Phase
        {
            Boot,
            Play,
            Cinematic,
            Transition,
            Victory,
            Defeat
        }

        Phase phase = Phase.Boot;
        bool playerControl;
        bool exitReady;
        bool ending;
        bool revealStarted;
        bool scrambleActive;
        bool fastHammer;
        float timeAtLevelStart;
        int collected;
        GridMap map;
        GameObject levelRoot;
        HealthSystem health;
        LevelBuilder.BuiltLevel built;
        TopDownCamera follow;
        PauseManager pause;
        UIManager ui;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Difficulty = GameSettings.Current;
            LevelSeconds = GameSettings.SecondsPerLevel(Difficulty);
            if (resume.Active && resume.Seed != 0)
                Seed = resume.Seed;
            if (Seed == 0)
                Seed = System.Environment.TickCount & int.MaxValue;
            if (Seed == 0)
                Seed = 1;

            if (GetComponent<AudioManager>() == null)
                gameObject.AddComponent<AudioManager>();
            Timer = GetComponent<LevelTimer>();
            if (Timer == null)
                Timer = gameObject.AddComponent<LevelTimer>();
            pause = GetComponent<PauseManager>();
            if (pause == null)
                pause = gameObject.AddComponent<PauseManager>();
            ui = GetComponent<UIManager>();
            if (ui == null)
                ui = gameObject.AddComponent<UIManager>();
            if (GetComponent<ExplorationHud>() == null)
                gameObject.AddComponent<ExplorationHud>();
            if (GetComponent<CheatCodes>() == null)
                gameObject.AddComponent<CheatCodes>();

            Time.timeScale = 1f;
            AudioListener.pause = false;
            EnsureCamera();
            pause.Bind(EnsureVolume());
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.012f, 0.011f, 0.016f);
            RenderSettings.fog = false;
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                    lights[i].intensity = 0.02f;
            }
        }

        void Start()
        {
            ui.SetFade(1f);
            var start = LevelId.BoulderTemple;
            if (resume.Active)
            {
                start = resume.Level;
                UsedCheat = resume.UsedCheat;
                Invincible = resume.Invincible;
                fastHammer = resume.FastHammer;
                if (Timer != null)
                    Timer.SetTotal(resume.Total);
                resume = default;
            }

            BeginLevel(start);
            StartCoroutine(Intro());
        }

        void OnDestroy()
        {
            if (health != null)
                health.Died -= OnDied;
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && phase != Phase.Boot)
            {
                ExitToMenu();
                return;
            }

            if (scrambleActive && ui != null)
                ui.SetEscapeCountdown(true, built != null && built.Gate != null ? built.Gate.Remaining : 0f);

            if (phase != Phase.Play || !playerControl || Player == null || map == null)
                return;
            if (pause != null && pause.IsPaused)
                return;

            if (exitReady && Flat(Player.transform.position, map.ExitCell) < 0.55f)
            {
                CompleteLevel();
                return;
            }

            if (scrambleActive && built != null && built.Gate != null && built.Gate.Sealed)
                Fail(FailReason.Sealed);
        }

        public void Fail(FailReason reason)
        {
            if (ending || phase == Phase.Victory || phase == Phase.Defeat)
                return;
            ending = true;
            scrambleActive = false;
            phase = Phase.Defeat;
            playerControl = false;
            if (Player != null)
                Player.ControlEnabled = false;
            if (ui != null)
                ui.SetEscapeCountdown(false);
            Timer.Stop();
            Time.timeScale = 0f;
            AudioListener.pause = false;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopStoneGrind();
                AudioManager.Instance.PlayDefeat();
            }

            ui.ShowDefeat(reason, CanContinueAfterFail);
        }

        public void Restart()
        {
            resume = default;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(GameScene);
        }

        public void ContinueLevel()
        {
            resume = new ResumeState
            {
                Active = true,
                Level = map != null ? map.Level : LevelId.BoulderTemple,
                Seed = Seed,
                Total = timeAtLevelStart,
                UsedCheat = UsedCheat,
                Invincible = Invincible,
                FastHammer = fastHammer
            };
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(GameScene);
        }

        public void ExitToMenu()
        {
            resume = default;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(MenuScene);
        }

        public void CheatGodMode()
        {
            if (ending || phase == Phase.Victory || phase == Phase.Defeat)
                return;
            UsedCheat = true;
            Invincible = true;
            if (health != null)
                health.Restore();
            PlayCheatAck();
        }

        public void CheatSkipFirstLevel()
        {
            if (ending || map == null || map.Level != LevelId.BoulderTemple)
                return;
            if (phase == Phase.Victory || phase == Phase.Defeat || phase == Phase.Transition)
                return;
            UsedCheat = true;
            ResumeAfterCheat();
            PlayCheatAck();
            phase = Phase.Play;
            CompleteLevel();
        }

        public void CheatFastHammer()
        {
            if (ending || phase == Phase.Victory || phase == Phase.Defeat)
                return;
            UsedCheat = true;
            fastHammer = true;
            ResumeAfterCheat();
            PlayCheatAck();
            if (map == null)
                return;
            if (map.Level == LevelId.BoulderTemple)
            {
                if (phase == Phase.Transition)
                    return;
                phase = Phase.Play;
                CompleteLevel();
                return;
            }

            RefreshObjective();
            if (!revealStarted && collected >= FragmentsNeeded)
                StartCoroutine(RevealExitHammer(-1, false));
        }

        public void CheatWin()
        {
            if (ending || phase == Phase.Victory || phase == Phase.Defeat)
                return;
            UsedCheat = true;
            ResumeAfterCheat();
            PlayCheatAck();
            Win();
        }

        int FragmentsNeeded
        {
            get
            {
                if (map == null)
                    return 1;
                if (map.Level != LevelId.ThorTemple)
                    return map.Collectibles.Count;
                return fastHammer ? 1 : map.Collectibles.Count;
            }
        }

        void ResumeAfterCheat()
        {
            if (pause != null && pause.IsPaused)
                pause.SetPaused(false);
            cinematicActor = null;
        }

        static void PlayCheatAck()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUi();
        }

        IEnumerator Intro()
        {
            yield return ui.FadeTo(0f, 0.4f);
            ArmPlay();
        }

        IEnumerator ToSecondLevel()
        {
            yield return ui.FadeTo(1f, 0.35f);
            BeginLevel(LevelId.ThorTemple);
            yield return ui.FadeTo(0f, 0.35f);
            ArmPlay();
        }

        void BeginLevel(LevelId id)
        {
            if (health != null)
                health.Died -= OnDied;
            if (levelRoot != null)
                Destroy(levelRoot);

            levelRoot = new GameObject(id == LevelId.BoulderTemple ? "BoulderTemple" : "ThorTemple");
            int levelSeed = unchecked(Seed + (id == LevelId.BoulderTemple ? 17 : 7919));
            if (levelSeed == 0)
                levelSeed = 1;
            map = LevelGenerator.Generate(id, MapWidth, MapHeight, levelSeed);
            built = LevelBuilder.Build(map, Library, levelRoot.transform, this, OnCollected);
            Player = built.Player;
            health = built.Health;
            health.Died += OnDied;
            if (follow != null)
            {
                follow.Target = Player.transform;
                follow.ViewWidth = ViewWidth;
            }

            collected = 0;
            exitReady = false;
            revealStarted = false;
            scrambleActive = false;
            timeAtLevelStart = Timer != null ? Timer.Total : 0f;
            cinematicActor = null;
            if (ui != null)
                ui.SetEscapeCountdown(false);
            playerControl = false;
            Player.ControlEnabled = false;
            Timer.Stop();
            phase = Phase.Play;
            RefreshObjective();
            ui.ShowHud(true);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayLevel(id);
            Debug.Log("Dr. Jones " + id + " seed " + map.Seed);
        }

        void OnCollected(Collectible item)
        {
            if (phase != Phase.Play || map == null)
                return;
            collected++;
            RefreshObjective();
            if (map.Level == LevelId.BoulderTemple)
            {
                if (collected >= map.Collectibles.Count)
                    StartCoroutine(RevealExitBoulder());
                else
                {
                    var nearest = NearestIdleBoulder(item.transform.position);
                    if (nearest != null)
                        nearest.Activate();
                }
            }
            else if (item.Index >= 0 && item.Index < map.Lightnings.Count)
            {
                if (collected >= FragmentsNeeded)
                    StartCoroutine(RevealExitHammer(item.Index));
                else
                    LevelBuilder.SpawnLightning(map.Lightnings[item.Index], map, levelRoot.transform, this);
            }
        }

        BoulderController NearestIdleBoulder(Vector3 from)
        {
            BoulderController best = null;
            float bestDistance = float.MaxValue;
            if (built.Boulders == null)
                return null;
            for (int i = 0; i < built.Boulders.Length; i++)
            {
                var boulder = built.Boulders[i];
                if (boulder == null || boulder.Special || boulder.IsLaunched)
                    continue;
                Vector3 offset = boulder.transform.position - from;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = boulder;
                }
            }

            return best;
        }

        IEnumerator RevealExitBoulder()
        {
            if (revealStarted)
                yield break;
            revealStarted = true;
            var boulder = built != null ? built.Special : null;
            BeginCinematic(boulder);
            if (follow != null && boulder != null)
            {
                follow.ReleaseHold();
                follow.Target = boulder.transform;
            }

            if (boulder != null)
                boulder.Activate();
            while (boulder != null && boulder.IsOpeningExit)
                yield return null;
            ShowExitMarker();
            yield return new WaitForSeconds(1.05f);
            yield return ReturnCamera();
            if (boulder != null)
                boulder.BeginChase(PlayerSpeed * 0.8f);
            exitReady = true;
            EndCinematic();
        }

        IEnumerator RevealExitHammer(int lightningIndex, bool spawnTrap = true)
        {
            if (revealStarted)
                yield break;
            revealStarted = true;
            BeginCinematic(null);
            LightningTrap trap = null;
            if (spawnTrap && lightningIndex >= 0 && lightningIndex < map.Lightnings.Count)
                trap = LevelBuilder.SpawnLightning(map.Lightnings[lightningIndex], map, levelRoot.transform, this);
            if (follow != null)
            {
                follow.ReleaseHold();
                if (trap != null)
                    follow.Target = trap.transform;
                else if (built != null && built.ExitWall != null)
                    follow.Target = built.ExitWall.transform;
            }

            yield return new WaitForSeconds(trap != null ? 0.7f : 0.2f);
            var hammer = LevelBuilder.SpawnHammer(map.HammerFrom, map.HammerTo, Library, levelRoot.transform, () =>
            {
                if (built != null && built.ExitWall != null)
                    built.ExitWall.Break();
                ShowExitMarker();
                BeginEscapeScramble();
            });
            if (follow != null && hammer != null)
                follow.Target = hammer.transform;
            while (hammer != null && !hammer.Arrived)
                yield return null;
            yield return new WaitForSeconds(1.05f);
            yield return ReturnCamera();
            exitReady = true;
            EndCinematic();
        }

        void BeginCinematic(MonoBehaviour actor)
        {
            cinematicActor = actor;
            phase = Phase.Cinematic;
            playerControl = false;
            if (Player != null)
                Player.ControlEnabled = false;
        }

        void EndCinematic()
        {
            cinematicActor = null;
            if (ending)
                return;
            phase = Phase.Play;
            playerControl = true;
            if (Player != null)
                Player.ControlEnabled = true;
        }

        IEnumerator ReturnCamera()
        {
            if (follow == null || Player == null)
                yield break;
            follow.Target = Player.transform;
            yield return follow.Pan(Player.transform.position, 1.15f);
            follow.ReleaseHold();
        }

        void ShowExitMarker()
        {
            if (built != null && built.ExitMarker != null)
                built.ExitMarker.SetActive(true);
        }

        void CompleteLevel()
        {
            if (phase != Phase.Play)
                return;
            if (map.Level == LevelId.BoulderTemple)
            {
                phase = Phase.Transition;
                playerControl = false;
                Player.ControlEnabled = false;
                Timer.Stop();
                StartCoroutine(ToSecondLevel());
                return;
            }

            StartCoroutine(HatGrab());
        }

        void BeginEscapeScramble()
        {
            if (map == null || Player == null || built == null)
                return;
            var from = new Vector2Int(Mathf.RoundToInt(Player.transform.position.x), Mathf.RoundToInt(Player.transform.position.z));
            int steps = Mathf.Max(1, map.PathLength(from, map.ExitCell));
            float seconds = Mathf.Clamp((steps / Mathf.Max(0.5f, PlayerSpeed) * 1.35f + 3.2f) * 1.5f, 12f, 33f);
            var gate = VisualFactory.ClosingSlab();
            gate.transform.SetParent(levelRoot != null ? levelRoot.transform : transform, true);
            gate.transform.position = GridMap.World(map.ExitCell, 1.9f);
            built.Gate = gate.AddComponent<ClosingSlab>();
            built.Gate.Begin(seconds);
            scrambleActive = true;
            Timer.Stop();
            if (ui != null)
                ui.SetEscapeCountdown(true, seconds);
            if (follow != null)
                follow.Shake(0.08f, 0.45f);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayWarning();
                AudioManager.Instance.PlayStoneGrind();
            }
        }

        IEnumerator HatGrab()
        {
            if (ending)
                yield break;
            scrambleActive = false;
            if (ui != null)
                ui.SetEscapeCountdown(false);
            BeginCinematic(null);
            Timer.Stop();
            if (Player != null)
            {
                Player.ControlEnabled = false;
                Player.HoldPose = true;
                var colliders = Player.GetComponentsInChildren<Collider>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                        colliders[i].enabled = false;
                }
            }

            if (built != null && built.Gate != null)
                built.Gate.HoldGap();
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopStoneGrind();

            Vector3 inward = map != null ? map.ExitInward() : Vector3.forward;
            Vector3 hide = map != null ? GridMap.World(map.ExitCell, PlayerController.GroundY) : Player.transform.position;
            Vector3 bounce = Player.transform.position + inward * 1.25f;
            bounce.y = PlayerController.GroundY;
            Vector3 startScale = Player.transform.localScale;
            yield return MoveHat(bounce, 0.28f, startScale, new Vector3(1.12f, 0.72f, 1.12f));
            yield return MoveHat(bounce, 0.12f, Player.transform.localScale, startScale);
            yield return new WaitForSeconds(2f);

            var hand = VisualFactory.IndyHand();
            hand.transform.SetParent(levelRoot != null ? levelRoot.transform : transform, true);
            Vector3 grab = bounce + Vector3.up * 0.06f;
            Vector3 beyond = hide - inward + Vector3.up * 0.08f;
            hand.transform.position = beyond;
            hand.transform.rotation = Quaternion.LookRotation(inward, Vector3.up);
            yield return Slide(hand.transform, grab, 0.28f);
            if (follow != null)
            {
                follow.ReleaseHold();
                follow.Target = Player.transform;
                follow.Shake(0.07f, 0.25f);
            }

            float pull = 0f;
            Vector3 hatFrom = Player.transform.position;
            Vector3 handFrom = hand.transform.position;
            while (pull < 0.38f)
            {
                pull += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pull / 0.38f));
                Player.transform.position = Vector3.Lerp(hatFrom, beyond, u);
                hand.transform.position = Vector3.Lerp(handFrom, beyond, u);
                yield return null;
            }

            if (built != null && built.Gate != null)
                yield return built.Gate.Slam(0.32f);
            HideRenderers(Player.gameObject);
            HideRenderers(hand);
            if (follow != null)
                follow.Shake(0.14f, 0.35f);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayEnd();
            yield return new WaitForSeconds(2f);
            yield return ui.FadeTo(1f, 0.45f);
            Win();
        }

        static void HideRenderers(GameObject go)
        {
            if (go == null)
                return;
            var renderers = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = false;
            }

            var lights = go.GetComponentsInChildren<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                    lights[i].enabled = false;
            }
        }

        IEnumerator MoveHat(Vector3 target, float time, Vector3 fromScale, Vector3 toScale)
        {
            Vector3 from = Player.transform.position;
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time));
                Player.transform.position = Vector3.Lerp(from, target, u);
                Player.transform.localScale = Vector3.Lerp(fromScale, toScale, u);
                yield return null;
            }

            Player.transform.position = target;
            Player.transform.localScale = toScale;
        }

        static IEnumerator Slide(Transform target, Vector3 world, float time)
        {
            Vector3 from = target.position;
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time));
                target.position = Vector3.Lerp(from, world, u);
                yield return null;
            }

            target.position = world;
        }

        void ArmPlay()
        {
            if (phase != Phase.Play || health == null || health.IsDead)
                return;
            Timer.BeginLevel(LevelSeconds);
            playerControl = true;
            if (Player != null)
                Player.ControlEnabled = true;
        }

        void OnDied()
        {
            Fail(FailReason.Health);
        }

        void Win()
        {
            if (ending)
                return;
            ending = true;
            scrambleActive = false;
            phase = Phase.Victory;
            playerControl = false;
            if (Player != null)
            {
                Player.ControlEnabled = false;
                Player.HoldPose = true;
            }

            if (ui != null)
            {
                ui.SetEscapeCountdown(false);
                ui.SetFade(0f);
            }

            if (AudioManager.Instance != null)
                AudioManager.Instance.StopStoneGrind();
            Timer.Stop();
            Time.timeScale = 0f;
            AudioListener.pause = false;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayVictory();
            if (UsedCheat)
            {
                ui.ShowVictory(0f, 0f, false, true);
                return;
            }

            float total = Timer.Total;
            string key = GameSettings.BestTimeKey(Difficulty);
            float best = PlayerPrefs.GetFloat(key, 0f);
            bool record = best <= 0f || total < best;
            if (record)
            {
                PlayerPrefs.SetFloat(key, total);
                PlayerPrefs.Save();
                best = total;
            }

            ui.ShowVictory(total, best, record, false);
        }

        void RefreshObjective()
        {
            int need = FragmentsNeeded;
            ObjectiveText = collected + "/" + need;
            if (collected >= need)
                ObjectiveText += "  к выходу!";
        }

        static float Flat(Vector3 world, Vector2Int cell)
        {
            float dx = world.x - cell.x;
            float dz = world.z - cell.y;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.orthographic = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.allowHDR = true;
            var extra = cam.GetComponent<UniversalAdditionalCameraData>();
            if (extra == null)
                extra = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            extra.renderPostProcessing = true;
            follow = cam.GetComponent<TopDownCamera>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<TopDownCamera>();
            follow.ViewWidth = ViewWidth;
            follow.Height = 16f;
        }

        Volume EnsureVolume()
        {
            var volume = FindAnyObjectByType<Volume>();
            if (volume == null)
            {
                var go = new GameObject("Post");
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
            }

            if (volume.profile == null)
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            if (!volume.profile.TryGet(out Vignette vignette))
                vignette = volume.profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.42f);
            if (!volume.profile.TryGet(out DepthOfField depth))
                depth = volume.profile.Add<DepthOfField>();
            depth.active = false;
            if (!volume.profile.TryGet(out Bloom bloom))
                bloom = volume.profile.Add<Bloom>();
            bloom.active = true;
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(0.42f);
            bloom.scatter.Override(0.55f);
            return volume;
        }
    }
}
