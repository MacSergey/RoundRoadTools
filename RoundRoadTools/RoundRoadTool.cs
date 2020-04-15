using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using ICities;
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
        static Color32 StartNodeOverlay => new Color32(95, 166, 0, 0);
        static Color32 EndNodeOverlay => new Color32(199, 72, 72, 0);

        #endregion


        #region PRIVATE

        #region MANAGERS
        NetManager NetManager => Singleton<NetManager>.instance;
        RenderManager RenderManager => Singleton<RenderManager>.instance;
        #endregion

        #region FIELDS
        ushort _startNode = 0;
        ushort _endNode = 0;
        float _radius = 0;
        #endregion

        bool NeedCalc { get; set; } = false;
        ushort HoverNode { get; set; } = 0;
        ushort StartNode
        {
            get => _startNode;
            set => CheckChange(ref _startNode, value);
        }
        ushort EndNode
        {
            get => _endNode;
            set => CheckChange(ref _endNode, value);
        }
        float Radius
        {
            get => _radius;
            set => CheckChange(ref _radius, value);
        }

        bool StartNodeSelected => StartNode != 0;
        bool EndNodeSelected => EndNode != 0;
        SelectedStatus SelectedStatus => EndNodeSelected ? SelectedStatus.End : (StartNodeSelected ? SelectedStatus.Start : SelectedStatus.None);

        #endregion


        #region PUBLIC

        public NetInfo NetInfo { get; set; }

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

            HoverNode = GetHoveredNode();
            Debug.Log($"{nameof(HoverNode)} = {HoverNode}");

            InputCheck();
            Debug.Log($"{nameof(StartNode)} = {StartNode}");
            Debug.Log($"{nameof(EndNode)} = {EndNode}");
        }

        private void InputCheck()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                enabled = false;
                ToolsModifierControl.SetTool<DefaultTool>();
            }

            if (Input.GetMouseButtonUp(0))
            {
                switch(SelectedStatus)
                {
                    case SelectedStatus.None:
                        StartNode = HoverNode;
                        break;
                    case SelectedStatus.Start:
                        EndNode = HoverNode;
                        break;
                }
            }

            if (Input.GetMouseButtonUp(1))
            {
                switch (SelectedStatus)
                {
                    case SelectedStatus.Start:
                        StartNode = 0;
                        break;
                    case SelectedStatus.End:
                        EndNode = 0;
                        break;
                }
            }
        }
        private ushort GetHoveredNode()
        {
            if (UIView.IsInsideUI() || !Cursor.visible)
                return 0;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastInput input = new RaycastInput(ray, Camera.main.farClipPlane)
            {
                m_ignoreTerrain = true,
                m_ignoreNodeFlags = NetNode.Flags.None,
                m_ignoreSegmentFlags = NetSegment.Flags.None
            };
            input.m_netService.m_itemLayers = (ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels);
            input.m_netService.m_service = ItemClass.Service.Road;

            if (!RayCast(input, out RaycastOutput output))
                return 0;

            if (output.m_netNode != 0)
                return output.m_netNode;

            if (output.m_netSegment == 0)
                return 0;

            var segment = NetManager.m_segments.m_buffer[output.m_netSegment];
            var startNode = NetManager.m_nodes.m_buffer[segment.m_startNode];
            var endNode = NetManager.m_nodes.m_buffer[segment.m_endNode];

            return Vector3.Distance(output.m_hitPos, startNode.m_position) < Vector3.Distance(output.m_hitPos, endNode.m_position) ? segment.m_startNode : segment.m_endNode;
        }

        #endregion


        #region OVERLAY

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            Debug.Log($"{nameof(RoundRoadTools)}.{nameof(RenderOverlay)}");
            if (!enabled)
                return;

            if (!StartNodeSelected)
                NodeOverlay(HoverNode, StartNodeOverlay, cameraInfo);
            else
            {
                NodeOverlay(StartNode, StartNodeOverlay, cameraInfo);
                NodeOverlay(EndNodeSelected ? EndNode : HoverNode, EndNodeOverlay, cameraInfo);
            }
        }

        private void NodeOverlay(ushort nodeId, Color32 color, RenderManager.CameraInfo cameraInfo)
        {
            if (nodeId == 0)
                return;

            var node = GetNode(nodeId);

            var alpha = 1f;
            NetTool.CheckOverlayAlpha(node.Info, ref alpha);
            color.a = (byte)(244 * alpha);

            RenderManager.OverlayEffect.DrawCircle(cameraInfo, color, node.m_position, Mathf.Max(6f, node.Info.m_halfWidth * 2f), -1f, 1280f, false, true);
        }

        #endregion


        #region GEOMETRY

        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo)
        {
            Debug.Log($"{nameof(RoundRoadTools)}.{nameof(RenderGeometry)}");
            if (!enabled)
                return;
        }

        #endregion


        protected override void OnToolGUI(Event e)
        {
            Debug.Log($"{nameof(RoundRoadTools)}.{nameof(OnToolGUI)}");
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

        private void CheckChange<T>(ref T value, T newValue) where T : struct
        {
            if(!value.Equals(newValue))
            {
                value = newValue;
                NeedCalc = true;
            }
        }

        #endregion


        #region CALCULATION

        public FoundRoundResult FoundRound(NodePoint start, NodePoint end, float radius, bool clockWay = false, Action<string> log = null)
        {
            log?.Invoke("Построение круга");
            log?.Invoke($"{nameof(start)} = {start}");
            log?.Invoke($"{nameof(end)} = {end}");
            log?.Invoke($"{nameof(radius)} = {radius}");
            log?.Invoke($"{nameof(clockWay)} = {clockWay}");

            radius *= U;

            if (!Line2.Intersect(start.Position, start.Position - start.Direction, end.Position, end.Position - end.Direction, out float startPointXDist, out float endPointXDist) || (Mathf.Abs(startPointXDist) > 10000 && Mathf.Abs(endPointXDist) > 10000))
                throw new RoadParallelLinesException();

            log?.Invoke($"{nameof(startPointXDist)} = {startPointXDist}");
            log?.Invoke($"{nameof(endPointXDist)} = {endPointXDist}");

            var xPosS = start.Position - (start.Direction * startPointXDist);
            var xPosE = end.Position - (end.Direction * endPointXDist);
            var xPos = (xPosS + xPosE) / 2;
            log?.Invoke($"{nameof(xPosS)} = {xPosS}");
            log?.Invoke($"{nameof(xPosE)} = {xPosE}");
            log?.Invoke($"{nameof(xPos)} = {xPos}");

            var angle = Mathf.Abs(Vector2.Angle(start.Direction, end.Direction));
            log?.Invoke($"{nameof(angle)} = {angle}");

            var dist = radius / Mathf.Tan(angle / 2 * Mathf.Deg2Rad);
            log?.Invoke($"{nameof(dist)} = {dist}");

            var isSandGlass = false;
            if (startPointXDist < 0 && endPointXDist < 0)
            {
                if (dist >= 0 && (dist < -startPointXDist || dist < -endPointXDist) & !clockWay)
                    dist = -dist;
                else
                    isSandGlass = true;
            }
            else if (dist < startPointXDist || dist < endPointXDist)
                throw new RoadSmallRadiusException();

            var startRoundPos = start.Position + start.Direction * (dist - startPointXDist);
            var endRoundPos = end.Position + end.Direction * (dist - endPointXDist);
            log?.Invoke($"{nameof(startRoundPos)} = {startRoundPos}");
            log?.Invoke($"{nameof(endRoundPos)} = {endRoundPos}");

            var distXO = Mathf.Sqrt(Mathf.Pow(radius, 2) + Mathf.Pow(dist, 2));
            var dirXO = Mathf.Sign(dist) * (start.Direction + end.Direction).normalized;
            log?.Invoke($"{nameof(distXO)} = {distXO}");
            log?.Invoke($"{nameof(dirXO)} = {dirXO}");

            var roundCenterPos = xPos + (distXO * dirXO);
            log?.Invoke($"{nameof(roundCenterPos)} = {roundCenterPos} ({roundCenterPos.Info()})");

            var isClockWise = IsClockWise(start.Direction, startRoundPos, roundCenterPos);

            var result = new FoundRoundResult()
            {
                RoundCenterPos = roundCenterPos,
                StartRoundPos = startRoundPos,
                EndRoundPos = endRoundPos,
                Angle = 360 - (isSandGlass ? angle : 180 - angle),
                IsSandGlass = isSandGlass,
                IsClockWise = isClockWise
            };
            return result;
        }
        public NodePoint[] CalculateRoad(NodePoint start, NodePoint end, float radius, float partLength, float[] shifts, FoundRoundResult foundRound)
        {
            radius *= U;
            var startStraight = CalculateStraightParts(start.Position, foundRound.StartRoundPos, start.Direction, partLength);
            var endStraight = CalculateStraightParts(end.Position, foundRound.EndRoundPos, end.Direction, partLength);
            var minRoundParts = Math.Max(0, shifts.Length - startStraight.Length - endStraight.Length);
            var roundDirections = CalculateRoundParts(radius, partLength, minRoundParts, foundRound);

            var result = new List<NodePoint>();
            var shiftsIndex = CalculateShiftsIndex(shifts.Length, startStraight.Length, roundDirections.Length, endStraight.Length);

            var startPartShiftDir = start.Direction.Turn(90, false).normalized;
            foreach (var i in Enumerable.Range(0, startStraight.Length))
            {
                var shift = GetShift(result.Count);
                var nodePos = startStraight[i] + startPartShiftDir * shift;
                result.Add(new NodePoint(nodePos, start.Direction, NodeDir.Forward));
            }

            foreach (var i in Enumerable.Range(0, roundDirections.Length))
            {
                var shift = GetShift(result.Count);
                var nodePos = foundRound.RoundCenterPos + roundDirections[i] * (radius + (foundRound.IsClockWise ? shift : -shift));
                var nodeDir = roundDirections[i].Turn(90, foundRound.IsClockWise);
                result.Add(new NodePoint(nodePos, nodeDir, NodeDir.Forward));
            }

            var endPartShiftDir = end.Direction.Turn(90, true).normalized;
            foreach (var i in Enumerable.Range(1, endStraight.Length))
            {
                var shift = GetShift(result.Count);
                var nodePos = endStraight[endStraight.Length - i] + endPartShiftDir * shift;
                result.Add(new NodePoint(nodePos, end.Direction, NodeDir.Backward));
            }

            float GetShift(int index)
            {
                var shiftIndex = shiftsIndex[index];
                return shiftIndex == -1 ? 0 : shifts[shiftIndex];
            }

            return result.ToArray();
        }
        public Vector2[] CalculateStraightParts(Vector2 startPos, Vector2 endPos, Vector2 direction, float partLength)
        {
            var distance = Vector2.Distance(startPos, endPos);
            var parts = GetPartCount(distance, partLength);

            var result = Enumerable.Range(1, parts).Select(i => startPos + direction * (distance / parts * i)).ToArray();
            return result;
        }
        public Vector2[] CalculateRoundParts(float radius, float partLength, int minParts, FoundRoundResult foundRound)
        {
            var distance = GetRoundDistance(radius, foundRound.Angle);
            var parts = Math.Max(minParts, GetPartCount(distance, partLength));
            var startVector = (foundRound.StartRoundPos - foundRound.RoundCenterPos).normalized;

            var result = Enumerable.Range(1, parts - 1).Select(i => startVector.Turn(foundRound.Angle / parts * i, foundRound.IsClockWise).normalized).ToArray();
            return result;
        }
        public int[] CalculateShiftsIndex(int shifts, int starts, int rounds, int ends)
        {
            var roundPartsShifts = Math.Min(rounds, shifts);
            var startPartsShifts = Math.Min(starts, shifts - roundPartsShifts);

            var shiftsIndex = new List<int>();

            shiftsIndex.AddRange(Enumerable.Range(0, starts).Select(i => Math.Max(-1, i - (starts - startPartsShifts))));
            shiftsIndex.AddRange(Enumerable.Range(0, rounds).Select(i => Math.Min(shifts - 1, startPartsShifts + i)));
            shiftsIndex.AddRange(Enumerable.Range(0, ends).Select(i => Math.Min(shifts - 1, startPartsShifts + roundPartsShifts + i)));

            return shiftsIndex.ToArray();
        }
        public float GetRoundDistance(float radius, float angle) => radius * angle * Mathf.Deg2Rad;
        public int GetPartCount(float distance, float length) => (int)Mathf.Ceil(distance / length);
        public bool IsClockWise(Vector2 startDir, Vector2 startRoundPoint, Vector2 roundCenter)
        {
            var v1 = startDir.Turn(90, false).normalized;
            var v2 = (startRoundPoint - roundCenter).normalized;
            return v1 + v2 != Vector2.zero;
        }

        #endregion
    }

    [Flags]
    public enum SelectedStatus
    {
        None = 0,
        Start = 1,
        End = 3,
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

        public NodePoint(Vector2 point, Vector2 direction, NodeDir mode)
        {
            Position = point;
            Direction = (mode == NodeDir.Forward ? direction : -direction).normalized;
        }
        public NodePoint(Vector3 point, Vector3 direction, NodeDir mode) : this(VectorUtils.XZ(point), VectorUtils.XZ(direction), mode) { }
        public NodePoint(float pointX, float pointZ, float directionX, float directionZ, NodeDir mode) : this(new Vector2(pointX, pointZ), new Vector2(directionX, directionZ), mode) { }

        public override string ToString() => $"{nameof(Position)}: {Position}, {nameof(Direction)}: {Direction}";
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
        public static string Info(this Vector2 vector) => $"{vector.x:F3}; {vector.y:F3}";
    }
}
