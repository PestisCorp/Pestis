using UnityEditor;
using UnityEngine;
using System.IO;

namespace Tools
{
    public class SpriteImporter : EditorWindow
    {
        private string _spritePath = "Assets/Sprites/rat_components/out";

        [MenuItem("Tools/Import Sprites")]
        public static void ShowWindow()
        {
            GetWindow(typeof(SpriteImporter));
        }

        public void OnGUI()
        {
            GUILayout.Label("Import Images Settings", EditorStyles.boldLabel);
           _spritePath = EditorGUILayout.TextField("Folder Path:", _spritePath);

            if (GUILayout.Button("Import Images"))
            {
                ImportAllImages();
            }
        }
        
        private void ImportAllImages()
        {
            if (!Directory.Exists(_spritePath))
            {
                Debug.LogError("Directory does not exist: " + _spritePath);
                return;
            }

            var imageFiles = Directory.GetFiles(_spritePath, "*.*", SearchOption.AllDirectories);
            foreach (var file in imageFiles)
            {
                if (!file.EndsWith(".png")) continue;
                var assetPath = file.Replace(Application.dataPath, "Assets");
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (!importer) continue;
                var settings = new TextureImporterSettings
                {
                    textureType = TextureImporterType.Sprite,
                    spriteMeshType = SpriteMeshType.Tight,
                    spriteGenerateFallbackPhysicsShape = true,
                    spriteExtrude = 1,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    spritePixelsPerUnit = 64,
                    aniso = 1
                };
                importer.SetTextureSettings(settings);
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            AssetDatabase.Refresh();
        }
    }
}