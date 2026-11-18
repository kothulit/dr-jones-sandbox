using System;
using UnityEngine;

namespace DrJones
{
    public static class LevelBuilder
    {
        public sealed class BuiltLevel
        {
            public PlayerController Player;
            public HealthSystem Health;
            public BreakableWall ExitWall;
            public GameObject ExitMarker;
            public ClosingSlab Gate;
            public BoulderController Special;
            public BoulderController[] Boulders;
            public Collectible[] Items;
            public HeartPickup[] Hearts;
            public Renderer[,] CellRenderers;
        }

        public static BuiltLevel Build(GridMap map, PrefabLibrary library, Transform parent, GameManager game, Action<Collectible> onPickup)
        {
            var built = new BuiltLevel();
            built.CellRenderers = new Renderer[map.Width, map.Height];
            float boulderY = 0.45f;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var kind = map.Get(cell);
                    if (kind == CellKind.Floor || kind == CellKind.Breakable)
                    {
                        var floor = Spawn(library != null ? library.FloorTile : null, () => VisualFactory.FloorTile(((x + y) & 1) == 0), cell, 0f, parent);
                        floor.name = "Floor";
                        floor.transform.rotation = Quaternion.Euler(0f, 90f * ((x * 7 + y * 13) & 3), 0f);
                        if (kind == CellKind.Floor)
                            built.CellRenderers[x, y] = floor.GetComponentInChildren<Renderer>();
                    }

                    if (kind == CellKind.Wall)
                    {
                        var wall = Spawn(library != null ? library.WallBlock : null, VisualFactory.WallBlock, cell, 0f, parent);
                        wall.name = "Wall";
                        built.CellRenderers[x, y] = wall.GetComponentInChildren<Renderer>();
                    }
                    else if (kind == CellKind.Breakable)
                    {
                        var block = Spawn(library != null ? library.BreakableWall : null, VisualFactory.BreakableBlock, cell, 0f, parent);
                        block.name = "Breakable";
                        built.ExitWall = block.GetComponent<BreakableWall>();
                        if (built.ExitWall == null)
                            built.ExitWall = block.AddComponent<BreakableWall>();
                    }
                }
            }

            var playerGo = Spawn(library != null ? library.Player : null, VisualFactory.Hat, map.PlayerStart, PlayerController.GroundY, parent);
            playerGo.name = "Player";
            playerGo.tag = "Player";
            built.Player = playerGo.GetComponent<PlayerController>();
            if (built.Player == null)
                built.Player = playerGo.AddComponent<PlayerController>();
            if (playerGo.GetComponent<HealthSystem>() == null)
                playerGo.AddComponent<HealthSystem>();
            if (playerGo.GetComponent<InvulnerabilitySystem>() == null)
                playerGo.AddComponent<InvulnerabilitySystem>();
            built.Health = playerGo.GetComponent<HealthSystem>();
            built.Player.Configure(game.PlayerSpeed, game.PlayerRadius, game.TorchRange, game.TorchIntensity);
            built.Health.Configure(game.MaxHealth);
            playerGo.GetComponent<InvulnerabilitySystem>().Configure(game.InvulnerabilityTime);

            built.Items = new Collectible[map.Collectibles.Count];
            for (int i = 0; i < map.Collectibles.Count; i++)
            {
                GameObject prefab = null;
                if (library != null)
                    prefab = map.Level == LevelId.BoulderTemple ? library.FertilityIdol : library.ThorFragment;
                var item = Spawn(prefab, map.Level == LevelId.BoulderTemple ? VisualFactory.Idol : VisualFactory.Fragment, map.Collectibles[i], 0.12f, parent);
                var collectible = item.GetComponent<Collectible>();
                if (collectible == null)
                    collectible = item.AddComponent<Collectible>();
                collectible.Setup(i, map.Level == LevelId.BoulderTemple ? CollectibleKind.FertilityIdol : CollectibleKind.ThorFragment);
                collectible.Picked += onPickup;
                built.Items[i] = collectible;
            }

