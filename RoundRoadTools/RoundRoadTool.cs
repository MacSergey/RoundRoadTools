﻿using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using Mod.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace Mod
{
    public class RoundRoadTools : ToolBase
    {
        #region STATIC

        public static int U => 8;
        public static float P => 3.625f;

        private static Color32 StartColor => new Color32(0, 200, 81, 0);
        private static Color32 StartHoverColor => new Color32(0, 126, 51, 0);
        private static Color32 StartPropColor => new Color32(195, 230, 203, 0);
        private static Color32 EndColor => new Color32(255, 68, 68, 0);
        private static Color32 EndHoverColor => new Color32(204, 0, 0, 0);
        private static Color32 EndPropColor => new Color32(245, 198, 203, 0);
        private static Color32 HoverColor => new Color32(51, 181, 229, 0);
        private static Color32 ShiftColor => new Color32(255, 136, 0, 0);

        public static int DefaultRadius => 8;
        public static int DefaultSegmentLenght => 5;
        public static int MinSegmentLenght => 2;
        public static bool DefaultSandGlass => false;
        public static bool DefaultShowShift => false;
        public static int DefaultShift => 0;
        public static int DefaultLines => 1;


        #endregion

        #region PRIVATE

        #region MANAGERS
        private NetManager NetManager => Singleton<NetManager>.instance;
        private RenderManager RenderManager => Singleton<RenderManager>.instance;
        private SimulationManager SimulationManager => Singleton<SimulationManager>.instance;
        private TerrainManager TerrainManager => Singleton<TerrainManager>.instance;
        private ToolManager ToolManager => Singleton<ToolManager>.instance;
        #endregion
        private RoundRoadCalculation Calculation { get; } = new RoundRoadCalculation();

        private bool Calced { get; set; }
        private string CalcedError { get; set; }

        private ushort HoverNodeId { get; set; } = 0;
        private ushort HoverSegmentId { get; set; } = 0;

        private Param<ushort> StartNodeId { get; } = new Param<ushort>(0, ushort.MinValue, ushort.MaxValue);
        private Param<ushort> EndNodeId { get; } = new Param<ushort>(0, ushort.MinValue, ushort.MaxValue);
        private Param<ushort> StartSegmentId { get; } = new Param<ushort>(0, ushort.MinValue, ushort.MaxValue);
        private Param<ushort> EndSegmentId { get; } = new Param<ushort>(0, ushort.MinValue, ushort.MaxValue);

        private bool ShowShift { get; set; } = DefaultShowShift;
        private ParallelMode Parallel { get; set; } = ParallelMode.None;


        private bool StartNodeSelected => StartNodeId != 0;
        private bool EndNodeSelected => EndNodeId != 0;
        private bool StartSegmentSelected => StartSegmentId != 0;
        private bool EndSegmentSelected => EndSegmentId != 0;

        private SelectedStatus Status => (StartNodeSelected && StartSegmentSelected ? SelectedStatus.Start : SelectedStatus.None) | (EndNodeSelected && EndSegmentSelected ? SelectedStatus.End : SelectedStatus.None);
        private bool CanBuild => Status == SelectedStatus.All;
        private bool LetBuild { get; set; } = false;

        private List<DirectionPointExtended> CalculatedPoints { get; set; }
        #endregion

        #region PUBLIC

        public NetInfo NetInfo { get; set; }
        public new bool enabled
        {
            get => base.enabled;
            set
            {
                if (!enabled && value)
                    Init();

                base.enabled = value;
            }
        }

        #endregion

        public RoundRoadTools()
        {
            StartNodeId.OnChange += OnParamChange;
            EndNodeId.OnChange += OnParamChange;
            StartSegmentId.OnChange += OnParamChange;
            EndSegmentId.OnChange += OnParamChange;
            Calculation.Radius.OnChange += OnParamChange;
            Calculation.SegmentLenght.OnChange += OnParamChange;
            Calculation.MustSandGlass.OnChange += OnParamChange;
            Calculation.StartShift.OnChange += OnParamChange;
            Calculation.EndShift.OnChange += OnParamChange;
            Calculation.LineCount.OnChange += OnParamChange;
            Calculation.ShiftBegin.OnChange += OnParamChange;
            Calculation.LineCount.OnChange += OnParamChange;
        }


        #region INIT

        protected override void Awake()
        {
            Debug.Log($"{nameof(RoundRoadTools)}.{nameof(Awake)}");
            base.Awake();
            Init();
        }
        public void Init()
        {
            Debug.Log($"{nameof(RoundRoadTools)}.{nameof(Init)}");

            StartNodeId.Value = 0;
            EndNodeId.Value = 0;
            StartSegmentId.Value = 0;
            EndSegmentId.Value = 0;
            Calculation.Radius.Value = DefaultRadius;
            Calculation.SegmentLenght.Value = DefaultSegmentLenght;
            Calculation.MustSandGlass.Value = DefaultSandGlass;
            Calculation.LineCount.Value = DefaultLines;
            Calculation.StartShift.Value = DefaultShift;
            Calculation.EndShift.Value = DefaultShift;
        }

        #endregion

        #region UPDATE

        protected override void OnToolUpdate()
        {
            Debug.Log($"{nameof(RoundRoadTools)}.{nameof(OnToolUpdate)}");
            base.OnToolUpdate();

            GetHovered();
            Debug.Log($"{nameof(HoverNodeId)} = {HoverNodeId}");
            Debug.Log($"{nameof(HoverSegmentId)} = {HoverSegmentId}");

            InputCheck();
            Debug.Log($"{nameof(StartNodeId)} = {StartNodeId}");
            Debug.Log($"{nameof(EndNodeId)} = {EndNodeId}");

            Calculate();

            Build();

            Info();
        }
        private void GetHovered()
        {
            if (!UIView.IsInsideUI() && Cursor.visible)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastInput input = new RaycastInput(ray, Camera.main.farClipPlane)
                {
                    m_ignoreTerrain = true,
                    m_ignoreNodeFlags = NetNode.Flags.None,
                    m_ignoreSegmentFlags = NetSegment.Flags.None
                };
                input.m_netService.m_itemLayers = (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
                input.m_netService.m_service = ItemClass.Service.Road;

                if (RayCast(input, out RaycastOutput output))
                {
                    HoverSegmentId = output.m_netSegment;
                    var segment = GetSegment(output.m_netSegment);
                    var startNode = GetNode(segment.m_startNode);
                    var endNode = GetNode(segment.m_endNode);
                    HoverNodeId = Vector3.Distance(output.m_hitPos, startNode.m_position) < Vector3.Distance(output.m_hitPos, endNode.m_position) ? segment.m_startNode : segment.m_endNode;

                    return;
                }
            }
            HoverSegmentId = 0;
            HoverNodeId = 0;
        }
        private void InputCheck()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                enabled = false;
                ToolsModifierControl.SetTool<DefaultTool>();
            }

            if (Input.GetMouseButtonUp(0))
                OnMouseButton0Up();

            if (Input.GetMouseButtonUp(1))
                OnMouseButton1Up();
        }

        private void OnMouseButton0Up()
        {
            switch (Status)
            {
                case SelectedStatus.None:
                    StartNodeId.Value = HoverNodeId;
                    StartSegmentId.Value = HoverSegmentId;
                    break;
                case SelectedStatus.Start:
                    EndNodeId.Value = HoverNodeId;
                    EndSegmentId.Value = HoverSegmentId;
                    break;
            }
        }

        private void OnMouseButton1Up()
        {
            switch (Status)
            {
                case SelectedStatus.Start:
                    StartNodeId.Value = 0;
                    StartSegmentId.Value = 0;
                    break;
                case SelectedStatus.All:
                    EndNodeId.Value = 0;
                    EndSegmentId.Value = 0;
                    break;
            }
        }
        private void Info()
        {
            switch (Status)
            {
                case SelectedStatus.None:
                    ShowToolInfo(Localize.SelectStartInfo);
                    break;
                case SelectedStatus.Start:
                    ShowToolInfo(Localize.SelectEndInfo);
                    break;
                case SelectedStatus.All:
                    var lines = new List<string>();

                    if (!string.IsNullOrEmpty(CalcedError))
                        lines.Add(CalcedError);
                    lines.Add($"{Localize.PreviewMode}: {(ShowShift ? "со смещением" : "без смещения")}");
                    lines.Add($"{Localize.Radius}: {Calculation.RadiusProcessed / U:F2}{nameof(U)}");
                    lines.Add($"{Localize.Segment}: {Calculation.SegmentLenghtProcessed / U:F2}{nameof(U)}");
                    lines.Add($"{Localize.LineCount}: {Calculation.LineCount}");
                    lines.Add($"{Localize.Shifts}: {string.Join("-", Calculation.Shifts.Select(i => i.Name).ToArray())}");

                    ShowToolInfo(string.Join("\n", lines.ToArray()));
                    break;
            }
        }
        #endregion

        #region OVERLAY

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            Debug.Log($"{nameof(RoundRoadTools)}.{nameof(RenderOverlay)}");
            if (!enabled)
                return;

            RenderNodesOverlay(cameraInfo);
            RenderSegmentsOverlay(cameraInfo);
            RenderRoadOverlay(cameraInfo);
        }

        private void RenderNodesOverlay(RenderManager.CameraInfo cameraInfo)
        {
            if (StartNodeSelected)
                RenderNodeOverlay(StartNodeId, StartColor, cameraInfo);
            if (EndNodeSelected)
                RenderNodeOverlay(EndNodeId, EndColor, cameraInfo);
            if (HoverNodeId != 0 && Status != SelectedStatus.All && HoverNodeId != StartNodeId && HoverNodeId != EndNodeId)
                RenderNodeOverlay(HoverNodeId, Status == SelectedStatus.Start ? EndHoverColor : StartHoverColor, cameraInfo);
        }
        private void RenderSegmentsOverlay(RenderManager.CameraInfo cameraInfo)
        {
            if (StartSegmentSelected)
                RenderSegmentOverlay(StartSegmentId, StartColor, cameraInfo);
            if (EndSegmentSelected)
                RenderSegmentOverlay(EndSegmentId, EndColor, cameraInfo);
            if (HoverSegmentId != 0 && Status != SelectedStatus.All && HoverSegmentId != StartSegmentId && HoverSegmentId != EndSegmentId)
                RenderSegmentOverlay(HoverSegmentId, Status == SelectedStatus.Start ? EndHoverColor : StartHoverColor, cameraInfo);
        }
        private void RenderNodeOverlay(ushort nodeId, Color32 color, RenderManager.CameraInfo cameraInfo)
        {
            if (nodeId == 0)
                return;

            var node = GetNode(nodeId);

            color.a = GetAlpha(node.Info);

            RenderManager.OverlayEffect.DrawCircle(cameraInfo, color, node.m_position, Mathf.Max(6f, node.Info.m_halfWidth * 2f), -1f, 1280f, false, true);
        }
        private void RenderSegmentOverlay(ushort segmentId, Color32 color, RenderManager.CameraInfo cameraInfo)
        {
            if (segmentId == 0)
                return;

            var segment = GetSegment(segmentId);



            Bezier3 bezier;
            bezier.a = GetNodePosition(segment.m_startNode);
            bezier.d = GetNodePosition(segment.m_endNode);
            NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d, segment.m_endDirection, IsMiddle(segment.m_startNode), IsMiddle(segment.m_endNode), out bezier.b, out bezier.c);

            color.a = GetAlpha(segment.Info);

            RenderManager.OverlayEffect.DrawBezier(cameraInfo, color, bezier, Mathf.Max(6f, segment.Info.m_halfWidth * 2f), 0, 0, -1f, 1280f, false, true);

            bool IsMiddle(ushort nodeId) => GetNode(nodeId) is NetNode node && (node.m_flags & NetNode.Flags.Middle) != 0;
        }
        private byte GetAlpha(NetInfo info)
        {
            var alpha = 1f;
            NetTool.CheckOverlayAlpha(info, ref alpha);
            return (byte)(244 * alpha);
        }

        private void RenderRoadOverlay(RenderManager.CameraInfo cameraInfo)
        {
            if (!CanBuild || !Calced)
            {
                Debug.LogWarning($"Рендер дороги невозможен: {nameof(CanBuild)} = {CanBuild}; {nameof(Calced)} = {Calced}");
                return;
            }

            Render(CalculatedPoints);
            Render(GetParallelPoints(CalculatedPoints, Parallel));

            void Render(List<DirectionPointExtended> points)
            {
                var segments = Calculation.CalculateSegments(points);
                foreach (var segment in segments)
                    RenderRoadSegmentOverlay(segment, cameraInfo);
            }
        }

        private void RenderRoadSegmentOverlay(Segment segment, RenderManager.CameraInfo cameraInfo)
        {
            var bezier = new Bezier3
            {
                a = ShowShift ? segment.StartShiftPosition : segment.StartPosition,
                d = ShowShift ? segment.EndShiftPosition : segment.EndPosition
            };
            NetSegment.CalculateMiddlePoints(bezier.a, segment.StartDirection, bezier.d, segment.EndDirection, true, true, out bezier.b, out bezier.c);

            var color = segment.ShiftChange ? ShiftColor : HoverColor;
            color.a = GetAlpha(NetInfo);

            Debug.Log($"Рендер сегмента\n{segment}\nБезье: {bezier.a};{bezier.b};{bezier.c};{bezier.d}");
            var width = Calculation.LineCount * P;
            RenderManager.OverlayEffect.DrawBezier(cameraInfo, color, bezier, width, width / 2, width / 2, -1f, 1280f, false, true);
        }
        //private void RenderRoadSegment(NodePoint start, NodePoint end)
        //{
        //    Debug.Log($"Рендер сегмента\n{start}\n{end}");

        //    var manager = NetManager;
        //    var materialBlock = manager.m_materialBlock;
        //    materialBlock.Clear();

        //    var startPos = start.Position.ToVector3Terrain();
        //    var startDir = start.Direction.ToVector3(norm: true);

        //    var endPos = end.Position.ToVector3Terrain();
        //    var endDir = end.Direction.ToVector3(norm: true);

        //    var middlePos = (startPos + endPos) / 2;

        //    var startDirNormal = start.Direction.Turn90(false).ToVector3(norm: true);
        //    var endDirNormal = end.Direction.Turn90(false).ToVector3(norm: true);

        //    var startHalfWidth = startDirNormal * NetInfo.m_halfWidth;
        //    var endHalfWidth = endDirNormal * NetInfo.m_halfWidth;

        //    var startLeft = startPos - startHalfWidth;
        //    var startRight = startPos + startHalfWidth;

        //    var endLeft = endPos - endHalfWidth;
        //    var endRight = endPos + endHalfWidth;

        //    NetSegment.CalculateMiddlePoints(startLeft, startDir, endLeft, -endDir, true, true, out Vector3 startMiddleLeft, out Vector3 endMiddleLeft);
        //    NetSegment.CalculateMiddlePoints(startRight, startDir, endRight, -endDir, true, true, out Vector3 startMiddleRight, out Vector3 endMiddleRight);

        //    var vScale = NetInfo.m_netAI.GetVScale();

        //    var leftMatrix = NetSegment.CalculateControlMatrix(startLeft, startMiddleLeft, endMiddleLeft, endLeft, startRight, startMiddleRight, endMiddleRight, endRight, middlePos, vScale);
        //    var rightMatrix = NetSegment.CalculateControlMatrix(startRight, startMiddleRight, endMiddleRight, endRight, startLeft, startMiddleLeft, endMiddleLeft, endLeft, middlePos, vScale);

        //    var meshScale = new Vector4(0.5f / NetInfo.m_halfWidth, 1f / NetInfo.m_segmentLength, 1f, 1f);
        //    var turnMeshScale = new Vector4(-meshScale.x, -meshScale.y, meshScale.z, meshScale.w);

        //    materialBlock.SetMatrix(manager.ID_LeftMatrix, leftMatrix);
        //    materialBlock.SetMatrix(manager.ID_RightMatrices, rightMatrix);
        //    materialBlock.SetVector(manager.ID_ObjectIndex, RenderManager.DefaultColorLocation);
        //    materialBlock.SetColor(manager.ID_Color, NetInfo.m_color);

        //    if (NetInfo.m_requireSurfaceMaps)
        //    {
        //        TerrainManager.GetSurfaceMapping(middlePos, out Texture surfaceTexA, out Texture surfaceTexB, out Vector4 surfaceMapping);
        //        if (surfaceTexA != null)
        //        {
        //            materialBlock.SetTexture(manager.ID_SurfaceTexA, surfaceTexA);
        //            materialBlock.SetTexture(manager.ID_SurfaceTexB, surfaceTexB);
        //            materialBlock.SetVector(manager.ID_SurfaceMapping, surfaceMapping);
        //        }
        //    }

        //    //Debug.Log($"{nameof(startLeft)} = {startLeft}\n{nameof(startMiddleLeft)} = {startMiddleLeft}\n{nameof(endMiddleLeft)} = {endMiddleLeft}\n{nameof(endLeft)} = {endLeft}\n{nameof(startRight)} = {startRight}\n{nameof(startMiddleRight)} = {startMiddleRight}\n{nameof(endMiddleRight)} = {endMiddleRight}\n{nameof(endRight)} = {endRight}\n{nameof(middlePos)} = {middlePos}\n{nameof(vScale)} = {vScale}\n{nameof(meshScale)} = {meshScale}");

        //    foreach (var segment in NetInfo.m_segments)
        //    {
        //        if (!segment.CheckFlags(NetSegment.Flags.None, out bool turnAround))
        //        {
        //            Debug.Log("continue");
        //            continue;
        //        }

        //        materialBlock.SetVector(manager.ID_MeshScale, turnAround ? turnMeshScale : meshScale);
        //        ToolManager.m_drawCallData.m_defaultCalls += 1;
        //        Graphics.DrawMesh(segment.m_segmentMesh, middlePos, Quaternion.identity, segment.m_segmentMaterial, segment.m_layer, null, 0, materialBlock);
        //        Debug.Log("DrawMesh");
        //    }
        //}

        #endregion

        #region GEOMETRY

        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo)
        {
            //Debug.Log($"{nameof(RoundRoadTools)}.{nameof(RenderGeometry)}");
            if (!enabled)
                return;
        }

        #endregion


        protected override void OnToolGUI(Event e)
        {
            Debug.Log($"{nameof(RoundRoadTools)}.{nameof(OnToolGUI)}");
            if (!enabled)
                return;

            if (KeyMapping.RadiusPlus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.RadiusPlus)}");
                Calculation.Radius.Value += 0.25f;
            }
            if (KeyMapping.RadiusMinus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.RadiusMinus)}");
                Calculation.Radius.Value -= 0.25f;
            }

            if (KeyMapping.SegmentPlus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.SegmentPlus)}");
                Calculation.SegmentLenght.Value += 0.25f;
            }
            if (KeyMapping.SegmentMinus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.SegmentMinus)}");
                Calculation.SegmentLenght.Value -= 0.25f;
            }

            if (KeyMapping.StartShiftPlus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.StartShiftPlus)}");
                Calculation.StartShift.Value += 1;
            }
            if (KeyMapping.StartShiftMinus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.StartShiftMinus)}");
                Calculation.StartShift.Value -= 1;
            }

            if (KeyMapping.EndShiftPlus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.EndShiftPlus)}");
                Calculation.EndShift.Value += 1;
            }
            if (KeyMapping.EndShiftMinus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.EndShiftMinus)}");
                Calculation.EndShift.Value -= 1;
            }

            if (KeyMapping.ShiftBeginPlus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.ShiftBeginPlus)}");
                Calculation.ShiftBegin.Value += 1;
            }
            if (KeyMapping.ShiftBeginMinus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.ShiftBeginMinus)}");
                Calculation.ShiftBegin.Value -= 1;
            }

            if (KeyMapping.LinesPlus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.LinesPlus)}");
                Calculation.LineCount.Value += 1;
            }
            if (KeyMapping.LinesMinus.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.LinesMinus)}");
                Calculation.LineCount.Value -= 1;
            }

            if (KeyMapping.SandGlass.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.SandGlass)}");
                Calculation.MustSandGlass.Value = !Calculation.MustSandGlass;
            }
            if (KeyMapping.ShowShift.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.ShowShift)}");
                ShowShift = !ShowShift;
            }
            if (KeyMapping.Parallel.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.Parallel)}");
                switch (Parallel)
                {
                    case ParallelMode.None:
                        Parallel = ParallelMode.Start;
                        break;
                    case ParallelMode.Start:
                        Parallel = ParallelMode.End;
                        break;
                    case ParallelMode.End:
                        Parallel = ParallelMode.None;
                        break;
                }
            }
            if (KeyMapping.Build.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.Build)}");
                LetBuild = true;
            }
            if (KeyMapping.Copy.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.Copy)}");
                Copy();
            }
            if (KeyMapping.Paste.IsPressed(e))
            {
                Debug.Log($"нажата {nameof(KeyMapping.Paste)}");
                Copy();
            }
        }

        #region INFO

        private void ShowInfo(string text, Vector3? position = null)
        {
            if (position == null)
                position = GetInfoPosition();

            UIView uIView = extraInfoLabel.GetUIView();
            var a = Camera.main.WorldToScreenPoint(position.Value) / uIView.inputScale;
            extraInfoLabel.relativePosition = uIView.ScreenPointToGUI(a) - extraInfoLabel.size * 0.5f;

            extraInfoLabel.isVisible = true;
            extraInfoLabel.textColor = GetToolColor(false, false);
            extraInfoLabel.text = text ?? string.Empty;
        }

        private void ShowToolInfo(string text, Vector3? position = null)
        {
            if (cursorInfoLabel == null)
                return;

            if (position == null)
                position = GetInfoPosition();

            cursorInfoLabel.isVisible = true;
            cursorInfoLabel.text = text ?? string.Empty;

            UIView uIView = cursorInfoLabel.GetUIView();

            var screenSize = fullscreenContainer?.size ?? uIView.GetScreenResolution();
            var v = Camera.main.WorldToScreenPoint(position.Value) / uIView.inputScale;
            var vector2 = cursorInfoLabel.pivot.UpperLeftToTransform(cursorInfoLabel.size, cursorInfoLabel.arbitraryPivotOffset);
            var relativePosition = uIView.ScreenPointToGUI(v) + new Vector2(vector2.x, vector2.y);
            relativePosition.x = MathPos(relativePosition.x, cursorInfoLabel.width, screenSize.x);
            relativePosition.y = MathPos(relativePosition.y, cursorInfoLabel.height, screenSize.y);

            cursorInfoLabel.relativePosition = relativePosition;

            float MathPos(float pos, float size, float screen) => pos + size > screen ? (screen - size < 0 ? 0 : screen - size) : pos;
        }
        private Vector3 GetInfoPosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastInput input = new RaycastInput(ray, Camera.main.farClipPlane)
            {
                m_ignoreTerrain = false,
                m_ignoreNodeFlags = NetNode.Flags.None
            };
            RayCast(input, out RaycastOutput output);

            return output.m_hitPos;
        }
        private void HideInfo() => extraInfoLabel.isVisible = false;
        private void HideToolInfo() => cursorInfoLabel.isVisible = false;

        #endregion

        #region UTILITIES

        private NetNode GetNode(ushort nodeId) => NetManager.m_nodes.m_buffer[nodeId];
        private NetSegment GetSegment(ushort segmentId) => NetManager.m_segments.m_buffer[segmentId];
        private Vector3 GetNodePosition(ushort nodeId) => GetNode(nodeId).m_position;
        private Vector3 GetNodeDirection(ushort nodeId, ushort segmentId)
        {
            var segment = GetSegment(segmentId);
            if (segment.m_startNode == nodeId)
                return -segment.m_startDirection;
            else if (segment.m_endNode == nodeId)
                return -segment.m_endDirection;
            else
                return Vector3.zero;
        }
        private void OnParamChange() => Calced = false;
        private uint GetCurrentBuildIndex(bool inc = true)
        {
            var index = SimulationManager.m_currentBuildIndex;

            if (inc)
                SimulationManager.m_currentBuildIndex += 1;

            return index;
        }
        private List<DirectionPointExtended> GetParallelPoints(List<DirectionPointExtended> points, ParallelMode parallel)
        {
            var parallelPoints = new List<DirectionPointExtended>();

            var delta = Vector2.zero;
            switch (parallel)
            {
                case ParallelMode.None:
                    return parallelPoints;
                case ParallelMode.Start:
                    delta = points.First().Shift;
                    break;
                case ParallelMode.End:
                    delta = points.Last().Shift;
                    break;
            }

            foreach (var point in (points as IEnumerable<DirectionPointExtended>).Reverse())
            {
                var thisDelta = point.Shift.magnitude - delta.magnitude;
                if (Math.Abs(thisDelta) < 0.1)
                    parallelPoints.Add(new DirectionPointExtended(point.Point, -point.Direction, -point.Shift));
                else
                    parallelPoints.Add(new DirectionPointExtended(point.Position + point.Shift.normalized * 2 * thisDelta, -point.Direction, -point.Shift, point.Height));
            }
            return parallelPoints;
        }

        #endregion

        #region BUILD

        public bool Build()
        {
            try
            {
                if (!CanBuild || !LetBuild)
                    return false;

                Debug.LogWarning($"{nameof(RoundRoadTools)}.{nameof(Build)}");

                var segments = Calculation.CalculateSegments(CalculatedPoints);
                foreach (var segment in segments)
                    BuildSegment(segment);

                var parallelSegments = Calculation.CalculateSegments(GetParallelPoints(CalculatedPoints, Parallel));
                foreach (var parallelSegment in parallelSegments)
                    BuildSegment(parallelSegment);


                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"Не удалось построить: {error.Message}");
                return false;
            }
            finally
            {
                LetBuild = false;
            }
        }

        public ushort BuildSegment(Segment segment)
        {
            BuildNode(segment.Start, false);
            BuildNode(segment.End, false);

            Debug.LogWarning($"Создание сегмента\n{segment}");
            var random = new Randomizer();
            NetManager.CreateSegment(out ushort segmentId, ref random, NetInfo, segment.Start.NodeId, segment.End.NodeId, segment.StartDirection, segment.EndDirection, GetCurrentBuildIndex(), GetCurrentBuildIndex(), false);

            return segmentId;
        }
        public ushort BuildNode(DirectionPointExtended point, bool terrain = true)
        {
            if (point.NodeCreated)
                return point.NodeId;

            var position = point.Position.ToVector3(point.Height);
            if (terrain)
                position.y = TerrainManager.SampleRawHeightSmoothWithWater(position, false, 0);

            Debug.LogError($"Создание узла: {position}");
            var random = new Randomizer();
            NetManager.CreateNode(out ushort newNodeId, ref random, NetInfo, position, GetCurrentBuildIndex());
            point.NodeId = newNodeId;

            return newNodeId;
        }

        #endregion

        #region CALCULATION
        private void Calculate()
        {
            if (!CanBuild || Calced)
                return;

            var startPoint = new DirectionPoint(GetNodePosition(StartNodeId), GetNodeDirection(StartNodeId, StartSegmentId), nodeId: StartNodeId);
            var endPoint = new DirectionPoint(GetNodePosition(EndNodeId), GetNodeDirection(EndNodeId, EndSegmentId), nodeId: EndNodeId);

            try
            {
                CalculatedPoints = Calculation.CalculateRoad(startPoint, endPoint, (s) => Debug.Log(s));

                Calced = true;
                CalcedError = null;
            }
            catch (RoadException error)
            {
                Calced = false;
                CalcedError = error.Message;
            }
            catch (Exception error)
            {
                Calced = false;
                CalcedError = "Ошибка";
                Debug.LogError(error);
            }
        }



        #endregion

        public XmlDocument XmlDocument { get; set; }
        public void Copy()
        {
            Debug.Log("START RECOVERY");

            var xdoc = new XmlDocument();

            var xmlDeclaration = xdoc.CreateXmlDeclaration("1.0", "UTF-8", null);
            xdoc.AppendChild(xmlDeclaration);

            var xroot = xdoc.CreateElement("catalog");

            for (ushort i = 0; i < ushort.MaxValue; i += 1)
            {
                try
                {
                    var node = NetManager.m_nodes.m_buffer[i];

                    if (node.m_position == Vector3.zero)
                        continue;

                    if (node.m_position.x > 4800 || node.m_position.x < -4800 || node.m_position.z > 4800 || node.m_position.z < -4800)
                        continue;

                    Debug.Log($"node{i}: {node.m_position}");
                    var xnode = xdoc.CreateElement(nameof(node));

                    AddAttr(xnode, "i", i.ToString());
                    AddAttr(xnode, "info", node.Info.m_netAI.name);

                    AddAttr(xnode, nameof(node.m_position.x), node.m_position.x.ToString());
                    AddAttr(xnode, nameof(node.m_position.y), node.m_position.y.ToString());
                    AddAttr(xnode, nameof(node.m_position.z), node.m_position.z.ToString());

                    xroot.AppendChild(xnode);
                }
                catch { }
            }

            for (ushort i = 0; i < ushort.MaxValue; i += 1)
            {
                try
                {
                    var segment = NetManager.m_segments.m_buffer[i];

                    if (segment.m_startNode == 0 && segment.m_endNode == 0)
                        continue;

                    Debug.Log($"segment{i}: start = {segment.m_startNode}; end = {segment.m_endNode}");

                    var xsegment = xdoc.CreateElement(nameof(segment));

                    AddAttr(xsegment, "i", i.ToString());
                    AddAttr(xsegment, "info", segment.Info.m_netAI.name);

                    AddAttr(xsegment, "startNode", segment.m_startNode.ToString());
                    AddAttr(xsegment, "startx", segment.m_startDirection.x.ToString());
                    AddAttr(xsegment, "starty", segment.m_startDirection.y.ToString());
                    AddAttr(xsegment, "startz", segment.m_startDirection.z.ToString());

                    AddAttr(xsegment, "endNode", segment.m_endNode.ToString());
                    AddAttr(xsegment, "endx", segment.m_endDirection.x.ToString());
                    AddAttr(xsegment, "endy", segment.m_endDirection.y.ToString());
                    AddAttr(xsegment, "endz", segment.m_endDirection.z.ToString());

                    AddAttr(xsegment, "invert", segment.m_flags.IsFlagSet(NetSegment.Flags.Invert).ToString());

                    xroot.AppendChild(xsegment);
                }
                catch { }
            }

            xdoc.AppendChild(xroot);

            using (FileStream stream = new FileStream("recovery.xml", FileMode.Create))
            {
                xdoc.Save(stream);
                Debug.Log("RECOVERY SAVE");
            }
            XmlDocument = xdoc;

            void AddAttr(XmlElement element, string name, string value)
            {
                var attr = xdoc.CreateAttribute(name);
                var attrValue = xdoc.CreateTextNode(value);
                attr.AppendChild(attrValue);
                element.Attributes.Append(attr);
            }
        }
        public void Paste()
        {
            Debug.Log("START RECOVERY");

            try
            {

                var xdoc = new XmlDocument();

                using (FileStream stream = new FileStream("recovery.xml", FileMode.Open))
                {
                    xdoc.Load(stream);
                }
                

                var random = new Randomizer();

                var bridge = new Dictionary<ushort, ushort>();
                foreach (var element in xdoc.DocumentElement.ChildNodes.OfType<XmlElement>().Where(i => i.Name == "node"))
                {
                    var i = ushort.Parse(element.GetAttribute("i"));
                    var info = element.GetAttribute("info");
                    var x = float.Parse(element.GetAttribute("x"));
                    var y = float.Parse(element.GetAttribute("y"));
                    var z = float.Parse(element.GetAttribute("z"));
                    var position = new Vector3(x, y, z);

                    if (!Regex.IsMatch(info, @".+\..+"))
                        continue;

                    var netInfo = PrefabCollection<NetInfo>.FindLoaded(info);
                    NetManager.CreateNode(out ushort newNodeId, ref random, netInfo, position, GetCurrentBuildIndex());

                    bridge[i] = newNodeId;

                    Debug.Log($"Node created: {i}-{newNodeId}; {position}; {info}");
                }

                foreach (var element in xdoc.DocumentElement.ChildNodes.OfType<XmlElement>().Where(i => i.Name == "segment"))
                {
                    var i = ushort.Parse(element.GetAttribute("i"));
                    var info = element.GetAttribute("info");

                    var startNode = ushort.Parse(element.GetAttribute("startNode"));
                    var endNode = ushort.Parse(element.GetAttribute("endNode"));

                    var startx = float.Parse(element.GetAttribute("startx"));
                    var starty = float.Parse(element.GetAttribute("starty"));
                    var startz = float.Parse(element.GetAttribute("startz"));
                    var startDir = new Vector3(startx, starty, startz);

                    var endx = float.Parse(element.GetAttribute("endx"));
                    var endy = float.Parse(element.GetAttribute("endy"));
                    var endz = float.Parse(element.GetAttribute("endz"));
                    var endDir = new Vector3(endx, endy, endz);

                    var invert = bool.Parse(element.GetAttribute("invert"));

                    if (!Regex.IsMatch(info, @".*\..*"))
                        continue;
                    if (!bridge.TryGetValue(startNode, out ushort startNodeId) || !bridge.TryGetValue(endNode, out ushort endNodeId))
                        continue;

                    var netInfo = PrefabCollection<NetInfo>.FindLoaded(info);
                    NetManager.CreateSegment(out ushort segmentId, ref random, netInfo, startNodeId, endNodeId, startDir, endDir, GetCurrentBuildIndex(), GetCurrentBuildIndex(), invert);

                    Debug.Log($"Segment created: {i}-{segmentId}; {startNodeId} - {startDir}; {endNodeId} - {endDir}");
                }

                Debug.Log("RECOVERY COMPLITE");
            }
            catch (Exception error)
            {
                Debug.Log($"RECOVERY FAILD: {error.Message} {error.StackTrace}");
            }
        }
    }

    [Flags]
    public enum SelectedStatus
    {
        None = 0,
        Start = 1,
        End = 2,
        All = Start | End,
    }
    public enum Direction
    {
        Forward = 0,
        Backward = 1
    }
    public enum ParallelMode
    {
        None,
        Start,
        End
    }
    public class Point
    {
        public Vector2 Position { get; }
        public float Height { get; set; }
        public ushort NodeId { get; set; } = 0;
        public bool NodeCreated => NodeId != 0;

        public Point(Vector2 position, float height = 0, ushort nodeId = 0)
        {
            Position = position;
            Height = height;
            NodeId = nodeId;
        }
        public Point(float positionX, float positionY, float height = 0, ushort nodeId = 0) : this(new Vector2(positionX, positionY), height, nodeId) { }

        public override string ToString() => $"{nameof(Position)}: {Position.Info()}, {nameof(Height)}: {Height}";
    }
    public class DirectionPoint
    {
        public Point Point { get; set; }
        public Vector2 Direction { get; }

        public Vector2 Position => Point.Position;
        public float Height
        {
            get => Point.Height;
            set => Point.Height = value;
        }
        public ushort NodeId
        {
            get => Point.NodeId;
            set => Point.NodeId = value;
        }
        public bool NodeCreated => Point.NodeCreated;

        public DirectionPoint(Point point, Vector2 direction)
        {
            Point = point;
            Direction = direction.normalized;
        }
        public DirectionPoint(Vector2 position, Vector2 direction, float height = 0, ushort nodeId = 0) : this(new Point(position, height, nodeId), direction) { }
        public DirectionPoint(Vector3 position, Vector3 direction, ushort nodeId = 0) : this(VectorUtils.XZ(position), VectorUtils.XZ(direction), position.y, nodeId) { }

        public override string ToString() => $"{Point}, {nameof(Direction)}: {Direction.Info()}";

        public static DirectionPoint operator -(DirectionPoint dirPoint) => new DirectionPoint(dirPoint.Point, -dirPoint.Direction);
    }
    public class DirectionPointExtended : DirectionPoint
    {
        public Vector2 Shift { get; set; }
        public Vector2 ShiftPosition => Point.Position + Shift;
        public bool HasShift => Shift != Vector2.zero;

        public DirectionPointExtended(Point point, Vector2 direction, Vector2? shift = null) : base(point, direction)
        {
            Shift = shift ?? Vector2.zero;
        }
        public DirectionPointExtended(Vector2 position, Vector2 direction, Vector2? shift = null, float height = 0, ushort nodeId = 0) : this(new Point(position, height, nodeId), direction, shift) { }

        public override string ToString() => $"{base.ToString()}, {nameof(Shift)}: {Shift.Info()}";
    }

    public class Segment
    {
        public DirectionPointExtended Start { get; }
        public DirectionPointExtended End { get; }

        public Vector3 StartDirection => Start.Direction.ToVector3(norm: true);
        public Vector3 EndDirection => -End.Direction.ToVector3(norm: true);

        public Vector3 StartPosition => Start.Position.ToVector3(Start.Height);
        public Vector3 EndPosition => End.Position.ToVector3(End.Height);

        public Vector3 StartShiftPosition => Start.ShiftPosition.ToVector3(Start.Height);
        public Vector3 EndShiftPosition => End.ShiftPosition.ToVector3(End.Height);

        public bool HasShift => Start.HasShift || End.HasShift;
        public bool ShiftChange => Mathf.Abs(Start.Shift.magnitude - End.Shift.magnitude) > 0.1;

        public Segment(DirectionPointExtended startPoint, DirectionPointExtended endPoint)
        {
            Start = startPoint;
            End = endPoint;
        }
        public override string ToString() => $"{nameof(Start)} - {Start}\n{nameof(End)} - {End}";
    }
    public class Param<T> where T : IComparable<T>
    {
        private T _value;
        private T _maxValue;
        private T _minValue;
        public T Value
        {
            get => _value;
            set
            {
                var newValue = Max(Min(value, MaxValue), MinValue);

                if (Equal(_value, newValue))
                    return;

                _value = newValue;
                OnChange?.Invoke();
            }
        }
        public T MaxValue
        {
            get => _maxValue;
            set
            {
                if (Equal(_maxValue, value))
                    return;

                _maxValue = value;
                Value = Value;
            }
        }
        public T MinValue
        {
            get => _minValue;
            set
            {
                if (Equal(_minValue, value))
                    return;

                _minValue = value;
                Value = Value;
            }
        }
        public event Action OnChange;
        public Param(T value, T minValue, T maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
            Value = value;
        }
        private T Max(T v1, T v2) => v1.CompareTo(v2) > 0 ? v1 : v2;
        private T Min(T v1, T v2) => v1.CompareTo(v2) < 0 ? v1 : v2;
        private bool Equal(T v1, T v2) => v1.CompareTo(v2) == 0;

        public static implicit operator T(Param<T> param) => param.Value;

        public override string ToString() => Value.ToString();
    }
    public struct Line
    {
        public float Shift;
        public string Name;
        public Line(float shift, string name)
        {
            Shift = shift;
            Name = name;
        }
    }
    public struct FoundRoundResult
    {
        public Vector2 RoundCenterPos;
        public Vector2 StartRoundPos;
        public Vector2 EndRoundPos;
        public float Angle;
        public bool IsSandGlass;
        public bool IsClockWise;
    }
    public class RoadException : Exception
    {
        public RoadException(string messsage) : base(messsage) { }
    }
    public class RoadSmallRadiusException : RoadException
    {
        public RoadSmallRadiusException() : base("Радиус слишком маленький") { }
    }
    public class RoadParallelLinesException : RoadException
    {
        public RoadParallelLinesException() : base("Линии параллельны") { }
    }
    public static class Utilities
    {
        public static Vector2 Turn(this Vector2 vector, float turnAngle, bool isClockWise)
        {
            turnAngle *= isClockWise ? -Mathf.Deg2Rad : Mathf.Deg2Rad;
            var newX = vector.x * Mathf.Cos(turnAngle) - vector.y * Mathf.Sin(turnAngle);
            var newY = vector.x * Mathf.Sin(turnAngle) + vector.y * Mathf.Cos(turnAngle);
            return new Vector2(newX, newY);
        }
        public static Vector2 Turn90(this Vector2 v, bool isClockWise) => isClockWise ? new Vector2(v.y, -v.x) : new Vector2(-v.y, v.x);
        public static string Info(this Vector2 vector) => $"{vector.x}; {vector.y}";

        public static Vector3 ToVector3(this Vector2 vector, float y = 0, bool norm = false)
        {
            var vector3 = new Vector3(vector.x, y, vector.y);
            return norm ? vector3.normalized : vector3;
        }
        public static Vector3 ToVector3Terrain(this Vector2 vector) => vector.ToVector3(280/*Singleton<TerrainManager>.instance.SampleRawHeight(vector.x, vector.y)*/);
    }

}
