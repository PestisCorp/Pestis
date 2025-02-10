using System;
using System.IO;
using Unity.Collections;
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
        [HideInInspector] public TextAsset savedMap;
        [HideInInspector] public int[] tileIndices;
        
        internal const int WaterValue = Int32.MaxValue;

        private void Start()
        {
            LoadRuntime();
        }
        
        public void Save()
        {
            SaveData data = new(width, height, tileIndices);
            savedMap = data.MapBytesFile;
            File.WriteAllBytes("Assets/Map/map.map", savedMap.bytes);
            Debug.Log("Map saved");
        }

        public void LoadEditor()
        {
            byte[] fileBytes = File.ReadAllBytes("Assets/Map/map.map");
            savedMap = new TextAsset(fileBytes);
        }
    
        public void LoadRuntime()
        {
            tilemap.transform.position = new Vector2(0, -height / 4.0f);
            
            if (savedMap)
            {
                var biomeList = landBiomes.GetList();
                
                width = BitConverter.ToInt32(savedMap.bytes, 0);
                height = BitConverter.ToInt32(savedMap.bytes, 4);

                var mapBytes = savedMap.GetData<byte>();
                
                int startIndex = 8;
                for (int x = 0; x < width; ++x)
                {
                    for (int y = 0; y < height; ++y)
                    {
                        int currentTileIndex = BitConverter.ToInt32(mapBytes.Slice(startIndex, 4).ToArray(), 0);
                        if (currentTileIndex == WaterValue)
                        {
                            tilemap.SetTile(new Vector3Int(x, y), water);
                        }
                        else
                        {
                            tilemap.SetTile(new Vector3Int(x, y), biomeList[currentTileIndex]);
                        }

                        startIndex += 4;
                    }
                }
                
                Debug.Log("Map loaded");
            }
            else
            {
                Debug.LogError("No map file found");
            }
        }
    }
}