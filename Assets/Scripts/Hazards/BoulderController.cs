using System.Collections.Generic;
using UnityEngine;

namespace DrJones
{
    public sealed class BoulderController : MonoBehaviour
    {
        public bool Special;
        public float Speed = 5.2f;
        public float Radius = 0.42f;
        public float SpeedMultiplier = 1f;
        public float HearingDistance = 14f;

        enum Mode
        {
            Idle,
            Enter,
            Patrol,
            Charge,
            AwaitChase,
            Chase
        }

        Mode mode = Mode.Idle;
        Vector3 pointA;
        Vector3 pointB;
        Vector3 entry;
        Vector3 chargeTarget;
        bool toB = true;
        bool solid = true;
        BreakableWall wall;
        ParticleSystem dust;
        AudioSource roll;
        Collider[] colliders;
        Transform mesh;
        Vector3 lastPosition;
        readonly List<Vector2Int> path = new List<Vector2Int>();
        int pathIndex;
        Vector2Int chaseCell = new Vector2Int(int.MinValue, 0);

        public bool IsLaunched => mode != Mode.Idle;
        public bool IsOpeningExit => Special && (mode == Mode.Enter || mode == Mode.Charge);
        public bool IsRolling => solid && (mode == Mode.Enter || mode == Mode.Patrol || mode == Mode.Charge || mode == Mode.Chase);

        public void ConfigurePatrol(Vector3 a, Vector3 b, Vector3 entryPoint, Vector3 idle, float speed)
        {
            pointA = a;
            pointB = b;
            entry = entryPoint;
            Speed = speed;
            transform.position = idle;
            lastPosition = idle;
            Special = false;
            toB = (entryPoint - a).sqrMagnitude < (entryPoint - b).sqrMagnitude;
        }

