using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Felina.ARColoringBook.Runtime
{
    public static class Solver
    {
        private const string LIB_NAME = "InternalSolver";

        [DllImport( LIB_NAME )]
        private static extern void HomographySolver_Solve(
            float[] srcPts, int srcCount,
            float inlierThreshold, int maxIterations, bool refineWithLM,
            int outputSize,
            float[] outH, float[] outHinv
        );

        public static void Solve(
            float2[] srcPts,
            float inlierThreshold,
            int maxIterations,
            bool refineWithLM,
            int outputSize,
            out float[] hMatrix,
            out float[] hInvMatrix )
        {
            int count = srcPts.Length;
            float[] flatSrc = new float[ count * 2 ];
            for ( int i = 0; i < count; i++ )
            {
                flatSrc[ i * 2 ] = srcPts[ i ].x;
                flatSrc[ i * 2 + 1 ] = srcPts[ i ].y;
            }

            float[] hRaw = new float[ 9 ];
            float[] hInvRaw = new float[ 9 ];

            HomographySolver_Solve(
                flatSrc, count,
                inlierThreshold, maxIterations, refineWithLM,
                outputSize,
                hRaw, hInvRaw
            );

            hMatrix = hRaw;
            hInvMatrix = hInvRaw;
        }
    }
}


