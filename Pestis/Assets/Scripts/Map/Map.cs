using UnityEngine;
using UnityEngine.Tilemaps;
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
        public int width = 1024;
        public int height = 1024;
        public TileBase water;
        public TileBase[] landTiles;
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
    }
#endif
}