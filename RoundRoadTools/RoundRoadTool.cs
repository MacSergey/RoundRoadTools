using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using Mod.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mod
{
    public class RoundRoadTools : ToolBase
    {
        #region STATIC

        public static int U => 8;

        private static Color32 StartColor => new Color32(0, 200, 81, 0);
        private static Color32 StartHoverColor => new Color32(0, 126, 51, 0);
        private static Color32 StartPropColor => new Color32(195, 230, 203, 0);
        private static Color32 EndColor => new Color32(255, 68, 68, 0);
        private static Color32 EndHoverColor => new Color32(204, 0, 0, 0);
        private static Color32 EndPropColor => new Color32(245, 198, 203, 0);
        private static Color32 HoverColor => new Color32(51, 181, 229, 0);
        private static Color32 ShiftColor => new Color32(255, 136, 0, 0);

        public static int MinRadius => 4;
        public static int DefaultRadius => 8;
        public static int MaxRadius => 200;
        public static int MinSegmentLenght => 2;
        public static int DefaultSegmentLenght => 5;
        public static int MaxSegmentLenght => 10;
        public static bool DefaultSandGlass => false;

        #endregion

        #region PRIVATE

        #region MANAGERS
        private NetManager NetManager => Singleton<NetManager>.instance;
        private RenderManager RenderManager => Singleton<RenderManager>.instance;
        private SimulationManager SimulationManager => Singleton<SimulationManager>.instance;
        private TerrainManager TerrainManager => Singleton<TerrainManager>.instance;
        private ToolManager ToolManager => Singleton<ToolManager>.instance;
        #endregion

        #region FIELDS
        private ushort _startNodeId = 0;
        private ushort _endNodeId = 0;
        private ushort _startSegmentId = 0;
        private ushort _endSegmentId = 0;
        private int _radius = DefaultRadius;
        private int _segmentLength = DefaultSegmentLenght;
        private bool _sandGlass = DefaultSandGlass;
        #endregion

        private bool Calced { get; set; }
        private string CalcedError { get; set; }

        private ushort HoverNodeId { get; set; } = 0;
        private ushort HoverSegmentId { get; set; } = 0;
        private ushort StartNodeId
        {
            get => _startNodeId;
            set => CheckChange(ref _startNodeId, value);
        }
        private ushort EndNodeId
        {
            get => _endNodeId;
            set => CheckChange(ref _endNodeId, value);
        }
        private ushort StartSegmentId
        {
            get => _startSegmentId;
            set => CheckChange(ref _startSegmentId, value);
        }
        private ushort EndSegmentId
        {
            get => _endSegmentId;
            set => CheckChange(ref _endSegmentId, value);
        }
        private int RawRadius
        {
            get => _radius;
            set => CheckChange(ref _radius, value);
        }
        private int SegmentLenght
        {
            get => _segmentLength;
            set => CheckChange(ref _segmentLength, value);
        }
        private float Radius => ((float)RawRadius) / 2;
        private bool MastSandGlass
        {
            get => _sandGlass;
            set => CheckChange(ref _sandGlass, value);
        }

        private bool StartNodeSelected => StartNodeId != 0;
        private bool EndNodeSelected => EndNodeId != 0;
        private bool StartSegmentSelected => StartSegmentId != 0;
        private bool EndSegmentSelected => EndSegmentId != 0;

        private SelectedStatus Status => (StartNodeSelected && StartSegmentSelected ? SelectedStatus.Start : SelectedStatus.None) | (EndNodeSelected && EndSegmentSelected ? SelectedStatus.End : SelectedStatus.None);
        private bool CanBuild => Status == SelectedStatus.All;

        private CalcResult CalcResult { get; set; }
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

            StartNodeId = 0;
            EndNodeId = 0;
            StartSegmentId = 0;
            EndSegmentId = 0;
            RawRadius = DefaultRadius;
            SegmentLenght = DefaultSegmentLenght;
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

            //var startPoint = new NodePoint(GetNodePosition(StartNodeId), GetNodeDirection(StartNodeId, StartSegmentId)) { NodeId = StartNodeId };
            //var endPoint = new NodePoint(GetNodePosition(EndNodeId), GetNodeDirection(EndNodeId, EndSegmentId)) { NodeId = EndNodeId };
            //ShowInfo($"{startPoint}\n{endPoint}");

            Calculate();

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
                    StartNodeId = HoverNodeId;
                    StartSegmentId = HoverSegmentId;
                    break;
                case SelectedStatus.Start:
                    EndNodeId = HoverNodeId;
                    EndSegmentId = HoverSegmentId;
                    break;
            }
        }
        void OnMouseButton1Up()
        {
            switch (Status)
            {
                case SelectedStatus.Start:
                    StartNodeId = 0;
                    StartSegmentId = 0;
                    break;
                case SelectedStatus.All:
                    EndNodeId = 0;
                    EndSegmentId = 0;
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
                    lines.Add($"{Localize.Radius}: {Radius:F1}U");
                    lines.Add($"{Localize.Segment}: {SegmentLenght}U");
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

            foreach (var segment in CalcResult.Segments)
                RenderRoadSegmentOverlay(segment, cameraInfo);
        }

        private void RenderRoadSegmentOverlay(Segment segment, RenderManager.CameraInfo cameraInfo)
        {
            var bezier = new Bezier3
            {
                a = segment.StartPosition,
                d = segment.EndPosition
            };
            NetSegment.CalculateMiddlePoints(bezier.a, segment.StartDirection, bezier.d, segment.EndDirection, true, true, out bezier.b, out bezier.c);

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

            var startDirNormal = start.Direction.Turn(90, false).ToVector3(norm: true);
            var endDirNormal = end.Direction.Turn(90, false).ToVector3(norm: true);

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
                RawRadius = Math.Min(MaxRadius, RawRadius + 1);

            if (KeyMapping.RadiusMinus.IsPressed(e))
                RawRadius = Math.Max(MinRadius, RawRadius - 1);

            if (KeyMapping.SegmentPlus.IsPressed(e))
                SegmentLenght = Math.Min(MaxSegmentLenght, SegmentLenght + 1);

            if (KeyMapping.SegmentMinus.IsPressed(e))
                SegmentLenght = Math.Max(MinSegmentLenght, SegmentLenght - 1);

            if (KeyMapping.SandGlass.IsPressed(e))
                MastSandGlass = !MastSandGlass;
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

        private void CheckChange<T>(ref T value, T newValue) where T : struct
        {
            if (!value.Equals(newValue))
            {
                value = newValue;
                Calced = false;
            }
        }
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
                Debug.LogWarning($"{nameof(RoundRoadTools)}.{nameof(Build)}");

                foreach (var segment in CalcResult.Segments)
                    CreateSegment(segment);

                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"Не удалось построить: {error.Message}");
                return false;
            }
        }

        public ushort CreateSegment(Segment segment)
        {
            CreateNode(segment.Start);
            CreateNode(segment.End);

            Debug.LogWarning($"Создание сегмента\n{segment}");
            var random = new Randomizer();
            NetManager.CreateSegment(out ushort segmentId, ref random, NetInfo, segment.Start.NodeId, segment.End.NodeId, segment.StartDirection, segment.EndDirection, GetCurrentBuildIndex(), GetCurrentBuildIndex(), false);

            return segmentId;
        }
        public ushort CreateNode(NodePointExtended point, bool terrain = true)
        {
            if (point.NodeCreated)
                return point.NodeId;

            var position = point.ShiftPosition.ToVector3();
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

            var startPoint = new NodePoint(GetNodePosition(StartNodeId), GetNodeDirection(StartNodeId, StartSegmentId)) { NodeId = StartNodeId };
            var endPoint = new NodePoint(GetNodePosition(EndNodeId), GetNodeDirection(EndNodeId, EndSegmentId)) { NodeId = EndNodeId };

            try
            {
                CalcResult = CalculateRoad(startPoint, endPoint, Radius * U, SegmentLenght * U, MinSegmentLenght * U, MastSandGlass, new float[] { 3f, 6f, 9f }, (s) => Debug.Log(s));
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

        public static CalcResult CalculateRoad(NodePoint start, NodePoint end, float radius, float partLength, float minLength, bool mastSandGlass, float[] shifts, Action<string> log = null)
        {
            log?.Invoke("Рассчет дороги");

            var foundRound = FoundRound(start, end, radius, mastSandGlass, log: log);

            var startStraight = CalculateStraightParts(start.Position, foundRound.StartRoundPos, start.Direction, partLength, minLength);
            var endStraight = CalculateStraightParts(end.Position, foundRound.EndRoundPos, end.Direction, partLength, minLength);
            var minRoundParts = Math.Max(0, shifts.Length - startStraight.Length - endStraight.Length);
            var roundDirections = CalculateRoundParts(radius, partLength, minLength, minRoundParts, foundRound, log);

            log?.Invoke($"Начальная часть - {startStraight.Length}\n{string.Join("\n", startStraight.Select(i => i.Info()).ToArray())}");
            log?.Invoke($"Закругенная часть - {roundDirections.Length}\n{string.Join("\n", roundDirections.Select(i => i.Info()).ToArray())}");
            log?.Invoke($"Конечная часть - {endStraight.Length}\n{string.Join("\n", endStraight.Select(i => i.Info()).ToArray())}");

            var points = CalculatePoints(start, end, startStraight, endStraight, roundDirections, radius, shifts, foundRound.RoundCenterPos, foundRound.IsClockWise);
            var segments = Enumerable.Range(0, points.Count - 1).Select(i => new Segment(points[i], points[i + 1].Around())).ToList();

            log?.Invoke($"Дорога расчитана: {points.Count} точек\n{string.Join("\n", points.Select(i => i.ToString()).ToArray())}");

            return new CalcResult() { Points = points, Segments = segments };
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
        public static List<NodePointExtended> CalculatePoints(NodePoint start, NodePoint end, Vector2[] startStraight, Vector2[] endStraight, Vector2[] roundDirections, float radius, float[] shifts, Vector2 roundCenterPos, bool isClockWise)
        {
            var points = new List<NodePointExtended>();
            var shiftsIndex = CalculateShiftsIndex(shifts.Length, startStraight.Length, roundDirections.Length, endStraight.Length);

            var startPartShiftDir = start.Direction.Turn(90, false).normalized;
            foreach (var i in Enumerable.Range(0, startStraight.Length))
            {
                var shift = startPartShiftDir * GetShift(points.Count);
                points.Add(new NodePointExtended(startStraight[i], start.Direction, shift));
            }

            foreach (var i in Enumerable.Range(0, roundDirections.Length))
            {
                var nodePos = roundCenterPos + roundDirections[i] * radius;
                var nodeDir = roundDirections[i].Turn(90, isClockWise).normalized;
                var shift = roundDirections[i] * GetShift(points.Count) * (isClockWise ? 1 : -1);
                points.Add(new NodePointExtended(nodePos, nodeDir, shift));
            }

            var endPartAroundDir = end.Direction.Turn(90, true).normalized;
            foreach (var i in Enumerable.Range(1, endStraight.Length))
            {
                var shift = endPartAroundDir * GetShift(points.Count);
                points.Add(new NodePointExtended(endStraight[endStraight.Length - i], end.Direction, shift, nodeDir: NodeDir.Backward));
            }

            float GetShift(int index)
            {
                var shiftIndex = shiftsIndex[index];
                return shiftIndex == -1 ? 0 : shifts[shiftIndex];
            }

            points.Insert(0, new NodePointExtended(start.Position, start.Direction) { NodeId = start.NodeId });
            points.Add(new NodePointExtended(end.Position, end.Direction, nodeDir: NodeDir.Backward) { NodeId = end.NodeId });

            return points;
        }
        public static Vector2[] CalculateStraightParts(Vector2 startPos, Vector2 endPos, Vector2 direction, float partLength, float minLength)
        {
            var distance = Vector2.Distance(startPos, endPos);
            var parts = GetPartCount(distance, partLength);
            if (distance / parts < minLength)
                parts -= 1;

            var result = Enumerable.Range(1, parts).Select(i => startPos + direction * (distance / parts * i)).ToArray();
            return result;
        }
        public static Vector2[] CalculateRoundParts(float radius, float partLength, float minLength, int minParts, FoundRoundResult foundRound, Action<string> log = null)
        {
            var distance = GetRoundDistance(radius, foundRound.Angle);
            var parts = Math.Max(minParts, GetPartCount(distance, partLength));
            if (parts > Math.Max(1, minParts) && distance / parts < minLength)
                parts -= 1;

            var startVector = (foundRound.StartRoundPos - foundRound.RoundCenterPos).normalized;

            log?.Invoke($"{nameof(distance)} = {distance}");
            log?.Invoke($"{nameof(parts)} = {parts}");
            log?.Invoke($"{nameof(startVector)} = {startVector.Info()}");
            log?.Invoke($"{nameof(foundRound.Angle)} = {foundRound.Angle}");
            log?.Invoke($"{nameof(foundRound.IsClockWise)} = {foundRound.IsClockWise}");

            var result = Enumerable.Range(1, parts - 1).Select(i => startVector.Turn(foundRound.Angle / parts * i, foundRound.IsClockWise).normalized).ToArray();
            return result;
        }
        public static int[] CalculateShiftsIndex(int shifts, int starts, int rounds, int ends)
        {
            var roundPartsShifts = Math.Min(rounds, shifts);
            var startPartsShifts = Math.Min(starts, shifts - roundPartsShifts);

            var shiftsIndex = new List<int>();

            shiftsIndex.AddRange(Enumerable.Range(0, starts).Select(i => Math.Max(-1, i - (starts - startPartsShifts))));
            shiftsIndex.AddRange(Enumerable.Range(0, rounds).Select(i => Math.Min(shifts - 1, startPartsShifts + i)));
            shiftsIndex.AddRange(Enumerable.Range(0, ends).Select(i => Math.Min(shifts - 1, startPartsShifts + roundPartsShifts + i)));

            return shiftsIndex.ToArray();
        }
        public static float GetRoundDistance(float radius, float angle) => radius * angle * Mathf.Deg2Rad;
        public static int GetPartCount(float distance, float length) => (int)Mathf.Ceil(distance / length);
        public static bool IsClockWise(Vector2 startDir, Vector2 startRoundPoint, Vector2 roundCenter)
        {
            var v1 = startDir.Turn(90, true).normalized;
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
    public struct CalcResult
    {
        public List<NodePointExtended> Points;
        public List<Segment> Segments;
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
        public static string Info(this Vector2 vector) => $"{vector.x}; {vector.y}";

        public static Vector3 ToVector3(this Vector2 vector, float y = 0, bool norm = false)
        {
            var vector3 = new Vector3(vector.x, y, vector.y);
            return norm ? vector3.normalized : vector3;
        }
        public static Vector3 ToVector3Terrain(this Vector2 vector) => vector.ToVector3(280/*Singleton<TerrainManager>.instance.SampleRawHeight(vector.x, vector.y)*/);
    }

}
