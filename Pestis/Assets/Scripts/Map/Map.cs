using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Fusion;
using ProceduralToolkit;
using ProceduralToolkit.FastNoiseLib;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Map
{
    public class Map : MonoBehaviour
    {
        public Tilemap tilemap;
        public int width = 1024;
        public int height = 1024;
        public TileBase water;
        public LandBiomesList landBiomes;
        [HideInInspector] public int[] savedTiles;
    
        private static readonly string FilePath = $"{Application.dataPath}/Scripts/Map/SavedMap.dat";
        
        // Credit: https://www.red-gate.com/simple-talk/development/dotnet-development/saving-game-data-with-unity/
        public void Save()
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(FilePath);
            SaveData data = new();
            data.width = width;
            data.height = height;
            data.water = water;
            data.landBiomes = landBiomes;
            data.tiles = savedTiles;
            bf.Serialize(file, data);
            file.Close();
            Debug.Log("Map saved");
        }
    
        public void Load()
        {
            if (File.Exists(FilePath))
            {
                BinaryFormatter bf = new();
                FileStream file = File.Open(FilePath, FileMode.Open);
                SaveData data = (SaveData)bf.Deserialize(file);
                file.Close();
                width = data.width;
                height = data.height;
                water = data.water;
                landBiomes = data.landBiomes;
                savedTiles = data.tiles;
                Debug.Log("Map save loaded");
            }
            else
            {
                Debug.LogError("No map save found");
            }
        }
        
        public void Reset()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
                Debug.Log("Map save reset");
            }
            else
            {
                Debug.LogError("No map save found to reset");
            }
        }
    }
    
    [Serializable]
    public struct SaveData
    {
        public int width;
        public int height;
        public TileBase water;
        public LandBiomesList landBiomes;
        public int[] tiles;
    }
}