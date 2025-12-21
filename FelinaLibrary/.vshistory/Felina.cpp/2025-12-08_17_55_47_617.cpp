#include <math.h>
#include <string.h>

#if defined(_MSC_VER)
#define EXPORT_API __declspec(dllexport)
#else
#define EXPORT_API __attribute__((visibility("default")))
#endif

extern "C" {

    // --- STRUCTS (Matches Unity.Mathematics) ---
    struct Float2 { float x, y; };
    struct Float3 { float x, y, z; };
    struct Float4 { float x, y, z, w; }; // Quaternion
    struct Float4x4 {
        float c0x, c0y, c0z, c0w;
        float c1x, c1y, c1z, c1w;
        float c2x, c2y, c2z, c2w;
        float c3x, c3y, c3z, c3w;
    };

    // --- STEP 1: HELLO WORLD (Keep this) ---
    EXPORT_API int GetDebugNumber() {
        return 777;
    }

    // --- NEW: Internal Helper for Aspect Ratio (Hidden logic) ---
    void ComputeUVs(float width, float height, Float2* uvs) {
        float ratio = width / height;
        float uMin, uMax, vMin, vMax;

        if (ratio < 1.0f) { // Portrait
            float s = 1.0f / ratio;
            vMin = (1.0f - s) * 0.5f; vMax = vMin + s;
            uMin = 0.0f; uMax = 1.0f;
        }
        else { // Landscape
            float s = ratio;
            uMin = (1.0f - s) * 0.5f; uMax = uMin + s;
            vMin = 0.0f; vMax = 1.0f;
        }

        uvs[0] = { uMin, vMin };
        uvs[1] = { uMax, vMin };
        uvs[2] = { uMax, vMax };
        uvs[3] = { uMin, vMax };
    }

    // --- STEP 2: STABILITY LOGIC ---
    EXPORT_API bool CheckStability(
        Float3 curPos, Float4 curRot,
        Float3 lastPos, Float4 lastRot,
        float dt,
        float maxMoveSpeed, float maxRotSpeed
    ) {
        // 1. Calculate Distance (Squared first)
        float dx = curPos.x - lastPos.x;
        float dy = curPos.y - lastPos.y;
        float dz = curPos.z - lastPos.z;
        float distSq = dx * dx + dy * dy + dz * dz;

        // 2. Calculate Angle (Quaternion Dot Product)
        // Dot = q1.x*q2.x + ...
        float dot = (curRot.x * lastRot.x) + (curRot.y * lastRot.y) + (curRot.z * lastRot.z) + (curRot.w * lastRot.w);

        // acos requires value between -1 and 1
        // Angle difference logic: 2 * acos(|dot|)
        float absDot = dot < 0.0f ? -dot : dot;
        if (absDot > 1.0f) absDot = 1.0f;

        // Calculate Angle in Degrees (precompute constant)
        const float RAD_TO_DEG = 57.29577951308232f; // 180/pi
        float angleDeg = 2.0f * acosf(absDot) * RAD_TO_DEG;

        // 3. Calculate Speeds
        if (dt <= 1e-5f) dt = 0.016f; // Safety against divide by zero

        float moveSpeed = sqrtf(distSq) / dt;
        float rotSpeed = angleDeg / dt;

        // 4. Return Result
        return (moveSpeed <= maxMoveSpeed) && (rotSpeed <= maxRotSpeed);
    }

    // --- HELPER MATH (Internal) ---
    inline float Dot(Float3 a, Float3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }

    inline float DistSq2(Float2 a, Float2 b) {
        float dx = a.x - b.x; float dy = a.y - b.y; return dx * dx + dy * dy;
    }

    inline float DistSq3(Float3 a, Float3 b) {
        float dx = a.x - b.x; float dy = a.y - b.y; float dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }

    inline float Clamp01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

    // --- STEP 3: QUALITY SCORING LOGIC ---
    EXPORT_API float CalculateQuality(
        Float3 camPos, Float3 camFwd,
        Float3 imgPos, Float3 imgUp,
        Float2 imgScreenPos,
        float screenWidth, float screenHeight
    ) {
        // 1. Angle Score (Dot Product)
        // Dot(ImageUp, -CamFwd) -> 1.0 if facing directly
        Float3 negFwd = { -camFwd.x, -camFwd.y, -camFwd.z };
        float dotVal = Dot(imgUp, negFwd);
        float angleScore = Clamp01(dotVal);

        // 2. Center Score
        Float2 screenCenter = { screenWidth * 0.5f, screenHeight * 0.5f };

        // Check bounds (Is it behind camera?) - Passed as -1 in C# if invalid
        if (imgScreenPos.x < 0 || imgScreenPos.y < 0) return 0.0f;

        float sqrDistCenter = DistSq2(imgScreenPos, screenCenter);
        float halfH = screenHeight * 0.5f;
        float sqrMaxDist = halfH * halfH;
        float centerScore = Clamp01(1.0f - (sqrDistCenter / sqrMaxDist));

        // 3. Distance Score
        float sqrDistCam = DistSq3(camPos, imgPos);
        float distScore = 1.0f;

        // Logic: Too close (< 0.2m) or too far (> 1.0m)
        // 0.04 = 0.2*0.2, 1.0 = 1.0*1.0
        if (sqrDistCam < 0.04f || sqrDistCam > 1.0f) distScore = 0.5f;

        // Final Weighted Score
        // 60% Angle, 40% Center (Distance is just a penalty multiplier essentially)
        return (angleScore * 0.6f) + (centerScore * 0.4f * distScore);
    }

    EXPORT_API void CalculateHomography(Float2* src, Float2* dst, Float4x4* result) {
        // Optimized solver for fixed 8x9 system using row pointers
        const int n = 8; const int cols = 9;
        float M[72]; // 8*9

        // 1. Build the Linear System (A | b)
        for (int i = 0; i < 4; i++) {
            const float u = src[i].x; const float v = src[i].y;
            const float x = dst[i].x; const float y = dst[i].y;

            const int r1 = (2 * i) * cols;
            M[r1 + 0] = u; M[r1 + 1] = v; M[r1 + 2] = 1.0f;
            M[r1 + 3] = 0.0f; M[r1 + 4] = 0.0f; M[r1 + 5] = 0.0f;
            M[r1 + 6] = -u * x; M[r1 + 7] = -v * x; M[r1 + 8] = x;

            const int r2 = (2 * i + 1) * cols;
            M[r2 + 0] = 0.0f; M[r2 + 1] = 0.0f; M[r2 + 2] = 0.0f;
            M[r2 + 3] = u; M[r2 + 4] = v; M[r2 + 5] = 1.0f;
            M[r2 + 6] = -u * y; M[r2 + 7] = -v * y; M[r2 + 8] = y;
        }

        // Create row pointers to avoid repeated index arithmetic
        float* row[n];
        for (int i = 0; i < n; ++i) row[i] = M + i * cols;

        // 2. Gaussian elimination with partial pivoting and pivot row normalization
        const float EPS = 1e-9f;
        for (int k = 0; k < n; ++k) {
            // Find pivot row
            int maxRow = k;
            float maxVal = fabsf(row[k][k]);
            for (int i = k + 1; i < n; ++i) {
                float v = fabsf(row[i][k]);
                if (v > maxVal) { maxVal = v; maxRow = i; }
            }

            // If pivot too small -> singular or ill-conditioned: return identity homography
            if (maxVal < EPS) {
                // Identity homography
                result->c0x = 1.0f; result->c0y = 0.0f; result->c0z = 0.0f; result->c0w = 0.0f;
                result->c1x = 0.0f; result->c1y = 1.0f; result->c1z = 0.0f; result->c1w = 0.0f;
                result->c2x = 0.0f; result->c2y = 0.0f; result->c2z = 1.0f; result->c2w = 0.0f;
                result->c3x = 0.0f; result->c3y = 0.0f; result->c3z = 0.0f; result->c3w = 1.0f;
                return;
            }

            // Swap pointers instead of row data for speed
            if (maxRow != k) {
                float* tmp = row[k]; row[k] = row[maxRow]; row[maxRow] = tmp;
            }

            float* pivotRow = row[k];
            float pivot = pivotRow[k];
            // Normalize pivot row
            float invPivot = 1.0f / pivot;
            for (int j = k; j < cols; ++j) pivotRow[j] *= invPivot;

            // Eliminate below
            for (int i = k + 1; i < n; ++i) {
                float* ri = row[i];
                float factor = ri[k];
                if (factor == 0.0f) continue;
                // ri[j] -= factor * pivotRow[j] for j=k..cols-1
                for (int j = k; j < cols; ++j) ri[j] -= factor * pivotRow[j];
            }
        }

        // 3. Back substitution (upper triangular)
        float xsol[8];
        for (int i = n - 1; i >= 0; --i) {
            float sum = 0.0f;
            float* ri = row[i];
            for (int j = i + 1; j < n; ++j) sum += ri[j] * xsol[j];
            float rhs = ri[n] - sum;
            float diag = ri[i];
            if (fabsf(diag) < EPS) {
                // Degenerate; fallback to identity
                result->c0x = 1.0f; result->c0y = 0.0f; result->c0z = 0.0f; result->c0w = 0.0f;
                result->c1x = 0.0f; result->c1y = 1.0f; result->c1z = 0.0f; result->c1w = 0.0f;
                result->c2x = 0.0f; result->c2y = 0.0f; result->c2z = 1.0f; result->c2w = 0.0f;
                result->c3x = 0.0f; result->c3y = 0.0f; result->c3z = 0.0f; result->c3w = 1.0f;
                return;
            }
            xsol[i] = rhs / diag;
        }

        // 4. Pack Result (Column-Major Float4x4)
        result->c0x = xsol[0]; result->c0y = xsol[3]; result->c0z = xsol[6]; result->c0w = 0.0f;
        result->c1x = xsol[1]; result->c1y = xsol[4]; result->c1z = xsol[7]; result->c1w = 0.0f;
        result->c2x = xsol[2]; result->c2y = xsol[5]; result->c2z = 1.0f;    result->c2w = 0.0f;
        result->c3x = 0.0f;     result->c3y = 0.0f;     result->c3z = 0.0f;    result->c3w = 1.0f;
    }
}
