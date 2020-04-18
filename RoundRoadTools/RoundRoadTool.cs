using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using Mod.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mod
{
    public class RoundRoadTools : ToolBase
    {
        #region STATIC

        public static int U => 8;
        public static float P => 3.5f;

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

        #endregion

        #region PRIVATE

        #region MANAGERS
        private NetManager NetManager => Singleton<NetManager>.instance;
        private RenderManager RenderManager => Singleton<RenderManager>.instance;
        private SimulationManager SimulationManager => Singleton<SimulationManager>.instance;
        private TerrainManager TerrainManager => Singleton<TerrainManager>.instance;
        private ToolManager ToolManager => Singleton<ToolManager>.instance;
        #endregion


        private bool Calced { get; set; }
        private string CalcedError { get; set; }

        private ushort HoverNodeId { get; set; } = 0;
        private ushort HoverSegmentId { get; set; } = 0;

        private Param<ushort> StartNodeId { get; } = new Param<ushort>(0, ushort.MinValue, ushort.MaxValue);
        private Param<ushort> EndNodeId { get; } = new Param<ushort>(0, ushort.MinValue, ushort.MaxValue);
        private Param<ushort> StartSegmentId { get; } = new Param<ushort>(0, ushort.MinValue, ushort.MaxValue);
        private Param<ushort> EndSegmentId { get; } = new Param<ushort>(0, ushort.MinValue, ushort.MaxValue);
        private Param<int> RawRadius { get; } = new Param<int>(0, 4, 200);
        private Param<int> SegmentLenghtRaw { get; } = new Param<int>(5, 2, 10);
        private Param<bool> MustSandGlass { get; } = new Param<bool>(false, false, true);
        private Param<int> StartShiftRaw { get; } = new Param<int>(0, -20, 20);
        private Param<int> EndShiftRaw { get; } = new Param<int>(0, -20, 20);

        private bool ShowShift { get; set; } = DefaultShowShift;
        private float Radius => ((float)RawRadius) / 2 * U;
        private float StartShift => ((float)StartShiftRaw) / 2 * P;
        private float EndShift => ((float)EndShiftRaw) / 2 * P;
        private float SegmentLenght => SegmentLenghtRaw * U;
        private float[] MiddleShift
        {
            get
            {
                var delta = EndShiftRaw - StartShiftRaw;
                var middleShifts = Enumerable.Range(1, Math.Max(0, Math.Abs(delta) - 1)).Select(i => ((float)(StartShiftRaw + Math.Sign(delta) * i)) / 2 * P).ToArray();
                return middleShifts;
            }
        }

        private bool StartNodeSelected => StartNodeId != 0;
        private bool EndNodeSelected => EndNodeId != 0;
        private bool StartSegmentSelected => StartSegmentId != 0;
        private bool EndSegmentSelected => EndSegmentId != 0;

        private SelectedStatus Status => (StartNodeSelected && StartSegmentSelected ? SelectedStatus.Start : SelectedStatus.None) | (EndNodeSelected && EndSegmentSelected ? SelectedStatus.End : SelectedStatus.None);
        private bool CanBuild => Status == SelectedStatus.All;
        private bool LetBuild { get; set; } = false;

        private List<Segment> CalcSegments { get; set; }
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
            RawRadius.OnChange += OnParamChange;
            SegmentLenghtRaw.OnChange += OnParamChange;
            MustSandGlass.OnChange += OnParamChange;
            StartShiftRaw.OnChange += OnParamChange;
            EndShiftRaw.OnChange += OnParamChange;
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
            RawRadius.Value = DefaultRadius;
            SegmentLenghtRaw.Value = DefaultSegmentLenght;
            MustSandGlass.Value = DefaultSandGlass;
            StartShiftRaw.Value = DefaultShift;
            EndShiftRaw.Value = DefaultShift;
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
        void OnMouseButton0Up()
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
        void OnMouseButton1Up()
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
                    lines.Add($"{Localize.Radius}: {Radius / U:F1}{nameof(U)}");
                    lines.Add($"{Localize.Segment}: {SegmentLenght / U:F0}{nameof(U)}");
                    lines.Add($"{Localize.StartShift}: {StartShift / P:F1}{nameof(P)}");
                    lines.Add($"{Localize.EndShift}: {EndShift / P:F1}{nameof(P)}");

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

            foreach (var segment in CalcSegments)
                RenderRoadSegmentOverlay(segment, cameraInfo);
        }

        private void RenderRoadSegmentOverlay(Segment segment, RenderManager.CameraInfo cameraInfo)
        {
            var bezier = new Bezier3
            {
                a = ShowShift ? segment.StartShiftPosition : segment.StartPosition,
                d = ShowShift ? segment.EndShiftPosition : segment.EndPosition
            };
            NetSegment.CalculateMiddlePoints(bezier.a, segment.StartDirection, bezier.d, -segment.EndDirection, true, true, out bezier.b, out bezier.c);

            var color = segment.ShiftChange ? ShiftColor : HoverColor;
            color.a = GetAlpha(NetInfo);

            Debug.Log($"Рендер сегмента\n{segment}\nБезье: {bezier.a};{bezier.b};{bezier.c};{bezier.d}");
            RenderManager.OverlayEffect.DrawBezier(cameraInfo, color, bezier, NetInfo.m_halfWidth * 2f, NetInfo.m_halfWidth, NetInfo.m_halfWidth, -1f, 1280f, false, true);
        }
        private void RenderRoadSegment(NodePoint start, NodePoint end)
        {
            Debug.Log($"Рендер сегмента\n{start}\n{end}");

            var manager = NetManager;
            var materialBlock = manager.m_materialBlock;
            materialBlock.Clear();

            var startPos = start.Position.ToVector3Terrain();
            var startDir = start.Direction.ToVector3(norm: true);

            var endPos = end.Position.ToVector3Terrain();
            var endDir = end.Direction.ToVector3(norm: true);

            var middlePos = (startPos + endPos) / 2;

            var startDirNormal = start.Direction.Turn90(false).ToVector3(norm: true);
            var endDirNormal = end.Direction.Turn90(false).ToVector3(norm: true);

            var startHalfWidth = startDirNormal * NetInfo.m_halfWidth;
            var endHalfWidth = endDirNormal * NetInfo.m_halfWidth;

            var startLeft = startPos - startHalfWidth;
            var startRight = startPos + startHalfWidth;

            var endLeft = endPos - endHalfWidth;
            var endRight = endPos + endHalfWidth;

            NetSegment.CalculateMiddlePoints(startLeft, startDir, endLeft, -endDir, true, true, out Vector3 startMiddleLeft, out Vector3 endMiddleLeft);
            NetSegment.CalculateMiddlePoints(startRight, startDir, endRight, -endDir, true, true, out Vector3 startMiddleRight, out Vector3 endMiddleRight);

            var vScale = NetInfo.m_netAI.GetVScale();

            var leftMatrix = NetSegment.CalculateControlMatrix(startLeft, startMiddleLeft, endMiddleLeft, endLeft, startRight, startMiddleRight, endMiddleRight, endRight, middlePos, vScale);
            var rightMatrix = NetSegment.CalculateControlMatrix(startRight, startMiddleRight, endMiddleRight, endRight, startLeft, startMiddleLeft, endMiddleLeft, endLeft, middlePos, vScale);

            var meshScale = new Vector4(0.5f / NetInfo.m_halfWidth, 1f / NetInfo.m_segmentLength, 1f, 1f);
            var turnMeshScale = new Vector4(-meshScale.x, -meshScale.y, meshScale.z, meshScale.w);

            materialBlock.SetMatrix(manager.ID_LeftMatrix, leftMatrix);
            materialBlock.SetMatrix(manager.ID_RightMatrices, rightMatrix);
            materialBlock.SetVector(manager.ID_ObjectIndex, RenderManager.DefaultColorLocation);
            materialBlock.SetColor(manager.ID_Color, NetInfo.m_color);

            if (NetInfo.m_requireSurfaceMaps)
            {
                TerrainManager.GetSurfaceMapping(middlePos, out Texture surfaceTexA, out Texture surfaceTexB, out Vector4 surfaceMapping);
                if (surfaceTexA != null)
                {
                    materialBlock.SetTexture(manager.ID_SurfaceTexA, surfaceTexA);
                    materialBlock.SetTexture(manager.ID_SurfaceTexB, surfaceTexB);
                    materialBlock.SetVector(manager.ID_SurfaceMapping, surfaceMapping);
                }
            }

            //Debug.Log($"{nameof(startLeft)} = {startLeft}\n{nameof(startMiddleLeft)} = {startMiddleLeft}\n{nameof(endMiddleLeft)} = {endMiddleLeft}\n{nameof(endLeft)} = {endLeft}\n{nameof(startRight)} = {startRight}\n{nameof(startMiddleRight)} = {startMiddleRight}\n{nameof(endMiddleRight)} = {endMiddleRight}\n{nameof(endRight)} = {endRight}\n{nameof(middlePos)} = {middlePos}\n{nameof(vScale)} = {vScale}\n{nameof(meshScale)} = {meshScale}");

            foreach (var segment in NetInfo.m_segments)
            {
                if (!segment.CheckFlags(NetSegment.Flags.None, out bool turnAround))
                {
                    Debug.Log("continue");
                    continue;
                }

                materialBlock.SetVector(manager.ID_MeshScale, turnAround ? turnMeshScale : meshScale);
                ToolManager.m_drawCallData.m_defaultCalls += 1;
                Graphics.DrawMesh(segment.m_segmentMesh, middlePos, Quaternion.identity, segment.m_segmentMaterial, segment.m_layer, null, 0, materialBlock);
                Debug.Log("DrawMesh");
            }
        }

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
                RawRadius.Value += 1;

            if (KeyMapping.RadiusMinus.IsPressed(e))
                RawRadius.Value -= 1;

            if (KeyMapping.SegmentPlus.IsPressed(e))
                SegmentLenghtRaw.Value += 1;

            if (KeyMapping.SegmentMinus.IsPressed(e))
                SegmentLenghtRaw.Value -= 1;

            if (KeyMapping.StartShiftPlus.IsPressed(e))
                StartShiftRaw.Value += 1;

            if (KeyMapping.StartShiftMinus.IsPressed(e))
                StartShiftRaw.Value -= 1;

            if (KeyMapping.EndShiftPlus.IsPressed(e))
                EndShiftRaw.Value += 1;

            if (KeyMapping.EndShiftMinus.IsPressed(e))
                EndShiftRaw.Value -= 1;

            if (KeyMapping.SandGlass.IsPressed(e))
                MustSandGlass.Value = !MustSandGlass;

            if (KeyMapping.ShowShift.IsPressed(e))
                ShowShift = !ShowShift;

            if (KeyMapping.Build.IsPressed(e))
                LetBuild = true;
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

        #endregion

        #region BUILD

        public bool Build()
        {
            try
            {
                if (!CanBuild || !LetBuild)
                    return false;

                Debug.LogWarning($"{nameof(RoundRoadTools)}.{nameof(Build)}");

                foreach (var segment in CalcSegments)
                    CreateSegment(segment);

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

        public ushort CreateSegment(Segment segment)
        {
            CreateNode(segment.Start);
            CreateNode(segment.End);

            Debug.LogWarning($"Создание сегмента\n{segment}");
            var random = new Randomizer();
            NetManager.CreateSegment(out ushort segmentId, ref random, NetInfo, segment.Start.NodeId, segment.End.NodeId, segment.StartDirection, -segment.EndDirection, GetCurrentBuildIndex(), GetCurrentBuildIndex(), false);

            return segmentId;
        }
        public ushort CreateNode(NodePointExtended point, bool terrain = true)
        {
            if (point.NodeCreated)
                return point.NodeId;

            var position = point.Position.ToVector3();
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

            var startPoint = new NodePoint(GetNodePosition(StartNodeId), GetNodeDirection(StartNodeId, StartSegmentId), StartNodeId);
            var endPoint = new NodePoint(GetNodePosition(EndNodeId), GetNodeDirection(EndNodeId, EndSegmentId), EndNodeId);

            try
            {
                CalcSegments = CalculateRoad(startPoint, endPoint, Radius, SegmentLenght, MinSegmentLenght, MustSandGlass, StartShift, EndShift, MiddleShift, (s) => Debug.Log(s));
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

        public static List<Segment> CalculateRoad(NodePoint startRaw, NodePoint endRaw, float radius, float partLength, float minLength, bool mastSandGlass, float startShift, float endShift, float[] middleShifts, Action<string> log = null)
        {
            log?.Invoke("Рассчет дороги");

            var startShifted = CalculateShiftPoint(startRaw, startShift, NodeDir.Forward);
            var endShifted = CalculateShiftPoint(endRaw, endShift, NodeDir.Backward);
            var transitions = CalculateTransition(startShift, endShift, middleShifts);

            var foundRound = FoundRound(startShifted, endShifted, radius, mastSandGlass, log: log);

            var startPart = CalculateStraightPoints(startShifted.Position, foundRound.StartRoundPos, startShifted.Direction, partLength, minLength, false);
            var endPart = CalculateStraightPoints(endShifted.Position, foundRound.EndRoundPos, endShifted.Direction, partLength, minLength, true);
            var minRoundParts = Math.Max(0, transitions.Count - startPart.Count - endPart.Count);
            var roundPart = CalculateRoundPoints(radius, partLength, minLength, minRoundParts, foundRound);

            log?.Invoke($"Начальная часть - {startPart.Count}\n{string.Join("\n", startPart.Select(i => i.ToString()).ToArray())}");
            log?.Invoke($"Закругленная часть - {roundPart.Count}\n{string.Join("\n", roundPart.Select(i => i.ToString()).ToArray())}");
            log?.Invoke($"Конечная часть - {endPart.Count}\n{string.Join("\n", endPart.Select(i => i.ToString()).ToArray())}");

            var segments = CalculateSegments(startShifted, new NodePoint(endShifted.Position, -endShifted.Direction, endShifted.NodeId), startPart, endPart, roundPart, transitions);

            log?.Invoke($"Дорога расчитана: {segments.Count} сегментов\n{string.Join("\n", segments.Select((s, i) => $"[{i + 1}]\n{s}").ToArray())}");

            return segments;
        }
        public static NodePoint CalculateShiftPoint(NodePoint point, float shift, NodeDir nodeDir)
        {
            var shiftVector = point.Direction.Turn90(nodeDir == NodeDir.Forward).normalized * shift;
            var shiftPoint = new NodePoint(point.Position + shiftVector, point.Direction, point.NodeId);
            return shiftPoint;
        }
        public static FoundRoundResult FoundRound(NodePoint start, NodePoint end, float radius, bool mastSandGlass = false, Action<string> log = null)
        {
            log?.Invoke("Поиск круга");

            log?.Invoke($"{nameof(start)} = {start}");
            log?.Invoke($"{nameof(end)} = {end}");
            log?.Invoke($"{nameof(radius)} = {radius}");
            log?.Invoke($"{nameof(mastSandGlass)} = {mastSandGlass}");

            if (!Line2.Intersect(start.Position, start.Position - start.Direction, end.Position, end.Position - end.Direction, out float startPointXDist, out float endPointXDist) || (Mathf.Abs(startPointXDist) > 10000 && Mathf.Abs(endPointXDist) > 10000))
                throw new RoadParallelLinesException();

            log?.Invoke($"{nameof(startPointXDist)} = {startPointXDist}");
            log?.Invoke($"{nameof(endPointXDist)} = {endPointXDist}");

            var xPosS = start.Position - (start.Direction * startPointXDist);
            var xPosE = end.Position - (end.Direction * endPointXDist);
            var xPos = (xPosS + xPosE) / 2;
            log?.Invoke($"{nameof(xPosS)} = {xPosS.Info()}");
            log?.Invoke($"{nameof(xPosE)} = {xPosE.Info()}");
            log?.Invoke($"{nameof(xPos)} = {xPos.Info()}");

            var angle = Mathf.Abs(Vector2.Angle(start.Direction, end.Direction));
            log?.Invoke($"{nameof(angle)} = {angle}");

            var dist = radius / Mathf.Tan(angle / 2 * Mathf.Deg2Rad);
            log?.Invoke($"{nameof(dist)} = {dist}");

            var isSandGlass = true;
            if (startPointXDist < 0 && endPointXDist < 0)
            {
                if (dist < -startPointXDist && dist < -endPointXDist && !mastSandGlass)
                {
                    dist = -dist;
                    isSandGlass = false;
                }
            }
            else if (dist < startPointXDist || dist < endPointXDist)
                throw new RoadSmallRadiusException();

            var startRoundPos = xPos + start.Direction * dist;
            var endRoundPos = xPos + end.Direction * dist;
            log?.Invoke($"{nameof(startRoundPos)} = {startRoundPos.Info()}");
            log?.Invoke($"{nameof(endRoundPos)} = {endRoundPos.Info()}");

            var distXO = Mathf.Sqrt(Mathf.Pow(radius, 2) + Mathf.Pow(dist, 2));
            var dirXO = Mathf.Sign(dist) * (start.Direction + end.Direction).normalized;
            log?.Invoke($"{nameof(distXO)} = {distXO}");
            log?.Invoke($"{nameof(dirXO)} = {dirXO.Info()}");

            var roundCenterPos = xPos + (distXO * dirXO);
            log?.Invoke($"{nameof(roundCenterPos)} = {roundCenterPos} ({roundCenterPos.Info()})");

            var isClockWise = IsClockWise(start.Direction, startRoundPos, roundCenterPos);

            log?.Invoke($"{nameof(isSandGlass)} = {isSandGlass}");
            log?.Invoke($"{nameof(isClockWise)} = {isClockWise}");

            var result = new FoundRoundResult()
            {
                RoundCenterPos = roundCenterPos,
                StartRoundPos = startRoundPos,
                EndRoundPos = endRoundPos,
                Angle = 180 + (isSandGlass ? angle : -angle),
                IsSandGlass = isSandGlass,
                IsClockWise = isClockWise
            };
            return result;
        }
        public static List<Segment> CalculateSegments(NodePoint startPoint, NodePoint endPoint, List<NodePoint> startPart, List<NodePoint> endPart, List<NodePoint> roundPart, List<Transition> transitions)
        {
            var startTransitions = startPart.Count;
            var endTransitions = startTransitions + roundPart.Count;
            var transitionsCount = transitions.Count;

            if (startTransitions + transitionsCount < endTransitions)
                endTransitions = startTransitions + transitionsCount;
            else if (transitionsCount < endTransitions)
                startTransitions = endTransitions - transitionsCount;
            else
            {
                startTransitions = 0;
                endTransitions = transitionsCount;
            }

            var points = new List<NodePoint>();
            //points.Add(startPoint);
            points.AddRange(startPart);
            points.AddRange(roundPart);
            points.AddRange(endPart);
            //points.Add(endPoint);

            var startTransition = new Transition(transitions.First().StartShift, transitions.First().StartShift);
            transitions.InsertRange(0, Enumerable.Range(0, startTransitions).Select(i => startTransition));

            var endTransition = new Transition(transitions.Last().EndShift, transitions.Last().EndShift);
            transitions.AddRange(Enumerable.Range(0, points.Count + 1 - endTransitions).Select(i => endTransition));

            var extendetPoints = new List<NodePointExtended>();

            extendetPoints.Add(CreatePoint(startPoint, transitions.First().StartShift));
            extendetPoints.AddRange(points.Select((p, i) => CreatePoint(p, transitions[i].EndShift)));
            extendetPoints.Add(CreatePoint(endPoint, transitions.Last().EndShift));

            var segments = Enumerable.Range(0, extendetPoints.Count - 1).Select(i => new Segment(extendetPoints[i], extendetPoints[i + 1])).ToList();


            //foreach (var i in Enumerable.Range(0, transitions.Count))
            //{
            //    var startShift = points[i].Direction.Turn90(true) * transitions[i].StartShift;
            //    var endShift = points[i + 1].Direction.Turn90(true) * transitions[i].EndShift;
            //    var segmentStart = new NodePointExtended(points[i].Position - startShift, points[i].Direction, startShift, points[i].NodeId);
            //    var segmentEnd = new NodePointExtended(points[i + 1].Position - endShift, -points[i + 1].Direction, endShift, points[i].NodeId);

            //    segments.Add(new Segment(segmentStart, segmentEnd));
            //}

            return segments;

            NodePointExtended CreatePoint(NodePoint p, float shift)
            {
                var shiftVector = p.Direction.Turn90(true) * shift;
                return new NodePointExtended(p.Position - shiftVector, p.Direction, shiftVector, p.NodeId);
            }
        }
        public static List<Transition> CalculateTransition(float startShift, float endShift, float[] middleShifts)
        {
            var shifts = new List<float>();

            shifts.Add(startShift);
            shifts.AddRange(middleShifts ?? new float[0]);
            shifts.Add(endShift);

            var transitions = new List<Transition>();

            for (int i = 0; i < shifts.Count - 1; i += 1)
                transitions.Add(new Transition(shifts[i], shifts[i + 1]));

            return transitions;
        }
        public static List<NodePoint> CalculateStraightPoints(Vector2 startPos, Vector2 endPos, Vector2 direction, float partLength, float minLength, bool flip)
        {
            var distance = Vector2.Distance(startPos, endPos);
            var parts = GetPartCount(distance, partLength);
            if (distance / parts < minLength)
                parts -= 1;

            var points = new List<NodePoint>(parts);

            foreach (var i in Enumerable.Range(1, parts))
            {
                var position = startPos + direction * (distance / parts * i);
                points.Add(new NodePoint(position, flip ? -direction : direction));
            }
            if (flip)
                points.Reverse();

            return points;
        }
        public static List<NodePoint> CalculateRoundPoints(float radius, float partLength, float minLength, int minParts, FoundRoundResult foundRound)
        {
            var distance = GetRoundDistance(radius, foundRound.Angle);
            var parts = Math.Max(minParts, GetPartCount(distance, partLength));
            if (parts > Math.Max(1, minParts) && distance / parts < minLength)
                parts -= 1;

            var startVector = (foundRound.StartRoundPos - foundRound.RoundCenterPos).normalized;
            var points = new List<NodePoint>(parts);

            foreach (var i in Enumerable.Range(1, parts - 1))
            {
                var normal = startVector.Turn(foundRound.Angle / parts * i, foundRound.IsClockWise).normalized;
                var position = foundRound.RoundCenterPos + normal * radius;
                var direction = normal.Turn90(foundRound.IsClockWise);

                points.Add(new NodePoint(position, direction));
            }

            return points;
        }
        public static float GetRoundDistance(float radius, float angle) => radius * angle * Mathf.Deg2Rad;
        public static int GetPartCount(float distance, float length) => (int)Mathf.Ceil(distance / length);
        public static bool IsClockWise(Vector2 startDir, Vector2 startRoundPoint, Vector2 roundCenter)
        {
            var v1 = startDir.Turn90(true).normalized;
            var v2 = (startRoundPoint - roundCenter).normalized;
            return (v1 + v2).magnitude < 0.001f;
        }

        #endregion
    }

    [Flags]
    public enum SelectedStatus
    {
        None = 0,
        Start = 1,
        End = 2,
        All = Start | End,
    }
    public enum NodeDir
    {
        Forward = 0,
        Backward = 1
    }
    public class NodePoint
    {
        public Vector2 Position { get; }
        public Vector2 Direction { get; }
        public ushort NodeId { get; set; } = 0;
        public bool NodeCreated => NodeId != 0;

        public NodePoint(Vector2 position, Vector2 direction, ushort nodeId = 0, NodeDir mode = NodeDir.Forward)
        {
            Position = position;
            Direction = (mode == NodeDir.Forward ? direction : -direction).normalized;
            NodeId = nodeId;
        }
        public NodePoint(Vector3 position, Vector3 direction, ushort nodeId = 0, NodeDir mode = NodeDir.Forward) : this(VectorUtils.XZ(position), VectorUtils.XZ(direction), nodeId, mode) { }
        public NodePoint(float positionX, float positionZ, float directionX, float directionZ, ushort nodeId = 0, NodeDir mode = NodeDir.Forward) : this(new Vector2(positionX, positionZ), new Vector2(directionX, directionZ), nodeId, mode) { }

        public override string ToString() => $"{nameof(Position)}: {Position.Info()}, {nameof(Direction)}: {Direction.Info()}";
    }
    public class NodePointExtended : NodePoint
    {
        public Vector2 Shift { get; }
        public Vector2 ShiftPosition => Position + Shift;
        public bool HasShift => Shift != Vector2.zero;
        public NodePointExtended(Vector2 position, Vector2 direction, Vector2 shift, ushort nodeId = 0, NodeDir nodeDir = NodeDir.Forward) : base(position, direction, nodeId, nodeDir)
        {
            Shift = shift;
        }
        public NodePointExtended(Vector2 position, Vector2 direction, ushort nodeId = 0, NodeDir nodeDir = NodeDir.Forward) : this(position, direction, Vector2.zero, nodeId, nodeDir) { }
        public NodePointExtended(NodePoint point, Vector2 shift, ushort nodeId = 0, NodeDir nodeDir = NodeDir.Forward) : this(point.Position, point.Direction, shift, nodeId, nodeDir) { }
        public NodePointExtended(NodePoint point, ushort nodeId = 0, NodeDir nodeDir = NodeDir.Forward) : this(point, Vector2.zero, nodeId, nodeDir) { }

        public NodePointExtended Around() => new NodePointExtended(Position, Direction, Shift, NodeId, NodeDir.Backward);

        public override string ToString() => $"{base.ToString()}, {nameof(Shift)}: {Shift.Info()}";
    }
    public class Segment
    {
        public NodePointExtended Start { get; }
        public NodePointExtended End { get; }

        public Vector3 StartDirection => Start.Direction.ToVector3(norm: true);
        public Vector3 EndDirection => End.Direction.ToVector3(norm: true);

        public Vector3 StartPosition => Start.Position.ToVector3();
        public Vector3 EndPosition => End.Position.ToVector3();

        public Vector3 StartShiftPosition => Start.ShiftPosition.ToVector3();
        public Vector3 EndShiftPosition => End.ShiftPosition.ToVector3();

        public bool HasShift => Start.HasShift || End.HasShift;
        public bool ShiftChange => Mathf.Abs(Start.Shift.magnitude - End.Shift.magnitude) > 0.1;

        public Segment(NodePointExtended startPoint, NodePointExtended endPoint)
        {
            Start = startPoint;
            End = endPoint;
        }
        public override string ToString() => $"{nameof(Start)} - {Start}\n{nameof(End)} - {End}";
    }
    public class Transition
    {
        public float StartShift { get; }
        public float EndShift { get; }
        public Transition(float startShift, float endShift)
        {
            StartShift = startShift;
            EndShift = endShift;
        }
        public override string ToString() => $"{nameof(StartShift)} = {StartShift}; {nameof(EndShift)} = {EndShift}";
    }
    public class Param<T> where T : IComparable<T>
    {
        private T _value;
        public T Value
        {
            get => _value;
            set
            {
                if (Equal(_value, value))
                    return;

                _value = Max(Min(value, MaxValue), MinValue);
                OnChange?.Invoke();
            }
        }
        public T MaxValue { get; }
        public T MinValue { get; }
        public event Action OnChange;
        public Param(T value, T minValue, T maxValue)
        {
            Value = value;
            MinValue = minValue;
            MaxValue = maxValue;
        }
        private T Max(T v1, T v2) => v1.CompareTo(v2) > 0 ? v1 : v2;
        private T Min(T v1, T v2) => v1.CompareTo(v2) < 0 ? v1 : v2;
        private bool Equal(T v1, T v2) => v1.CompareTo(v2) == 0;

        public static implicit operator T(Param<T> param) => param.Value;

        public override string ToString() => Value.ToString();
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
