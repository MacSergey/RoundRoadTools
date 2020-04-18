using ColossalFramework.Math;
using ColossalFramework.PlatformServices;
using Mod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            TestMethod(-46.139f, -312.879f, 0.407f, 0.913f, 1, -35.763f, -414.584f, 0.863f, 0.504f, 1,  5, false, -41.387f, -380.809f);
        }
        static void TestMethod(float sx, float sz, float sdx, float sdz, int sm, float ex, float ez, float edx, float edz, int em, int r, bool cw, float? rx = null, float? rz = null)
        {
            var StartShiftRaw = 1;
            var EndShiftRaw = 1;
            var delta = EndShiftRaw - StartShiftRaw;
            var middleShifts = Enumerable.Range(1, Math.Max(0,Math.Abs(delta) - 1)).Select(i => ((float)(StartShiftRaw + Math.Sign(delta) * i)) / 2 * RoundRoadTools.P).ToArray();

            var startNode = new NodePoint(sx, sz, sdx, sdz, mode: (NodeDir)sm);
            var endNode = new NodePoint(ex, ez, edx, edz, mode: (NodeDir)em);

            var calcRoadResult = RoundRoadTools.CalculateRoad(startNode, endNode, r * RoundRoadTools.U, 5 * RoundRoadTools.U, RoundRoadTools.MinSegmentLenght * RoundRoadTools.U, false, 3, 3, new float[0], (s) => Console.WriteLine(s));
        }
    }
}