        public void ConfigureSpecial(Vector3 idle, Vector3 entryPoint, Vector3 charge, float speed, BreakableWall target)
        {
            Special = true;
            Speed = speed;
            entry = entryPoint;
            chargeTarget = charge;
            wall = target;
            transform.position = idle;
            lastPosition = idle;
        }

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].isTrigger = true;
            }

            var sphere = GetComponentInChildren<SphereCollider>();
            if (sphere != null)
                Radius = sphere.radius * Mathf.Max(sphere.transform.lossyScale.x, 0.01f);
            var renderer = GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
                mesh = renderer.transform;
            dust = VisualFactory.Dust(transform);
            dust.Stop();
            roll = gameObject.AddComponent<AudioSource>();
            roll.loop = true;
            roll.spatialBlend = 0.35f;
            roll.dopplerLevel = 0f;
            roll.volume = 0f;
            roll.pitch = 0.85f;
            roll.playOnAwake = false;
        }

        void Start()
        {
            if (AudioManager.Instance != null)
                roll.clip = AudioManager.Instance.BoulderLoop;
            if (roll.clip != null)
                roll.time = Random.Range(0f, roll.clip.length * 0.9f);
        }

        public void Activate()
        {
            if (mode != Mode.Idle)
                return;
            mode = Mode.Enter;
            if (Special)
                Ignite();
            if (dust != null)
                dust.Play();
            if (roll != null && roll.clip != null)
                roll.Play();
        }

        public void BeginChase(float speed)
        {
            Speed = speed;
            SpeedMultiplier = 1f;
            mode = Mode.Chase;
            path.Clear();
            pathIndex = 0;
            chaseCell = new Vector2Int(int.MinValue, 0);
            SetSolid(true);
            if (dust != null && !dust.isPlaying)
                dust.Play();
            if (roll != null && roll.clip != null && !roll.isPlaying)
                roll.Play();
        }

        void Ignite()
        {
            var glow = VisualFactory.BoulderGlow;
            var renderer = GetComponentInChildren<MeshRenderer>();
            if (renderer != null && glow != null)
                renderer.sharedMaterial = glow;
            var light = GetComponent<Light>();
            if (light == null)
                light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.45f, 0.15f);
            light.range = 2.4f;
            light.intensity = 5f;
            light.shadows = LightShadows.None;
            light.enabled = true;
        }

        void Update()
        {
            var game = GameManager.Instance;
            bool frozen = Time.timeScale <= 0f || (game != null && game.SimulationFrozen && !game.IsCinematicActor(this));
            UpdateSound(frozen);
            if (frozen)
                return;

            float speed = Speed * SpeedMultiplier;
            switch (mode)
            {
                case Mode.Enter:
                    if (Approach(entry, speed))
                        mode = Special ? Mode.Charge : Mode.Patrol;
                    break;
                case Mode.Patrol:
                    Patrol(speed);
                    break;
                case Mode.Charge:
                    if (Approach(chargeTarget, speed * 1.15f))
                    {
                        if (wall != null)
                            wall.Break();
                        mode = Mode.AwaitChase;
                    }
                    break;
                case Mode.Chase:
                    Chase(speed);
                    break;
            }

            SpinMesh();
            if (IsRolling && (game == null || game.Player == null || game.Player.ControlEnabled))
                TryHitPlayer();
        }

        void Chase(float speed)
        {
            var game = GameManager.Instance;
            if (game == null || game.Player == null || game.Map == null)
                return;
            Vector2Int from = CellOf(transform.position);
            Vector2Int to = CellOf(game.Player.transform.position);
            if (path.Count == 0 || pathIndex >= path.Count || to != chaseCell)
            {
                chaseCell = to;
                BuildPath(game.Map, from, to);
                pathIndex = 0;
            }

            if (path.Count == 0)
            {
                Vector2Int step = GreedyStep(game.Map, from, to);
                Vector3 fallback = step == from
                    ? game.Player.transform.position
                    : GridMap.World(step, transform.position.y);
                fallback.y = transform.position.y;
                Approach(fallback, speed);
                return;
            }

            Vector3 waypoint = GridMap.World(path[pathIndex], transform.position.y);
            if (Approach(waypoint, speed))
                pathIndex++;
        }

        void BuildPath(GridMap map, Vector2Int from, Vector2Int to)
        {
            path.Clear();
            if (from == to)
                return;
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            prev[from] = from;
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell == to)
                    break;
                for (int i = 0; i < 4; i++)
                {
                    var next = cell + dirs[i];
                    if (prev.ContainsKey(next) || !CanRoll(map, next))
                        continue;
                    prev[next] = cell;
                    queue.Enqueue(next);
                }
            }

            if (!prev.ContainsKey(to))
                return;
            var cursor = to;
            var stack = new List<Vector2Int>();
            while (cursor != from)
            {
                stack.Add(cursor);
                cursor = prev[cursor];
            }

            for (int i = stack.Count - 1; i >= 0; i--)
                path.Add(stack[i]);
        }

        static Vector2Int GreedyStep(GridMap map, Vector2Int from, Vector2Int to)
        {
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            Vector2Int best = from;
            int bestDistance = Distance(from, to);
            for (int i = 0; i < 4; i++)
            {
                var next = from + dirs[i];
                if (!CanRoll(map, next))
                    continue;
                int distance = Distance(next, to);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = next;
                }
            }

            return best;
        }

        static int Distance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        static bool CanRoll(GridMap map, Vector2Int cell)
        {
            if (!map.InBounds(cell))
                return false;
            if (cell == map.ExitCell)
                return true;
            return map.Get(cell) == CellKind.Floor;
        }

        static Vector2Int CellOf(Vector3 world)
        {
            return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
        }

        void UpdateSound(bool frozen)
        {
            if (roll == null || !roll.isPlaying)
                return;
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || frozen)
            {
                roll.volume = 0f;
                return;
            }

            Vector3 offset = transform.position - player.transform.position;
            offset.y = 0f;
            float closeness = Mathf.Clamp01(1f - offset.magnitude / HearingDistance);
            roll.volume = closeness * closeness * 0.45f;
            roll.panStereo = Mathf.Clamp(offset.x / 8f, -0.85f, 0.85f);
            roll.pitch = Mathf.Lerp(0.72f, 0.92f, Mathf.Clamp01(Speed * SpeedMultiplier / 6f));
        }

        void SpinMesh()
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (mesh == null || distance < 0.0001f || distance > 2f)
                return;
            Vector3 axis = Vector3.Cross(Vector3.up, delta / distance);
            mesh.Rotate(axis, distance / Mathf.Max(0.1f, Radius) * Mathf.Rad2Deg, Space.World);
        }

        void Patrol(float speed)
        {
            Vector3 target = toB ? pointB : pointA;
            if (Approach(target, speed))
                toB = !toB;
        }

        bool Approach(Vector3 target, float speed)
        {
            float left = speed * Time.deltaTime;
            while (left > 0f)
            {
                float step = Mathf.Min(0.18f, left);
                transform.position = Vector3.MoveTowards(transform.position, target, step);
                left -= step;
                if ((transform.position - target).sqrMagnitude < 0.0004f)
                {
                    transform.position = target;
                    return true;
                }
            }

            return false;
        }

        void TryHitPlayer()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null)
                return;
            Vector3 a = transform.position;
            Vector3 b = player.Center;
            a.y = 0f;
            b.y = 0f;
            if ((a - b).sqrMagnitude > (Radius + player.HitRadius) * (Radius + player.HitRadius))
                return;
            var health = player.GetComponent<HealthSystem>();
            var inv = player.GetComponent<InvulnerabilitySystem>();
            if (health != null && health.TryDamage(1, inv) && AudioManager.Instance != null)
                AudioManager.Instance.PlayHurt();
        }

        void SetSolid(bool value)
        {
            solid = value;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = value;
            }
        }
    }
}