            built.Hearts = new HeartPickup[map.Hearts.Count];
            for (int i = 0; i < map.Hearts.Count; i++)
            {
                var heart = Spawn(library != null ? library.Heart : null, VisualFactory.Heart, map.Hearts[i], 0.12f, parent);
                built.Hearts[i] = heart.GetComponent<HeartPickup>();
                if (built.Hearts[i] == null)
                    built.Hearts[i] = heart.AddComponent<HeartPickup>();
            }

            if (map.Level == LevelId.BoulderTemple)
            {
                built.Boulders = new BoulderController[map.Boulders.Count];
                for (int i = 0; i < map.Boulders.Count; i++)
                {
                    var def = map.Boulders[i];
                    GameObject prefab = library != null ? library.Boulder : null;
                    var go = Spawn(prefab, () => VisualFactory.Boulder(false), def.Idle, boulderY, parent);
                    var boulder = go.GetComponent<BoulderController>();
                    if (boulder == null)
                        boulder = go.AddComponent<BoulderController>();
                    if (def.Special)
                    {
                        boulder.ConfigureSpecial(
                            At(def.Idle, boulderY),
                            At(def.Entry, boulderY),
                            At(def.ChargeTo, boulderY),
                            game.PlayerSpeed,
                            built.ExitWall);
                        built.Special = boulder;
                    }
                    else
                    {
                        boulder.ConfigurePatrol(
                            At(def.PatrolA, boulderY),
                            At(def.PatrolB, boulderY),
                            At(def.Entry, boulderY),
                            At(def.Idle, boulderY),
                            game.PlayerSpeed * 1.75f);
                    }

                    built.Boulders[i] = boulder;
                }
            }

            var markerPrefab = library != null ? library.ExitMarker : null;
            built.ExitMarker = Spawn(markerPrefab, VisualFactory.ExitMarker, map.ExitCell, 0.02f, parent);
            built.ExitMarker.SetActive(false);
            return built;
        }

        public static LightningTrap SpawnLightning(LightningDef def, GridMap map, Transform parent, GameManager game)
        {
            int length = 0;
            var cursor = def.Origin + def.Direction;
            while (map.InBounds(cursor) && map.Get(cursor) == CellKind.Floor)
            {
                length++;
                cursor += def.Direction;
            }

            var go = new GameObject("LightningTrap");
            go.transform.SetParent(parent, false);
            var trap = go.AddComponent<LightningTrap>();
            Vector3 from = At(def.Origin, 0.4f);
            Vector3 to = from + new Vector3(def.Direction.x, 0f, def.Direction.y) * (length + 0.45f);
            trap.Setup(from, to, game.LightningDelay, game.LightningInterval, game.LightningWarning);
            return trap;
        }

        public static HammerFlight SpawnHammer(Vector2Int from, Vector2Int to, PrefabLibrary library, Transform parent, Action onArrive)
        {
            GameObject prefab = library != null ? library.ThorHammer : null;
            var go = Spawn(prefab, VisualFactory.Hammer, from, 0.2f, parent);
            var flight = go.GetComponent<HammerFlight>();
            if (flight == null)
                flight = go.AddComponent<HammerFlight>();
            flight.Launch(At(to, 0.55f), 8.5f, onArrive);
            return flight;
        }

        static GameObject Spawn(GameObject prefab, Func<GameObject> fallback, Vector2Int cell, float y, Transform parent)
        {
            GameObject go = prefab != null ? UnityEngine.Object.Instantiate(prefab) : fallback();
            go.transform.SetParent(parent, false);
            go.transform.position = At(cell, y);
            go.transform.rotation = Quaternion.identity;
            return go;
        }

        static Vector3 At(Vector2Int cell, float y)
        {
            return new Vector3(cell.x, y, cell.y);
        }
    }
}
