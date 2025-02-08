using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditorInternal;

// Credit: https://medium.com/nerd-for-tech/how-to-create-a-list-in-a-custom-editor-window-in-unity-e6856e78adfc
namespace Editor
{
    public class MapGeneratorWindow : EditorWindow
    {
        private Map.Map _map;
        private Map.Generator _mapGenerator;
        private SerializedObject _serializedBiomeList;
        private ReorderableList _reorderableBiomeList;
        private const string HelpText = "Cannot find 'Land Biomes List' component on any GameObject in the scene";
        private static readonly Rect HelpRect = new(0f, 0f, 400f, 100f);
        private static readonly Rect ListRect = new(Vector2.zero, Vector2.one * 400f);

        [MenuItem("Window/Map Generator")]
        public static void ShowWindow()
        {
            GetWindow<MapGeneratorWindow>("Map Generator");
        }

        private void OnEnable()
        {
            _map = FindFirstObjectByType<Map.Map>();
            if (!_map) return;
            _mapGenerator = new Map.Generator();
            _mapGenerator.Map = _map;
            _map.landBiomes = FindFirstObjectByType<Map.LandBiomesList>();
            _map.tileIndices = new int[_map.width * _map.height];

            if (_map.landBiomes)
            {
                _serializedBiomeList = new SerializedObject(_map.landBiomes);
                _reorderableBiomeList = new ReorderableList(_serializedBiomeList,
                    _serializedBiomeList.FindProperty("landTiles"), true, true, true, true);
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            if (_serializedBiomeList == null)
            {
                EditorGUI.HelpBox(HelpRect, HelpText, MessageType.Warning);
                return;
            }

            _serializedBiomeList.Update();
            _reorderableBiomeList.DoList(ListRect);
            _serializedBiomeList.ApplyModifiedProperties();

            if (_map.landBiomes)
            {
                _reorderableBiomeList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Land tiles");
                _reorderableBiomeList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;

                    GUIContent tileLabel = new GUIContent($"Tile {index}");
                    EditorGUI.PropertyField(rect,
                        _reorderableBiomeList.serializedProperty.GetArrayElementAtIndex(index), tileLabel);
                };
            }

            GUILayout.Space(_reorderableBiomeList.GetHeight());

            EditorGUILayout.BeginVertical();

            _map.tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", _map.tilemap, typeof(Tilemap), true);
            _map.width = EditorGUILayout.IntField("Width", _map.width);
            _map.height = EditorGUILayout.IntField("Height", _map.height);
            _map.water = (TileBase)EditorGUILayout.ObjectField("Water tile", _map.water, typeof(TileBase), true);
            _mapGenerator.VoronoiFrequency =
                EditorGUILayout.FloatField("Voronoi frequency", _mapGenerator.VoronoiFrequency);
            _mapGenerator.RandomWalkSteps =
                EditorGUILayout.IntField("Random walk steps", _mapGenerator.RandomWalkSteps);
            _mapGenerator.Smoothing = EditorGUILayout.IntField("Water-land smoothing", _mapGenerator.Smoothing);

            EditorGUILayout.EndVertical();

            GUILayout.Space(20f);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Map"))
            {
                _mapGenerator.GenerateMap();
            }

            if (GUILayout.Button("Save Map"))
            {
                _map.Save();
            }

            if (GUILayout.Button("Load Map"))
            {
                _map.Load();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}