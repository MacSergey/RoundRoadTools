using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
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
        private float _radius = 4;
        #endregion

        private bool Calced { get; set; }

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
        private float Radius
        {
            get => _radius;
            set => CheckChange(ref _radius, value);
        }

        private bool StartNodeSelected => StartNodeId != 0;
        private bool EndNodeSelected => EndNodeId != 0;
        private bool StartSegmentSelected => StartSegmentId != 0;
        private bool EndSegmentSelected => EndSegmentId != 0;

        private SelectedStatus NodeStatus => (StartNodeSelected ? SelectedStatus.Start : SelectedStatus.None) | (EndNodeSelected ? SelectedStatus.End : SelectedStatus.None);
        private SelectedStatus SegmentStatus => (StartSegmentSelected ? SelectedStatus.Start : SelectedStatus.None) | (EndSegmentSelected ? SelectedStatus.End : SelectedStatus.None);

        private bool CanBuild => NodeStatus == SelectedStatus.All && SegmentStatus == SelectedStatus.All;

        private NodePoint[] CalcPoints { get; set; }
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
        }

        #endregion

        #region UPDATE

        protected override void OnToolUpdate()
        {
            Debug.Log($"{nameof(RoundRoadTools)}.{nameof(OnToolUpdate)}");
            base.OnToolUpdate();

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastInput input = new RaycastInput(ray, Camera.main.farClipPlane)
            {
                m_ignoreTerrain = false,
                m_ignoreNodeFlags = NetNode.Flags.None
            };
            RayCast(input, out RaycastOutput output);

            GetHovered();
            Debug.Log($"{nameof(HoverNodeId)} = {HoverNodeId}");
            Debug.Log($"{nameof(HoverSegmentId)} = {HoverSegmentId}");

            InputCheck();
            Debug.Log($"{nameof(StartNodeId)} = {StartNodeId}");
            Debug.Log($"{nameof(EndNodeId)} = {EndNodeId}");

            var startPoint = new NodePoint(GetNodePosition(StartNodeId), GetNodeDirection(StartNodeId)) { NodeId = StartNodeId };
            var endPoint = new NodePoint(GetNodePosition(EndNodeId), GetNodeDirection(EndNodeId)) { NodeId = EndNodeId };
            ShowInfo($"{startPoint}\n{endPoint}", output.m_hitPos);

            Calculate();
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
            switch (NodeStatus)
            {
                case SelectedStatus.None:
                    StartNodeId = HoverNodeId;
                    break;
                case SelectedStatus.Start:
                    EndNodeId = HoverNodeId;
                    break;
            }
            switch (SegmentStatus)
            {
                case SelectedStatus.None:
                    StartSegmentId = HoverSegmentId;
                    break;
                case SelectedStatus.Start:
                    EndSegmentId = HoverSegmentId;
                    break;
            }
        }
        void OnMouseButton1Up()
        {
            switch (NodeStatus)
            {
                case SelectedStatus.Start:
                    StartNodeId = 0;
                    break;
                case SelectedStatus.All:
                    EndNodeId = 0;
                    break;
            }
            switch (SegmentStatus)
            {
                case SelectedStatus.Start:
                    StartSegmentId = 0;
                    break;
                case SelectedStatus.All:
                    EndSegmentId = 0;
                    break;
            }
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
            Render(StartNodeId, StartNodeSelected, StartColor, StartHoverColor);
            Render(EndNodeId, EndNodeSelected, EndColor, EndHoverColor);

            if (HoverNodeId != 0 && NodeStatus != SelectedStatus.All && HoverNodeId != StartNodeId && HoverNodeId != EndNodeId)
                RenderNodeOverlay(HoverNodeId, HoverColor, cameraInfo);

            void Render(ushort nodeId, bool nodeSelected, Color32 color, Color32 hoverColor)
            {
                if (nodeSelected)
                    RenderNodeOverlay(nodeId, nodeId == HoverNodeId ? hoverColor : color, cameraInfo);
            }
        }
        private void RenderSegmentsOverlay(RenderManager.CameraInfo cameraInfo)
        {
            Render(StartSegmentId, StartSegmentSelected, StartColor, StartHoverColor);
            Render(EndSegmentId, EndSegmentSelected, EndColor, EndHoverColor);

            if (HoverSegmentId != 0 && SegmentStatus != SelectedStatus.All && HoverSegmentId != StartSegmentId && HoverSegmentId != EndSegmentId)
                RenderSegmentOverlay(HoverSegmentId, HoverColor, cameraInfo);

            void Render(ushort segmentId, bool segmentSelected, Color32 color, Color32 hoverColor)
            {
                if (segmentSelected)
                    RenderSegmentOverlay(segmentId, segmentId == HoverSegmentId ? hoverColor : color, cameraInfo);
            }
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

            for (var i = 0; i < CalcPoints.Length - 1; i += 1)
            {
                //RenderRoadSegment(CalcPoints[i], CalcPoints[i + 1]);
                RenderRoadSegmentOverlay(CalcPoints[i], CalcPoints[i + 1], cameraInfo);              
            }
        }

        private void RenderRoadSegmentOverlay(NodePoint start, NodePoint end, RenderManager.CameraInfo cameraInfo)
        {
            var bezier = new Bezier3
            {
                a = start.Position.ToVector3Terrain(),
                d = end.Position.ToVector3Terrain()
            };
            NetSegment.CalculateMiddlePoints(bezier.a, start.Direction.ToVector3().normalized, bezier.d, -end.Direction.ToVector3().normalized, true, true, out bezier.b, out bezier.c);

            var color = HoverColor;
            color.a = GetAlpha(NetInfo);

            Debug.Log($"Рендер сегмента\n{start}\n{end}\n{bezier.a};{bezier.b};{bezier.c};{bezier.d}");
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
                //Debug.Log($"{nameof(surfaceTexA)} - {surfaceTexA != null}");
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
            //Debug.Log($"{nameof(RoundRoadTools)}.{nameof(OnToolGUI)}");
            if (!enabled)
                return;
        }


        #region UTILITIES

        private void ShowInfo(string text, Vector3 worldPos)
        {
            UIView uIView = extraInfoLabel.GetUIView();
            var a = Camera.main.WorldToScreenPoint(worldPos) / uIView.inputScale;
            extraInfoLabel.relativePosition = uIView.ScreenPointToGUI(a) - extraInfoLabel.size * 0.5f;

            extraInfoLabel.isVisible = true;
            extraInfoLabel.textColor = GetToolColor(false, false);
            extraInfoLabel.text = text ?? string.Empty;
        }
        private void HideInfo() => extraInfoLabel.isVisible = false;
        private void ShowToolInfo(string text, Vector3 worldPos)
        {
            UIView uIView = cursorInfoLabel.GetUIView();
            var screenSize = fullscreenContainer == null ? uIView.GetScreenResolution() : fullscreenContainer.size;
            var v = Camera.main.WorldToScreenPoint(worldPos) / uIView.inputScale;
            var vector2 = cursorInfoLabel.pivot.UpperLeftToTransform(cursorInfoLabel.size, cursorInfoLabel.arbitraryPivotOffset);
            var relativePosition = uIView.ScreenPointToGUI(v) + new Vector2(vector2.x, vector2.y);
            relativePosition.x = MathPos(relativePosition.x, cursorInfoLabel.width, screenSize.x);
            relativePosition.y = MathPos(relativePosition.y, cursorInfoLabel.height, screenSize.y);

            cursorInfoLabel.isVisible = true;
            cursorInfoLabel.text = text ?? string.Empty;

            float MathPos(float pos, float size, float screen) => pos + size > screen ? (screen - size < 0 ? 0 : screen - size) : pos;
        }
        private void HideToolInfo() => cursorInfoLabel.isVisible = false;
        private NetNode GetNode(ushort nodeId) => NetManager.m_nodes.m_buffer[nodeId];
        private NetSegment GetSegment(ushort segmentId) => NetManager.m_segments.m_buffer[segmentId];
        private Vector3 GetNodePosition(ushort nodeId) => GetNode(nodeId).m_position;
        private Vector3 GetNodeDirection(ushort nodeId)
        {
            var node = GetNode(nodeId);

            for (int i = 0; i < 8; i += 1)
            {
                var segmentId = node.GetSegment(i);
                if (segmentId != 0)
                {
                    var segment = GetSegment(segmentId);
                    if (segment.m_startNode == nodeId)
                        return -segment.m_startDirection;
                    else if (segment.m_endNode == nodeId)
                        return -segment.m_endDirection;
                }
            }
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

                foreach (var point in CalcPoints)
                    CreateNode(point);

                //CreateSegment(StartPoint, CalcPoints[0]);
                for (int i = 0; i < CalcPoints.Length - 1; i += 1)
                    CreateSegment(CalcPoints[i], CalcPoints[i + 1]);
                //CreateSegment(CalcPoints[CalcPoints.Length - 1], EndPoint, false);

                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"Не удалось построить: {error.Message}");
                return false;
            }
        }

        public ushort CreateSegment(NodePoint start, NodePoint end, bool invertEnd = true)
        {
            CreateNode(start);
            CreateNode(end);

            var startDir = start.Direction.ToVector3();
            var endDir = (invertEnd ? -end.Direction : end.Direction).ToVector3();

            Debug.LogError($"Создание сегмента\nНачало: {start.Position}; {startDir}\nКонец: {end.Position}; {endDir}");
            var random = new Randomizer();
            NetManager.CreateSegment(out ushort segmentId, ref random, NetInfo, start.NodeId, end.NodeId, startDir, endDir, GetCurrentBuildIndex(), GetCurrentBuildIndex(), false);

            return segmentId;
        }
        public ushort CreateNode(NodePoint point, bool terrain = true)
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

            var startPoint = new NodePoint(GetNodePosition(StartNodeId), GetNodeDirection(StartNodeId)) { NodeId = StartNodeId };
            var endPoint = new NodePoint(GetNodePosition(EndNodeId), GetNodeDirection(EndNodeId)) { NodeId = EndNodeId };

            CalcPoints = CalculateRoad(startPoint, endPoint, Radius * U, 5 * U, new float[0], (s) => Debug.Log(s));

            Calced = true;
        }

        public static NodePoint[] CalculateRoad(NodePoint start, NodePoint end, float radius, float partLength, float[] shifts, Action<string> log = null)
        {
            log?.Invoke("Рассчет дороги");

            var foundRound = FoundRound(start, end, radius, log: log);

            var startStraight = CalculateStraightParts(start.Position, foundRound.StartRoundPos, start.Direction, partLength);
            var endStraight = CalculateStraightParts(end.Position, foundRound.EndRoundPos, end.Direction, partLength);
            var minRoundParts = Math.Max(0, shifts.Length - startStraight.Length - endStraight.Length);
            var roundDirections = CalculateRoundParts(radius, partLength, minRoundParts, foundRound, log);

            log?.Invoke($"Начальная часть - {startStraight.Length}\n{string.Join("\n", startStraight.Select(i => i.Info()).ToArray())}");
            log?.Invoke($"Закругенная часть - {roundDirections.Length}\n{string.Join("\n", roundDirections.Select(i => i.Info()).ToArray())}");
            log?.Invoke($"Конечная часть - {endStraight.Length}\n{string.Join("\n", endStraight.Select(i => i.Info()).ToArray())}");

            //====
            //var foundRound1 = FoundRound(start, end, radius, log: log);
            //var foundRound2 = FoundRound(end, start, radius, log: log);

            //var list1 = new List<NodePoint>();
            //var list2 = new List<NodePoint>();

            //var roundDirections1 = CalculateRoundParts(radius, partLength, minRoundParts, foundRound1);
            //var roundDirections2 = CalculateRoundParts(radius, partLength, minRoundParts, foundRound2);

            //foreach (var i in Enumerable.Range(0, roundDirections1.Length))
            //{
            //    var shift = 0;
            //    var nodePos = foundRound1.RoundCenterPos + roundDirections1[i] * (radius + (foundRound1.IsClockWise ? shift : -shift));
            //    var nodeDir = roundDirections1[i].Turn(90, foundRound1.IsClockWise).normalized;
            //    list1.Add(new NodePoint(nodePos, nodeDir, NodeDir.Forward));
            //}

            //foreach (var i in Enumerable.Range(0, roundDirections2.Length))
            //{
            //    var shift = 0;
            //    var nodePos = foundRound2.RoundCenterPos + roundDirections2[i] * (radius + (foundRound2.IsClockWise ? shift : -shift));
            //    var nodeDir = roundDirections2[i].Turn(90, foundRound2.IsClockWise).normalized;
            //    list2.Add(new NodePoint(nodePos, nodeDir, NodeDir.Forward));
            //}
            //===

            var points = new List<NodePoint>();
            var shiftsIndex = CalculateShiftsIndex(shifts.Length, startStraight.Length, roundDirections.Length, endStraight.Length);

            var startPartShiftDir = start.Direction.Turn(90, false).normalized;
            foreach (var i in Enumerable.Range(0, startStraight.Length))
            {
                var shift = GetShift(points.Count);
                var nodePos = startStraight[i] + startPartShiftDir * shift;
                points.Add(new NodePoint(nodePos, start.Direction, NodeDir.Forward));
            }

            foreach (var i in Enumerable.Range(0, roundDirections.Length))
            {
                var shift = GetShift(points.Count);
                var nodePos = foundRound.RoundCenterPos + roundDirections[i] * (radius + (foundRound.IsClockWise ? shift : -shift));
                var nodeDir = roundDirections[i].Turn(90, foundRound.IsClockWise).normalized;
                points.Add(new NodePoint(nodePos, nodeDir, NodeDir.Forward));
            }

            var endPartShiftDir = end.Direction.Turn(90, true).normalized;
            foreach (var i in Enumerable.Range(1, endStraight.Length))
            {
                var shift = GetShift(points.Count);
                var nodePos = endStraight[endStraight.Length - i] + endPartShiftDir * shift;
                points.Add(new NodePoint(nodePos, end.Direction, NodeDir.Backward));
            }

            float GetShift(int index)
            {
                var shiftIndex = shiftsIndex[index];
                return shiftIndex == -1 ? 0 : shifts[shiftIndex];
            }

            points.Insert(0, new NodePoint(start.Position, start.Direction) { NodeId = start.NodeId});
            points.Add(new NodePoint(end.Position, end.Direction, NodeDir.Backward) { NodeId = end.NodeId});

            log?.Invoke($"Дорога расчитана: {points.Count} точек\n{string.Join("\n", points.Select(i => i.ToString()).ToArray())}");

            return points.ToArray();
        }
        public static FoundRoundResult FoundRound(NodePoint start, NodePoint end, float radius, bool beSandGlass = false, Action<string> log = null)
        {
            log?.Invoke("Поиск круга");

            log?.Invoke($"{nameof(start)} = {start}");
            log?.Invoke($"{nameof(end)} = {end}");
            log?.Invoke($"{nameof(radius)} = {radius}");
            log?.Invoke($"{nameof(beSandGlass)} = {beSandGlass}");

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
                if (dist >= 0 && (dist < -startPointXDist || dist < -endPointXDist) & !beSandGlass)
                {
                    dist = -dist;
                    isSandGlass = false;
                }
            }
            else if (dist < startPointXDist || dist < endPointXDist)
                    throw new RoadSmallRadiusException();

            var startRoundPos = start.Position + start.Direction * (dist - startPointXDist);
            var endRoundPos = end.Position + end.Direction * (dist - endPointXDist);
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
                //Angle = 360 - (isSandGlass ? angle : 180 - angle),
                Angle = 180 + (isSandGlass ? angle : -angle), 
                IsSandGlass = isSandGlass,
                IsClockWise = isClockWise
            };
            return result;
        }
        public static Vector2[] CalculateStraightParts(Vector2 startPos, Vector2 endPos, Vector2 direction, float partLength)
        {
            var distance = Vector2.Distance(startPos, endPos);
            var parts = GetPartCount(distance, partLength);

            var result = Enumerable.Range(1, parts).Select(i => startPos + direction * (distance / parts * i)).ToArray();
            return result;
        }
        public static Vector2[] CalculateRoundParts(float radius, float partLength, int minParts, FoundRoundResult foundRound, Action<string> log = null)
        {
            var distance = GetRoundDistance(radius, foundRound.Angle);
            var parts = Math.Max(minParts, GetPartCount(distance, partLength));
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
            return (v1 + v2).magnitude < 0.001f ;
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

        public NodePoint(Vector2 position, Vector2 direction, NodeDir mode = NodeDir.Forward)
        {
            Position = position;
            Direction = (mode == NodeDir.Forward ? direction : -direction).normalized;
        }
        public NodePoint(Vector3 position, Vector3 direction, NodeDir mode = NodeDir.Forward) : this(VectorUtils.XZ(position), VectorUtils.XZ(direction), mode) { }
        public NodePoint(float positionX, float positionZ, float directionX, float directionZ, NodeDir mode = NodeDir.Forward) : this(new Vector2(positionX, positionZ), new Vector2(directionX, directionZ), mode) { }

        public override string ToString() => $"{nameof(Position)}: {Position.Info()}, {nameof(Direction)}: {Direction.Info()}";
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
