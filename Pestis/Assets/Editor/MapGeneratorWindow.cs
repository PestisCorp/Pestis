using Map;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Tilemaps;

// Credit: https://medium.com/nerd-for-tech/how-to-create-a-list-in-a-custom-editor-window-in-unity-e6856e78adfc
namespace Editor
{
    public class MapGeneratorWindow : EditorWindow
    {
        private const string HelpText = "Cannot find 'Land Biomes List' component on any GameObject in the scene";
        private static readonly Rect HelpRect = new(0f, 0f, 400f, 100f);
        private static readonly Rect ListRect = new(Vector2.zero, Vector2.one * 400f);
        private MapBehaviour _map;
        private Generator _mapGenerator;
        private ReorderableList _reorderableBiomeList;
        private SerializedObject _serializedBiomeList;

        private void OnEnable()
        {
            _map = FindFirstObjectByType<MapBehaviour>();
            if (!_map) return;
            _mapGenerator = new Generator();
            _mapGenerator.Map = _map;


            _serializedBiomeList = new SerializedObject(_map.mapObject);
            _reorderableBiomeList = new ReorderableList(_serializedBiomeList,
                _serializedBiomeList.FindProperty("landTiles"), true, true, true, true);
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


            _reorderableBiomeList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Land tiles");
            _reorderableBiomeList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.height = EditorGUIUtility.singleLineHeight;

                var tileLabel = new GUIContent($"Tile {index}");
                EditorGUI.PropertyField(rect,
                    _reorderableBiomeList.serializedProperty.GetArrayElementAtIndex(index), tileLabel);
            };


            GUILayout.Space(_reorderableBiomeList.GetHeight());

            EditorGUILayout.BeginVertical();

            _map.tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", _map.tilemap, typeof(Tilemap), true);
            _map.mapObject.width = EditorGUILayout.IntField("Width", _map.mapObject.width);
            _map.mapObject.height = EditorGUILayout.IntField("Height", _map.mapObject.height);
            _map.mapObject.water =
                (TileBase)EditorGUILayout.ObjectField("Water tile", _map.mapObject.water, typeof(TileBase), true);
            _mapGenerator.VoronoiFrequency =
                EditorGUILayout.FloatField("Voronoi frequency", _mapGenerator.VoronoiFrequency);
            _mapGenerator.RandomWalkSteps =
                EditorGUILayout.IntField("Random walk steps", _mapGenerator.RandomWalkSteps);
            _mapGenerator.Smoothing = EditorGUILayout.IntField("Water-land smoothing", _mapGenerator.Smoothing);

            EditorGUILayout.EndVertical();

            GUILayout.Space(20f);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Map")) _mapGenerator.GenerateMap();

            if (GUILayout.Button("Save Map"))
            {
                _map.mapObject.Save();
                _map.tilemap.ClearAllTiles();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        [MenuItem("Window/Map Generator")]
        public static void ShowWindow()
        {
            GetWindow<MapGeneratorWindow>("Map Generator");
        }
    }
}