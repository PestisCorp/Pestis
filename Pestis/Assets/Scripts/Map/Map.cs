using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Map
{
    [CreateAssetMenu(fileName = "MapData", menuName = "ScriptableObjects/MapData", order = 1)]
    public class MapScriptableObject : ScriptableObject
    {
        internal const int WaterValue = int.MaxValue;
        public int width = 256;
        public int height = 256;
        public int saa = 0;
        public TileBase water;
        public TileBase[] landTiles;
        public BiomeClass[] BiomeClasses;
        [HideInInspector] public int[] tileIndices;
        [HideInInspector] public byte[] mapBytes;
#if UNITY_EDITOR
        public void Save()
        {
            SaveData data = new(width, height, tileIndices);
            mapBytes = data.MapBytesFile.bytes;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            Debug.Log("Map saved");
        }

        [ContextMenu("Debug Biomes")]
        private void DebugBiomes()
        {
            Debug.Log($"BiomeClasses count: {BiomeClasses?.Length}");
        }
#endif
    }
}