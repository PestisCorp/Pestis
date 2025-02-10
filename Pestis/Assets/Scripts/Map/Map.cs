using System;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Map
{
    [CreateAssetMenu(fileName = "MapData", menuName = "ScriptableObjects/MapData", order = 1)]
    public class MapScriptableObject : ScriptableObject
    {
        public int width = 1024;
        public int height = 1024;
        public TileBase water;
        [HideInInspector] public int[] tileIndices;
        [HideInInspector] public byte[] mapBytes;
        
        
        internal const int WaterValue = Int32.MaxValue;
        
        

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
}