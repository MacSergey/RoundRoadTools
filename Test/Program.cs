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
            TestMethod(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.407f, 0.913f, 1, 4, false, -41.387f, -380.809f);
        }
        static void TestMethod(float sx, float sz, float sdx, float sdz, int sm, float ex, float ez, float edx, float edz, int em, int r, bool cw, float? rx = null, float? rz = null)
        {

            //mod.CalculateShiftsIndex(1, 2, 1, 2);

            var startNode = new NodePoint(sx, sz, sdx, sdz, (NodeDir)sm);
            var endNode = new NodePoint(ex, ez, edx, edz, (NodeDir)em);
            //var foundRoundResult = RoundRoadTools.FoundRound(startNode, endNode, r * RoundRoadTools.U, cw, (s) => Console.WriteLine(s));
            var calcRoadResult = RoundRoadTools.CalculateRoad(startNode, endNode, r * RoundRoadTools.U, 5 * RoundRoadTools.U, new float[] {/* 3,6,9*/}, (s) => Console.WriteLine(s));
        }
    }
}
