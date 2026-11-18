using UnityEngine;

namespace DrJones
{
    public static class VisualFactory
    {
        static Material particleMaterial;
        static readonly System.Collections.Generic.Dictionary<string, Material> cache = new System.Collections.Generic.Dictionary<string, Material>();

        public static void Bind(string name, Material material)
        {
            cache[name] = material;
        }

        public static Material M(string resourceName, Color color, Color emission, float emissionStrength)
        {
            if (cache.TryGetValue(resourceName, out var bound) && bound != null)
                return bound;
            var loaded = Resources.Load<Material>("Materials/" + resourceName);
            if (loaded != null)
            {
                cache[resourceName] = loaded;
                return loaded;
            }

            return Lit(color, emission, emissionStrength);
        }

        public static Material Lit(Color color, Color emission, float emissionStrength)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (emissionStrength > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission * emissionStrength);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_Metallic", emissionStrength > 1.5f ? 0.35f : 0f);
            return material;
        }

        public static Material ParticleMaterial(Color color)
        {
            if (particleMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                particleMaterial = new Material(shader);
            }

            var instance = new Material(particleMaterial);
            instance.color = color;
            return instance;
        }

        public static GameObject FloorTile(bool alternate)
        {
            var root = new GameObject("FloorTile");
            var cube = Cube(root.transform, new Vector3(0f, -0.08f, 0f), new Vector3(1.02f, 0.16f, 1.02f), M("Floor", new Color(0.72f, 0.58f, 0.4f), Color.black, 0f));
            cube.name = "Mesh";
            var renderer = cube.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return root;
        }

