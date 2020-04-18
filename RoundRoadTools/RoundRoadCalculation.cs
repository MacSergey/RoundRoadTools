using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using Mod.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mod
{
    public class RoundRoadCalculation
    {
        public static Dictionary<int, Line[]> LineShifts { get; } = new Dictionary<int, Line[]>
        {
            {1, new Line[] {
                new Line(0, "C"), 
                new Line(0.5f, "1R0P"), 
                new Line(1.0f , "1R"), 
                new Line(2.0f , "1R2"), 
                new Line(2.5f , "1R2P"), 
                new Line(3.0f, "1R3"), 
                new Line(3.5f, "1R3P"), 
                new Line(4.0f, "1R4"), 
                new Line(4.5f, "1R4P"), 
                new Line(5.0f, "1R5"), 
                new Line(5.5f, "1R5P") 
            } },
            {2, new Line[] {
                new Line(1.0f, "2R"),
                new Line(2.0f, "2R3"),
                new Line(3.5f, "2R4P"),
            } }
        };

        public Param<float> Radius { get; } = new Param<float>(0, 2f, 100f);
        public Param<float> SegmentLenght { get; } = new Param<float>(5, 2, 10);
        public Param<bool> MustSandGlass { get; } = new Param<bool>(false, false, true);
        public Param<int> StartShift { get; } = new Param<int>(0, 0, 20);
        public Param<int> EndShift { get; } = new Param<int>(0, 0, 20);
        public Param<int> LineCount { get; } = new Param<int>(1, 1, 6);
        public Param<int> ShiftBegin { get; } = new Param<int>(0, 0, 200);
        public Param<int> MinSegmentLenght { get; } = new Param<int>(2, 2, 5);


        public float RadiusProcessed => Radius * RoundRoadTools.U;
        public float SegmentLenghtProcessed => SegmentLenght * RoundRoadTools.U;
        public Line[] Shifts
        {
            get
            {
                if (!LineShifts.TryGetValue(LineCount, out Line[] lines))
                    return new Line[0];

                var delta = EndShift - StartShift;

                var shifts = (delta >= 0 ? lines.Skip(StartShift).Take(delta) : lines.Skip(EndShift).Take(-delta).Reverse()).ToArray();
                return shifts;
            }
        }
        public float[] ShiftsValue => Shifts.Any() ? Shifts.Select(l => l.Shift * RoundRoadTools.P).ToArray() : new float[] { 0 };


        public List<NodePointExtended> CalculateRoad(NodePoint startRaw, NodePoint endRaw, Action<string> log = null)
        {
            var radius = RadiusProcessed;
            var shifts = ShiftsValue;
            var shiftBegin = ShiftBegin;
            var segmentLenght = SegmentLenghtProcessed;
            var mastSandGlass = MustSandGlass;
            var minSegmentLength = MinSegmentLenght;

            log?.Invoke("Рассчет дороги");
            log?.Invoke($"{nameof(startRaw)} = {startRaw}");
            log?.Invoke($"{nameof(endRaw)} = {endRaw}");
            log?.Invoke($"{nameof(radius)} = {radius}");
            log?.Invoke($"{nameof(segmentLenght)} = {segmentLenght}");
            log?.Invoke($"{nameof(minSegmentLength)} = {minSegmentLength}");
            log?.Invoke($"{nameof(mastSandGlass)} = {mastSandGlass}");
            log?.Invoke($"{nameof(shifts)} = {string.Join(",", shifts.Select(i => i.ToString()).ToArray())}");


            var startShifted = CalculateShiftPoint(startRaw, shifts.First(), Direction.Forward);
            var endShifted = CalculateShiftPoint(endRaw, shifts.Last(), Direction.Backward);

            var foundRound = FoundRound(startShifted, endShifted, radius, mastSandGlass, log: log);

            var startPart = CalculateStraightPoints(startShifted.Position, foundRound.StartRoundPos, startShifted.Direction, segmentLenght, minSegmentLength, false);
            var endPart = CalculateStraightPoints(endShifted.Position, foundRound.EndRoundPos, endShifted.Direction, segmentLenght, minSegmentLength, true);
            var minRoundParts = Math.Max(0, shifts.Length - 1 - startPart.Count - endPart.Count);
            var roundPart = CalculateRoundPoints(radius, segmentLenght, minSegmentLength, minRoundParts, foundRound);

            log?.Invoke($"Начальная часть - {startPart.Count}\n{string.Join("\n", startPart.Select(i => i.ToString()).ToArray())}");
            log?.Invoke($"Закругленная часть - {roundPart.Count}\n{string.Join("\n", roundPart.Select(i => i.ToString()).ToArray())}");
            log?.Invoke($"Конечная часть - {endPart.Count}\n{string.Join("\n", endPart.Select(i => i.ToString()).ToArray())}");

            var points = CalculatePoints(startShifted, -endShifted, startPart, endPart, roundPart, shifts, shiftBegin);

            if (shiftBegin + shifts.Length - 1 > points.Count - 1)
                ShiftBegin.Value = points.Count - shifts.Length;

            //var segments = CalculateSegments(points);

            //log?.Invoke($"Дорога расcчитана: {segments.Count} сегментов\n{string.Join("\n", segments.Select((s, i) => $"[{i + 1}]\n{s}").ToArray())}");

            //if (shiftBegin + shifts.Length - 1 > segments.Count)
            //    ShiftBegin.Value = segments.Count - shifts.Length + 1;

            return points;
        }
        public NodePoint CalculateShiftPoint(NodePoint point, float shift, Direction nodeDir)
        {
            var shiftVector = point.Direction.Turn90(nodeDir == Direction.Forward).normalized * shift;
            var shiftPoint = new NodePoint(point.Position + shiftVector, point.Direction, point.NodeId, point.Height);
            return shiftPoint;
        }
        public FoundRoundResult FoundRound(NodePoint start, NodePoint end, float radius, bool mastSandGlass = false, Action<string> log = null)
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
        public List<NodePointExtended> CalculatePoints(NodePoint startPoint, NodePoint endPoint, List<NodePoint> startPart, List<NodePoint> endPart, List<NodePoint> roundPart, float[] shifts, int shiftBegin)
        {
            var pointsShift = CalculateShifts(shifts, shiftBegin, startPart.Count, endPart.Count, roundPart.Count);

            var points = new List<NodePointExtended>();

            points.Add(CreatePoint(startPoint));
            points.AddRange(startPart.Select(p => CreatePoint(p)));
            points.AddRange(roundPart.Select(p => CreatePoint(p)));
            points.AddRange(endPart.Select(p => CreatePoint(p)));
            points.Add(CreatePoint(endPoint));

            NodePointExtended CreatePoint(NodePoint p)
            {
                var shift = pointsShift[points.Count];
                var shiftVector = p.Direction.Turn90(true) * shift;
                return new NodePointExtended(p.Position - shiftVector, p.Direction, shiftVector, p.NodeId, p.Height);
            }

            CalculateHeight(points);

            return points;
        }
        public List<Segment> CalculateSegments(List<NodePointExtended> points) => SelectRange(0, points.Count - 1, i => new Segment(points[i], points[i + 1])).ToList();
        public List<float> CalculateShifts(float[] shifts, int shiftBegin, int start, int end, int round)
        {
            var pointsCount = 1 + start + round + end + 1;

            var startTransitions = shiftBegin + shifts.Length - 1 <= pointsCount ? shiftBegin : pointsCount - shifts.Length;
            var endTransitions = startTransitions + shifts.Length;

            var pointsShift = new List<float>(pointsCount);

            pointsShift.AddRange(SelectRange(0, startTransitions, i => shifts.First()));
            pointsShift.AddRange(shifts);
            pointsShift.AddRange(SelectRange(0, pointsCount - endTransitions, i => shifts.Last()));

            return pointsShift;
        }
        public void CalculateHeight(List<NodePointExtended> points)
        {
            var lengths = SelectRange(0, points.Count - 1, i => CalcucalePartLenght(points[i], points[i + 1])).ToArray();
            var sumLength = lengths.Sum();

            var startHeight = points.First().Height;
            var endHeight = points.Last().Height;
            var heightDelta = endHeight - startHeight;

            Enumerable.Range(1, Math.Max(0, points.Count - 2)).Aggregate(0f, AggregateFunc);

            float AggregateFunc(float currentLenth, int i)
            {
                currentLenth += lengths[i];
                points[i].Height = startHeight + heightDelta * (currentLenth / sumLength);
                return currentLenth;
            }
        }
        public float CalcucalePartLenght(NodePoint point1, NodePoint point2)
        {
            var bezier = new Bezier3 { a = point1.Position.ToVector3(), d = point2.Position.ToVector3() };
            NetSegment.CalculateMiddlePoints(bezier.a, point1.Direction.ToVector3(), bezier.d, -point2.Direction.ToVector3(), true, true, out bezier.b, out bezier.c);

            return BezierDistance(bezier);
        }
        public float BezierDistance(Bezier3 bezier, int accuracy = 10)
        {
            var parts = Math.Max(1, accuracy);
            var points = SelectRange(0, parts + 1, i => bezier.Position(i / (float)parts)).ToArray();
            var distance = SelectRange(0, points.Length - 1, i => (points[i + 1] - points[i]).magnitude).Sum();
            return distance;
        }
        public List<NodePoint> CalculateStraightPoints(Vector2 startPos, Vector2 endPos, Vector2 direction, float partLength, float minLength, bool flip)
        {
            var distance = GetStraightDistance(startPos, endPos);
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
        public List<NodePoint> CalculateRoundPoints(float radius, float partLength, float minLength, int minParts, FoundRoundResult foundRound)
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
        public float GetRoundDistance(float radius, float angle) => radius * angle * Mathf.Deg2Rad;
        public float GetStraightDistance(Vector2 startPos, Vector2 endPos) => Vector2.Distance(startPos, endPos);
        public int GetPartCount(float distance, float length) => (int)Mathf.Ceil(distance / length);
        public bool IsClockWise(Vector2 startDir, Vector2 startRoundPoint, Vector2 roundCenter)
        {
            var v1 = startDir.Turn90(true).normalized;
            var v2 = (startRoundPoint - roundCenter).normalized;
            return (v1 + v2).magnitude < 0.001f;
        }
        private IEnumerable<TResult> SelectRange<TResult>(int start, int count, Func<int, TResult> selector) => Enumerable.Range(start, Math.Max(0, count)).Select(selector);
    }
}
