using System;
using System.Linq;
using Combat;
using Fusion;
using Horde;
using Human;
using JetBrains.Annotations;
using Objectives;
using Players;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace POI
{
    public enum POIType
    {
        Farm,
        Lab,
        City,
        Camp,
        Van
    }

    public class PoiController : NetworkBehaviour
    {
        private static readonly ProfilerMarker s_RemoveController = new("RPCPOI.RemoveController");
        private static readonly ProfilerMarker s_ChangeController = new("RPCPOI.ChangeController");

        private static readonly ProfilerMarker s_UnstationHorde = new("RPCPOI.UnstationHorde");

        private static readonly ProfilerMarker s_Attack = new("RPCPOI.Attack");

        private static readonly ProfilerMarker s_EventCombatOver = new("RPCPOI.EventCombatOver");
        public ParticleSystem[] captureEffect;

        public PatrolController patrolController;

        public uint boidPoisIndex;
        private Camera _camera;
        private float _cheesePerTick;

        private POIType _poiType;

        private Sprite captureFlag;
        private GameObject flagObject;
        public float CheesePerSecond => _cheesePerTick / Runner.DeltaTime;

        public Collider2D Collider { get; private set; }

        public Player ControlledBy
        {
            get
            {
                try
                {
                    var bla = ControlledByNetworked.Id;
                    return ControlledByNetworked;
                }
                catch (Exception e) // Previous controller doesn't exist anymore
                {
                    ControlledByNetworked = null;
                    return null;
                }
            }
            private set => ControlledByNetworked = value;
        }

        [Networked]
        [OnChangedRender(nameof(UpdateFlag))]
        private Player ControlledByNetworked { get; set; }

        [Networked] [Capacity(4)] public NetworkLinkedList<HordeController> StationedHordes { get; } = default;

        [Networked] [CanBeNull] public CombatController Combat { get; private set; }

        private float TimeWhenPoiAbandoned { get; set; }

        public void Awake()
        {
            if (name.Contains("Lab")) _poiType = POIType.Lab;
            if (name.Contains("Farm")) _poiType = POIType.Farm;
            if (name.Contains("City")) _poiType = POIType.City;
            if (name.Contains("camp")) _poiType = POIType.Camp;
            if (name.Contains("Van")) _poiType = POIType.Van;
            
            // Create a new GameObject for the icon
            if (!flagObject)
            {
                // create canvas to display icon
                var canvasGO = new GameObject("Canvas");
                canvasGO.transform.SetParent(transform);

                switch (_poiType)
                {
                    case POIType.Van:
                        canvasGO.transform.localPosition = new Vector3(2.07f, 1.1f, 0);
                        break;
                    case POIType.Farm:
                        canvasGO.transform.localPosition = new Vector3(-0.05f, 1.95f, 0);
                        break;
                    case POIType.Camp:
                        canvasGO.transform.localPosition = new Vector3(-0.19f, 1.68f, 0);
                        break;
                    case POIType.Lab:
                        canvasGO.transform.localPosition = new Vector3(1.47f, 2.84f, 0);
                        break;
                    case POIType.City:
                        canvasGO.transform.localPosition = new Vector3(0.85f, 4.04f, 0);
                        break;
                    default:
                        canvasGO.transform.localPosition = new Vector3(0, 2f, 0);
                        break;
                }

                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingLayerName = "UI&Icons";

                var canvasScaler = canvasGO.AddComponent<CanvasScaler>();

                flagObject = new GameObject("CaptureFlagIcon");
                flagObject.transform.SetParent(canvasGO.transform); // Attach to canvas

                var rectTransform = flagObject.AddComponent<RectTransform>();
                rectTransform.localPosition = Vector3.zero;
                rectTransform.sizeDelta = new Vector2(2, 2);

                // Find the uncaptured flag resource and display it
                var flag = flagObject.AddComponent<Image>();
                captureFlag = Resources.Load<Sprite>("UI_design/POI_capture_flags/POI_capture_flag_uncaptured");
                flag.sprite = captureFlag;
            }
        }

#if UNITY_EDITOR
        [DrawGizmo(GizmoType.Selected ^ GizmoType.NonSelected)]
        public void OnDrawGizmos()
        {
            if (Application.isPlaying && Object)
            {
                var centeredStyle = GUI.skin.GetStyle("Label");
                centeredStyle.alignment = TextAnchor.MiddleCenter;
                centeredStyle.normal.background = Texture2D.whiteTexture;
                centeredStyle.normal.textColor = Color.black;
                centeredStyle.fontStyle = FontStyle.Bold;

                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(Collider.bounds.center, Collider.bounds.size);
                Handles.Label(Collider.bounds.center, $@"{Object.Id}
Combat: {(Combat ? Combat.Object.Id : "None")}
Controller: {(ControlledBy ? ControlledBy.Username : "None")}
Stationed: {string.Join("\n    ", StationedHordes.Select(x => x.Object.Id))}
");
                HandleUtility.Repaint();
            }
        }
#endif

        public override void FixedUpdateNetwork()
        {
            // Respawn human patrol if POI has not been controlled for a while
            if (patrolController.HumanCount == 0 && StationedHordes.Count == 0 &&
                Runner.RemoteRenderTime - TimeWhenPoiAbandoned > 100f)
            {
                patrolController.UpdateHumanCountRpc(patrolController.startingHumanCount);
                ControlledBy = null;
            }
        }


        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RemoveControllerRpc()
        {
            s_RemoveController.Begin();
            ControlledBy = null;
            StationedHordes.Clear();
            s_RemoveController.End();
        }


        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void ChangeControllerRpc(Player player)
        {
            s_ChangeController.Begin();
            Debug.Log(
                $"Changing POI Controller from {(ControlledBy ? ControlledBy.Object.Id : "None")} to {player.Object.Id}");
            if (ControlledBy)
            {
                // Remove Cheese benefits from previous controller
                ControlledBy.DecrementCheeseIncrementRateRpc(_cheesePerTick);
                // Remove this POI from previous controller
                ControlledBy.RemoveControlledPoiRpc(this);
            }

            // Give control to new controller
            ControlledBy = player;

            player.AddControlledPoiRpc(this);
            // Add Cheese benefits to new controller
            Debug.Log($"Fixed cheese rate is {_cheesePerTick}");
            ControlledBy.IncrementCheeseIncrementRateRpc(_cheesePerTick);
            StationedHordes.Clear();
            if (player.IsLocal)
            {
                GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.POICaptured, 1);

                switch (_poiType)
                {
                    case POIType.City:
                        foreach (var horde in player.Hordes) horde.SetAliveRatsRpc((uint)((uint)horde.AliveRats * 1.1));
                        GameManager.Instance.UIManager.AddNotification("City captured. Population increased",
                            Color.black);
                        break;
                    case POIType.Lab:
                        foreach (var horde in player.Hordes)
                        {
                            horde.GetComponent<EvolutionManager>().AddPoints();
                            horde.AddSpeechBubbleRpc(EmoteType.Evolution);
                        }

                        GameManager.Instance.UIManager.AddNotification("Lab captured. Mutation points acquired.",
                            Color.black);
                        break;
                    case POIType.Farm:
                        player.AddCheeseRpc(100);
                        GameManager.Instance.UIManager.AddNotification("Farm captured. Food package acquired.",
                            Color.black);
                        break;
                }

                GameManager.Instance.PlaySfx(SoundEffectType.POICapture);
            }

            s_ChangeController.End();
        }

        private void UpdateFlag()
        {
            var flag = flagObject.GetComponent<Image>();

            if (ControlledBy && ControlledBy.IsLocal)
            {
                EmitCaptureEffect();
                captureFlag = Resources.Load<Sprite>("UI_design/POI_capture_flags/POI_capture_flag_owned");
                flag.sprite = captureFlag;
            }
            else if (ControlledBy)
            {
                captureFlag = Resources.Load<Sprite>("UI_design/POI_capture_flags/POI_capture_flag_enemy");
                flag.sprite = captureFlag;
            }
            else
            {
                captureFlag = Resources.Load<Sprite>("UI_design/POI_capture_flags/POI_capture_flag_uncaptured");
                flag.sprite = captureFlag;
            }
        }

        public void EmitCaptureEffect()
        {
            Debug.Log("particle effect");
            if (captureEffect == null) Debug.LogError("captureEffect is not assigned!");
            foreach (var effect in captureEffect)
            {
                effect.Stop();
                effect.Play();
            }
        }

        private void StationHorde(HordeController horde)
        {
            Debug.Log($"Adding horde {horde.Object.Id} to myself (POI): {Object.Id}");
            StationedHordes.Add(horde);
        }

        /// <summary>
        ///     Called by horde which wants to stop being stationed at a specific POI.
        /// </summary>
        /// <param name="horde"></param>
        /// <param name="rpcInfo">Automatically populated by Photon Fusion with details of caller.</param>
        /// <exception cref="Exception"></exception>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void UnStationHordeRpc(HordeController horde, RpcInfo rpcInfo = default)
        {
            s_UnstationHorde.Begin();
            StationedHordes.Remove(horde);
            if (StationedHordes.Count == 0) TimeWhenPoiAbandoned = Runner.SimulationTime;
            s_UnstationHorde.End();
        }

        public override void Spawned()
        {
            Collider = GetComponentInChildren<Collider2D>();
            _cheesePerTick = 0.3f;
            UpdateFlag();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AttackRpc(HordeController horde)
        {
            s_Attack.Begin();
            if (ControlledBy == horde.player)
            {
                StationHorde(horde);
                s_Attack.End();
                return;
            }

            // No need to start combat, just hand over control
            if (StationedHordes.Count == 0)
            {
                ChangeControllerRpc(horde.player);
                StationHorde(horde);
                s_Attack.End();
                return;
            }

            Debug.Log("POI being attacked");
            if (!Combat)
            {
                Debug.Log("Changing POI Combat Controller");
                Combat = Runner.Spawn(GameManager.Instance.CombatControllerPrefab).GetComponent<CombatController>();
                Combat!.SetFightingOver(this);
                foreach (var defender in StationedHordes) Combat.AddHordeRpc(defender);
            }

            Combat.AddHordeRpc(horde);
            s_Attack.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventCombatOverRpc()
        {
            s_EventCombatOver.Begin();
            Debug.Log("POI Combat over");
            Combat = null;
            s_EventCombatOver.End();
        }
    }
}