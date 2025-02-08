using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
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
        [HideInInspector] public int[] tileIndices;
    
        private static readonly string FilePath = $"{Application.dataPath}/Scripts/Map/SavedMap.dat";
        
        // Credit: https://www.red-gate.com/simple-talk/development/dotnet-development/saving-game-data-with-unity/
        public void Save()
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(FilePath);
            SaveData data = new(width, height, tileIndices);
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
                tileIndices = data.tiles;
                for (int x = 0; x < width; ++x)
                {
                    for (int y = 0; y < height; ++y)
                    {
                        if (tileIndices[y * height + x] == -2)
                        {
                            tilemap.SetTile(new Vector3Int(x, y), water);
                        }
                        else
                        {
                            tilemap.SetTile(new Vector3Int(x, y), landBiomes.GetList()[tileIndices[y * height + x]]);
                        }
                    }
                }
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
        public int[] tiles;
        
        public SaveData(int width, int height, int[] tiles)
        {
            this.width = width;
            this.height = height;
            this.tiles = tiles;
        }
    }
}