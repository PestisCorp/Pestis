using System;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Map
{
    public class MapBehaviour : MonoBehaviour
    {
        public MapScriptableObject mapObject;
        public Tilemap tilemap;

        public void Start()
        {
            LoadRuntime();
        }

        public void LoadRuntime()
        {
            tilemap.transform.position = new Vector2(0, -mapObject.height / 4.0f);

            if (mapObject.mapBytes.Count() != 0)
            {
                var biomeList = mapObject.landTiles;

                var mapBytes = new NativeArray<byte>(mapObject.mapBytes, Allocator.Persistent);

                mapObject.width = BitConverter.ToInt32(mapBytes.Slice(0, 4).ToArray(), 0);
                mapObject.height = BitConverter.ToInt32(mapBytes.Slice(4, 4).ToArray(), 0);


                var startIndex = 8;
                for (var x = 0; x < mapObject.width; ++x)
                for (var y = 0; y < mapObject.height; ++y)
                {
                    var currentTileIndex = BitConverter.ToInt32(mapBytes.Slice(startIndex, 4).ToArray(), 0);
                    if (currentTileIndex == MapScriptableObject.WaterValue)
                        tilemap.SetTile(new Vector3Int(x, y), mapObject.water);
                    else
                        tilemap.SetTile(new Vector3Int(x, y), biomeList[currentTileIndex]);

                    startIndex += 4;
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