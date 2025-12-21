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

    // --- UPDATED API: Takes Raw Data, not processed data ---
    EXPORT_API void CalculateHomography(
        float imgW, float imgH,         // Physical Image Size
        float screenW, float screenH,   // Screen Resolution
        Float2* rawScreenPoints,        // Raw Pixel Coordinates (Not Normalized)
        Float4x4* result                // Output Matrix
    ) {
        // 1. Calculate UVs internally (Hidden from C#)
        Float2 src[4];
        ComputeUVs(imgW, imgH, src);

        // 2. Normalize Screen Points internally
        Float2 dst[4];
        float invW = 1.0f / screenW;
        float invH = 1.0f / screenH;

        for (int i = 0; i < 4; i++) {
            dst[i].x = rawScreenPoints[i].x * invW;
            dst[i].y = rawScreenPoints[i].y * invH;
        }

        // 3. Solve Linear System (Standard Gaussian Elimination)
        float M[72];
        for (int i = 0; i < 4; i++) {
            float u = src[i].x; float v = src[i].y;
            float x = dst[i].x; float y = dst[i].y;

            int r1 = (2 * i) * 9;
            M[r1 + 0] = u; M[r1 + 1] = v; M[r1 + 2] = 1.0f;
            M[r1 + 3] = 0; M[r1 + 4] = 0; M[r1 + 5] = 0;
            M[r1 + 6] = -u * x; M[r1 + 7] = -v * x; M[r1 + 8] = x;

            int r2 = (2 * i + 1) * 9;
            M[r2 + 0] = 0; M[r2 + 1] = 0; M[r2 + 2] = 0;
            M[r2 + 3] = u; M[r2 + 4] = v; M[r2 + 5] = 1.0f;
            M[r2 + 6] = -u * y; M[r2 + 7] = -v * y; M[r2 + 8] = y;
        }

        // Gaussian Elimination & Back Sub (Same as before)
        const int n = 8; const int cols = 9;
        const float EPS = 1e-9f;

        for (int k = 0; k < n; ++k) {
            int maxRow = k;
            float maxVal = fabsf(M[k * cols + k]);
            for (int i = k + 1; i < n; ++i) {
                float v = fabsf(M[i * cols + k]);
                if (v > maxVal) { maxVal = v; maxRow = i; }
            }
            if (maxVal < EPS) { // Singularity check
                memset(result, 0, sizeof(Float4x4)); // Return Zero matrix
                return;
            }
            if (maxRow != k) {
                for (int j = k; j < cols; j++) {
                    float tmp = M[k * cols + j]; M[k * cols + j] = M[maxRow * cols + j]; M[maxRow * cols + j] = tmp;
                }
            }
            float pivot = M[k * cols + k];
            float invPivot = 1.0f / pivot;
            for (int j = k; j < cols; ++j) M[k * cols + j] *= invPivot;
            for (int i = k + 1; i < n; ++i) {
                float factor = M[i * cols + k];
                for (int j = k; j < cols; j++) M[i * cols + j] -= factor * M[k * cols + j];
            }
        }

        float x[8];
        for (int i = n - 1; i >= 0; --i) {
            float sum = 0.0f;
            for (int j = i + 1; j < n; ++j) sum += M[i * cols + j] * x[j];
            x[i] = M[i * cols + n] - sum;
        }

        result->c0x = x[0]; result->c0y = x[3]; result->c0z = x[6]; result->c0w = 0.0f;
        result->c1x = x[1]; result->c1y = x[4]; result->c1z = x[7]; result->c1w = 0.0f;
        result->c2x = x[2]; result->c2y = x[5]; result->c2z = 1.0f; result->c2w = 0.0f;
        result->c3x = 0.0f; result->c3y = 0.0f; result->c3z = 0.0f; result->c3w = 1.0f;
    }
}
