using System;
using System.Linq;
using Combat;
using Fusion;
using Horde;
using Human;
using JetBrains.Annotations;
using Objectives;
using Players;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace POI
{
    public class PoiController : NetworkBehaviour
    {
        public ParticleSystem[] captureEffect;

        public PatrolController patrolController;

        public uint boidPoisIndex;
        private float _cheesePerTick;
        private Camera camera;

        private Sprite captureFlag;
        private GameObject flagObject;
        public float CheesePerSecond => _cheesePerTick / Runner.DeltaTime;

        public Collider2D Collider { get; private set; }

        [Networked]
        [OnChangedRender(nameof(UpdateFlag))]
        public Player ControlledBy { get; private set; }

        [Networked] [Capacity(4)] public NetworkLinkedList<HordeController> StationedHordes { get; } = default;

        [Networked] [CanBeNull] public CombatController Combat { get; private set; }

        public void Awake()
        {
            // Create a new GameObject for the icon
            if (!flagObject)
            {
                // create canvas to display icon
                var canvasGO = new GameObject("Canvas");
                canvasGO.transform.SetParent(transform);
                canvasGO.transform.localPosition = new Vector3(0, 2f, 0);

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

        public void ChangeController(Player player)
        {
            Debug.Log(
                $"Changing POI Controller from {(ControlledBy ? ControlledBy.Object.Id : "None")} to {player.Object.Id}");
            if (ControlledBy)
            {
                // Remove Cheese benefits from previous controller
                ControlledBy.DecrementCheeseIncrementRateRpc(_cheesePerTick);
                // Remove this POI from previous controller
                ControlledBy.ControlledPOIs.Remove(this);
            }

            // Give control to new controller
            ControlledBy = player;

            player.ControlledPOIs.Add(this);
            // Add Cheese benefits to new controller
            Debug.Log($"Fixed cheese rate is {_cheesePerTick}");
            ControlledBy.IncrementCheeseIncrementRateRpc(_cheesePerTick);
            StationedHordes.Clear();
            if (player.IsLocal) GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.POICaptured, 1);
        }

        private void UpdateFlag()
        {
            var flag = flagObject.GetComponent<Image>();

            if (ControlledBy.IsLocal)
            {
                EmitCaptureEffect();
                captureFlag = Resources.Load<Sprite>("UI_design/POI_capture_flags/POI_capture_flag_owned");
                flag.sprite = captureFlag;
            }
            else
            {
                captureFlag = Resources.Load<Sprite>("UI_design/POI_capture_flags/POI_capture_flag_enemy");
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
            StationedHordes.Remove(horde);
        }

        public override void Spawned()
        {
            Collider = GetComponentInChildren<Collider2D>();
            _cheesePerTick = 0.3f;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AttackRpc(HordeController horde)
        {
            if (ControlledBy == horde.player)
            {
                StationHorde(horde);
                return;
            }

            // No need to start combat, just hand over control
            if (StationedHordes.Count == 0)
            {
                ChangeController(horde.player);
                StationHorde(horde);
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
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventCombatOverRpc()
        {
            Debug.Log("POI Combat over");
            Combat = null;
        }
    }
}