        public static GameObject WallBlock()
        {
            var root = new GameObject("WallBlock");
            var cube = Cube(root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.08f, 1.1f, 1.08f), M("Wall", new Color(0.72f, 0.55f, 0.38f), Color.black, 0f));
            cube.name = "Mesh";
            var renderer = cube.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            renderer.receiveShadows = false;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(0.84f, 1f, 0.84f);
            return root;
        }

        public static GameObject BreakableBlock()
        {
            var root = new GameObject("BreakableWall");
            var cube = Cube(root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.08f, 1.1f, 1.08f), M("Breakable", new Color(0.85f, 0.55f, 0.32f), new Color(0.7f, 0.28f, 0.08f), 0.45f));
            cube.name = "Mesh";
            cube.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(0.84f, 1f, 0.84f);
            return root;
        }

        public static GameObject Hat()
        {
            var root = new GameObject("Player");
            var brim = M("Hat", new Color(0.55f, 0.32f, 0.14f), Color.black, 0f);
            var crown = M("Hat", new Color(0.5f, 0.28f, 0.12f), Color.black, 0f);
            var band = M("HatBand", new Color(0.22f, 0.12f, 0.07f), Color.black, 0f);
            Cube(root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.82f, 0.05f, 0.82f), brim);
            Cube(root.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.42f, 0.2f, 0.42f), crown);
            Cube(root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.46f, 0.05f, 0.46f), band);
            var lightGo = new GameObject("Torch");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0.16f, 0.05f, 0.12f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.38f);
            light.range = 9.2f;
            light.intensity = 13f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.82f;
            lightGo.AddComponent<TorchFlicker>();
            var fillGo = new GameObject("TorchFill");
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.7f, 0.42f);
            fill.range = 13f;
            fill.intensity = 4.2f;
            fill.shadows = LightShadows.None;
            var spark = PointParticles(lightGo.transform, new Color(1f, 0.7f, 0.3f, 0.45f), 4f, 0.35f, 0.04f, true);
            spark.transform.localPosition = Vector3.zero;
            var body = root.AddComponent<SphereCollider>();
            body.center = new Vector3(0f, 0.05f, 0f);
            body.radius = 0.26f;
            return root;
        }

        public static Material BoulderGlow => M("SpecialBoulder", new Color(0.55f, 0.32f, 0.22f), new Color(1f, 0.4f, 0.08f), 1.5f);

        public static GameObject Boulder(bool special)
        {
            var root = new GameObject(special ? "SpecialBoulder" : "Boulder");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Mesh";
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localScale = Vector3.one * 0.82f;
            var renderer = sphere.GetComponent<Renderer>();
            renderer.sharedMaterial = M("Boulder", new Color(0.7f, 0.58f, 0.42f), Color.black, 0f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var collider = sphere.GetComponent<SphereCollider>();
            if (collider != null)
                collider.isTrigger = true;
            return root;
        }

        public static void PrepareHudModel(GameObject go)
        {
            if (go == null)
                return;
            var lights = go.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                    lights[i].enabled = false;
            }

            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sharedMaterial == null)
                    continue;
                var source = renderer.sharedMaterial;
                var mat = shader != null ? new Material(shader) : new Material(source);
                Color color = HudTint(source);
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static Color HudTint(Material source)
        {
            string name = source != null ? source.name : "";
            if (name.Contains("Heart"))
                return new Color(1f, 0.16f, 0.2f, 1f);
            if (name.Contains("Idol"))
                return new Color(1f, 0.9f, 0.42f, 1f);
            if (name.Contains("HammerHead") || name.Contains("Fragment"))
                return new Color(0.88f, 0.97f, 1f, 1f);
            if (name.Contains("Wood"))
                return new Color(0.9f, 0.68f, 0.42f, 1f);
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
            return Color.Lerp(color, Color.white, 0.4f);
        }

        public static GameObject Heart()
        {
            var root = new GameObject("Heart");
            var mat = M("Heart", new Color(0.9f, 0.12f, 0.14f), new Color(1f, 0.1f, 0.12f), 1.6f);
            Sphere(root.transform, new Vector3(-0.09f, 0.3f, 0.04f), 0.2f, mat);
            Sphere(root.transform, new Vector3(0.09f, 0.3f, 0.04f), 0.2f, mat);
            var tip = Cube(root.transform, new Vector3(0f, 0.3f, -0.08f), new Vector3(0.2f, 0.16f, 0.2f), mat);
            tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.25f, 0.25f);
            light.range = 1.3f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;
            return root;
        }

        public static GameObject Idol()
        {
            var root = new GameObject("FertilityIdol");
            var gold = M("Idol", new Color(1f, 0.78f, 0.28f), new Color(1f, 0.7f, 0.15f), 1.8f);
            Cube(root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.42f, 0.16f, 0.42f), gold);
            Cube(root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.3f, 0.14f, 0.3f), gold);
            Cube(root.transform, new Vector3(0f, 0.34f, 0f), new Vector3(0.18f, 0.12f, 0.18f), gold);
            Sphere(root.transform, new Vector3(0f, 0.48f, 0f), 0.14f, gold);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.3f);
            light.range = 1.4f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
            return root;
        }

        public static GameObject Fragment()
        {
            var root = new GameObject("ThorFragment");
            var mat = M("Fragment", new Color(0.55f, 0.9f, 1f), new Color(0.35f, 0.8f, 1f), 2f);
            Cube(root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.28f, 0.12f, 0.16f), mat);
            Cube(root.transform, new Vector3(0.12f, 0.22f, 0f), new Vector3(0.1f, 0.28f, 0.1f), mat);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.5f, 0.85f, 1f);
            light.range = 1.4f;
            light.intensity = 2.4f;
            light.shadows = LightShadows.None;
            return root;
        }

        public static GameObject Hammer()
        {
            var root = new GameObject("ThorHammer");
            var handle = M("Wood", new Color(0.72f, 0.52f, 0.32f), Color.black, 0f);
            var head = M("HammerHead", new Color(0.75f, 0.92f, 1f), new Color(0.45f, 0.85f, 1f), 2.2f);
            Cylinder(root.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.1f, 0.38f, 0.1f), handle);
            Cube(root.transform, new Vector3(0f, 0.72f, 0f), new Vector3(0.46f, 0.2f, 0.18f), head);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.6f, 0.9f, 1f);
            light.range = 4.5f;
            light.intensity = 6f;
            light.shadows = LightShadows.Hard;
            return root;
        }

        public static GameObject ExitMarker()
        {
            var root = new GameObject("Exit");
            var mat = M("Exit", new Color(0.45f, 1f, 0.55f), new Color(0.25f, 1f, 0.4f), 2.2f);
            Cylinder(root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.55f, 0.03f, 0.55f), mat);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.4f, 1f, 0.5f);
            light.range = 1.4f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            return root;
        }

        public static GameObject ClosingSlab()
        {
            var root = new GameObject("ClosingSlab");
            var cube = Cube(root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.12f, 1.16f, 1.12f), M("Wall", new Color(0.58f, 0.42f, 0.28f), Color.black, 0f));
            cube.name = "Mesh";
            cube.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            return root;
        }

        public static GameObject IndyHand()
        {
            var root = new GameObject("IndyHand");
            var sleeve = M("HatBand", new Color(0.38f, 0.18f, 0.1f), Color.black, 0f);
            var skin = Lit(new Color(0.82f, 0.58f, 0.42f), Color.black, 0f);
            Cube(root.transform, new Vector3(0f, 0.1f, -0.08f), new Vector3(0.28f, 0.16f, 0.22f), sleeve);
            Cube(root.transform, new Vector3(0f, 0.08f, 0.1f), new Vector3(0.32f, 0.08f, 0.22f), skin);
            float[] x = { -0.12f, -0.04f, 0.04f, 0.12f };
            for (int i = 0; i < x.Length; i++)
                Cube(root.transform, new Vector3(x[i], 0.08f, 0.24f), new Vector3(0.06f, 0.06f, 0.16f), skin);
            Cube(root.transform, new Vector3(-0.16f, 0.08f, 0.06f), new Vector3(0.06f, 0.06f, 0.12f), skin);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.5f);
            light.range = 1.4f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            return root;
        }

        public static ParticleSystem Dust(Transform parent)
        {
            return PointParticles(parent, new Color(0.65f, 0.58f, 0.45f, 0.45f), 18f, 0.55f, 0.16f, true);
        }

        public static void Burst(Vector3 position, Color color, int count)
        {
            var ps = PointParticles(null, color, 0f, 0.7f, 0.12f, false);
            ps.transform.position = position;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            ps.Emit(count);
            Object.Destroy(ps.gameObject, 1.4f);
        }

        public static ParticleSystem PointParticles(Transform parent, Color color, float rate, float life, float size, bool loop)
        {
            var go = new GameObject("Particles");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            }

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = true;
            main.startLifetime = life;
            main.startSpeed = 0.55f;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.gravityModifier = -0.15f;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.18f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = ParticleMaterial(color);
            if (!loop)
                ps.Stop();
            return ps;
        }

        public static GameObject Cube(Transform parent, Vector3 localPos, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            return cube;
        }

        static GameObject Cylinder(Transform parent, Vector3 localPos, Vector3 scale, Material material)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPos;
            cylinder.transform.localScale = scale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            return cylinder;
        }

        static GameObject Sphere(Transform parent, Vector3 localPos, float scale, Material material)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = localPos;
            sphere.transform.localScale = Vector3.one * scale;
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            return sphere;
        }
    }
}
