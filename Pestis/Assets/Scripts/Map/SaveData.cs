using System;
using UnityEngine;

namespace Map
{
    [Serializable]
    public struct SaveData
    {
        public TextAsset MapBytesFile { get; private set; }

        public SaveData(int width, int height, int[] tileIndices)
        {
            byte[] mapData = new byte[8 + sizeof(int) * width * height];
            
            byte[] widthBytes = BitConverter.GetBytes(width);
            for (int i = 0; i < sizeof(int); ++i)
            {
                mapData[i] = widthBytes[i];
            }
            
            byte[] heightBytes = BitConverter.GetBytes(height);
            for (int i = 0; i < sizeof(int); ++i)
            {
                mapData[4 + i] = heightBytes[i];
            }
            
            for (int tileIndex = 0; tileIndex < tileIndices.Length; ++tileIndex)
            {
                byte[] currentTileBytes = BitConverter.GetBytes(tileIndices[tileIndex]);
                for (int byteIndex = 0; byteIndex < sizeof(int); ++byteIndex)
                {
                    mapData[8 + 4 * tileIndex + byteIndex] = currentTileBytes[byteIndex];
                }
            }

            MapBytesFile = new TextAsset(new ReadOnlySpan<byte>(mapData));
        }
    }
}