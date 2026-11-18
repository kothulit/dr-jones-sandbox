using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DrJones.EditorTools
{
    public static class DrJonesProjectSetup
    {
        const string PrefabDir = "Assets/Prefabs";
        const string LibraryPath = "Assets/Settings/DrJonesPrefabs.asset";

        [MenuItem("Dr. Jones/Setup Project")]
        public static void Setup()
        {
            int maps = LevelGenerator.SelfTest(20);
            Debug.Log("Dr. Jones: проверено карт " + maps);

            Directory.CreateDirectory(PrefabDir);
            ImportAudio();
            BuildMaterials();
            var library = AssetDatabase.LoadAssetAtPath<PrefabLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<PrefabLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.FloorTile = SavePrefab(VisualFactory.FloorTile(false), PrefabDir + "/FloorTile.prefab");
            library.WallBlock = SavePrefab(VisualFactory.WallBlock(), PrefabDir + "/WallBlock.prefab");
            library.BreakableWall = SavePrefab(VisualFactory.BreakableBlock(), PrefabDir + "/BreakableWall.prefab");
            library.Player = SavePrefab(VisualFactory.Hat(), PrefabDir + "/Player.prefab");
            library.Boulder = SavePrefab(VisualFactory.Boulder(false), PrefabDir + "/Boulder.prefab");
            library.SpecialBoulder = SavePrefab(VisualFactory.Boulder(true), PrefabDir + "/SpecialBoulder.prefab");
            library.FertilityIdol = SavePrefab(VisualFactory.Idol(), PrefabDir + "/FertilityIdol.prefab");
            library.ThorFragment = SavePrefab(VisualFactory.Fragment(), PrefabDir + "/ThorFragment.prefab");
            library.ThorHammer = SavePrefab(VisualFactory.Hammer(), PrefabDir + "/ThorHammer.prefab");
            library.ExitMarker = SavePrefab(VisualFactory.ExitMarker(), PrefabDir + "/Exit.prefab");
            library.Heart = SavePrefab(VisualFactory.Heart(), PrefabDir + "/Heart.prefab");
            EditorUtility.SetDirty(library);

            CreateMenuScene();
            CreateGameScene(library);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true)
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            Debug.Log("Dr. Jones: сцены готовы. Нажмите Play.");
        }

        static GameObject SavePrefab(GameObject temp, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        static void CreateMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var menu = new GameObject("MainMenu");
            menu.AddComponent<MainMenuController>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }

        static void CreateGameScene(PrefabLibrary library)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var systems = new GameObject("Game");
            var game = systems.AddComponent<GameManager>();
            game.Library = library;

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            cameraGo.AddComponent<AudioListener>();
            var extra = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            extra.renderPostProcessing = true;
            var follow = cameraGo.AddComponent<TopDownCamera>();
            follow.ViewWidth = 20f;
            follow.Height = 16f;
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
        }

        static void ImportAudio()
        {
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                    continue;
                bool music = path.Contains("/Music/");
                var settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = music ? 0.6f : 0.8f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !music;
                importer.SaveAndReimport();
            }
        }

        static void BuildMaterials()
        {
            Directory.CreateDirectory("Assets/Resources/Materials");
            AssetDatabase.Refresh();
            var floor = Textured("Floor", Tex("Floor"), new Color(1f, 0.92f, 0.78f), 0f, 0.16f, Color.black, 0f);
            var wall = Textured("Wall", Tex("Wall"), new Color(1f, 0.88f, 0.72f), 0f, 0.12f, Color.black, 0f);
            EnableMemoryGlow(floor);
            var boulder = Textured("Boulder", Tex("Boulder"), Color.white, 0f, 0.2f, Color.black, 0f);
            if (Exists("Breakable"))
                Textured("Breakable", Tex("Breakable"), new Color(1f, 0.85f, 0.7f), 0f, 0.15f, new Color(0.85f, 0.32f, 0.08f), 0.35f);
            else
                Tint("Breakable", wall, new Color(1f, 0.72f, 0.5f), new Color(0.85f, 0.32f, 0.08f), 0.7f, 0.05f, 0.2f);
            Tint("SpecialBoulder", boulder, new Color(1f, 0.62f, 0.42f), new Color(1f, 0.35f, 0.05f), 1.4f, 0.05f, 0.28f);
            Textured("Idol", Exists("Gold") ? Tex("Gold") : Tex("Metal"), new Color(1f, 0.85f, 0.45f), 1f, 0.6f, new Color(1f, 0.62f, 0.12f), 1.2f);
            Textured("Fragment", Tex("Metal"), new Color(0.45f, 0.9f, 1f), 0.85f, 0.7f, new Color(0.3f, 0.75f, 1f), 1.8f);
            Textured("HammerHead", Tex("Metal"), new Color(0.7f, 0.92f, 1f), 0.9f, 0.72f, new Color(0.35f, 0.8f, 1f), 2f);
            Textured("Wood", Tex("Wood"), Color.white, 0f, 0.25f, Color.black, 0f);
            if (Exists("Leather"))
                Textured("Hat", Tex("Leather"), new Color(0.85f, 0.62f, 0.42f), 0f, 0.3f, Color.black, 0f);
            else
                Textured("Hat", Tex("Wood"), new Color(0.55f, 0.32f, 0.14f), 0f, 0.25f, Color.black, 0f);
            if (Exists("Fabric"))
                Textured("HatBand", Tex("Fabric"), new Color(0.35f, 0.25f, 0.2f), 0f, 0.15f, Color.black, 0f);
            else
                Textured("HatBand", Tex("Wood"), new Color(0.22f, 0.12f, 0.07f), 0f, 0.2f, Color.black, 0f);
            if (Exists("Fabric"))
                Textured("Heart", Tex("Fabric"), new Color(0.95f, 0.18f, 0.16f), 0f, 0.35f, new Color(1f, 0.1f, 0.12f), 1.1f);
            else
                Flat("Heart", new Color(0.9f, 0.12f, 0.14f), new Color(1f, 0.1f, 0.12f), 1.6f, 0f, 0.55f);
            if (Exists("Exit"))
            {
                var exit = Textured("Exit", Tex("Exit"), new Color(0.55f, 1f, 0.6f), 0f, 0.3f, new Color(0.2f, 1f, 0.35f), 1.6f);
                exit.SetTexture("_EmissionMap", exit.GetTexture("_BaseMap"));
                EditorUtility.SetDirty(exit);
            }
            else
            {
                Flat("Exit", new Color(0.35f, 1f, 0.5f), new Color(0.2f, 1f, 0.35f), 2.2f, 0f, 0.4f);
            }
        }

        static void EnableMemoryGlow(Material material)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
        }

        static bool Exists(string name)
        {
            return File.Exists("Assets/Textures/Jungle/" + name + "_Color.jpg");
        }

        static string Tex(string name)
        {
            return Exists(name) ? "Assets/Textures/Jungle/" + name : "Assets/Textures/" + name;
        }

        static Material Textured(string name, string texturePrefix, Color tint, float metallic, float smoothness, Color emission, float emissionMul)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var color = ImportTexture(texturePrefix + "_Color.jpg", false);
            var normal = ImportTexture(texturePrefix + "_Normal.jpg", true);
            if (color != null)
                material.SetTexture("_BaseMap", color);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            Apply(material, tint, metallic, smoothness, emission, emissionMul);
            return SaveMaterial(name, material);
        }

        static Material Tint(string name, Material source, Color tint, Color emission, float emissionMul, float metallic, float smoothness)
        {
            var material = new Material(source);
            Apply(material, tint, metallic, smoothness, emission, emissionMul);
            return SaveMaterial(name, material);
        }

        static Material Flat(string name, Color tint, Color emission, float emissionMul, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            Apply(material, tint, metallic, smoothness, emission, emissionMul);
            return SaveMaterial(name, material);
        }

        static void Apply(Material material, Color tint, float metallic, float smoothness, Color emission, float emissionMul)
        {
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emissionMul > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission * emissionMul);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        static Material SaveMaterial(string name, Material material)
        {
            var path = "Assets/Resources/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            VisualFactory.Bind(name, material);
            return material;
        }

        static Texture2D ImportTexture(string path, bool normal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != type)
                {
                    importer.textureType = type;
                    dirty = true;
                }

                if (!normal && importer.sRGBTexture != true)
                {
                    importer.sRGBTexture = true;
                    dirty = true;
                }

                if (dirty)
                    importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
