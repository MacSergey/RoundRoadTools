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
            TestMethod(-46.139f, -312.879f, -0.407f, -0.913f, 1, -35.763f, -414.584f, -0.863f, -0.504f, 1, 5, false, -41.387f, -380.809f);
        }
        static void TestMethod(float sx, float sz, float sdx, float sdz, int sm, float ex, float ez, float edx, float edz, int em, int r, bool cw, float? rx = null, float? rz = null)
        {
            var startNode = new NodePoint(sx, sz, sdx, sdz, height: 260);
            var endNode = new NodePoint(ex, ez, edx, edz, height: 272);

            var calculation = new RoundRoadCalculation();
            calculation.Radius.Value = r;
            calculation.SegmentLenght.Value = 5;

            var calcRoadResult = calculation.CalculateRoad(startNode, endNode, (s) => Console.WriteLine(s));
        }
    }
}
