#define _CRT_SECURE_NO_WARNINGS // disable MSVC secure CRT warnings

// Standard includes
#include <string.h>
#include <math.h>
#include <stdio.h>  // sscanf
#include <stdlib.h> // malloc/free
#include <string>
#include <sstream>

// Export macro
#if defined(_WIN32)
#define EXPORT_API __declspec(dllexport) 
#else
#define EXPORT_API __attribute__((visibility("default")))
#endif

// XOR obfuscation helper
void XorString(char* buffer, const char* source, int len, char key) {
	for (int i = 0; i < len; i++) buffer[i] = source[i] ^ key;
	buffer[len] = '\0';
}


// --- SECURITY KEYS ---
// XOR Key for the Invoice (0x5A)
const char INV_KEY = 0x5A;
// XOR Key for the URL (0x99)
const char URL_KEY = 0x99;

// --- INTERNAL HELPERS ---

// Decrypts invoice bytes back to string on the stack
void DecryptInvoiceInternal(const unsigned char* src, int len, char* dest) {
	for (int i = 0; i < len; i++) {
		dest[i] = src[i] ^ INV_KEY;
	}
	dest[len] = '\0'; // Null terminate
}

// --- OBFUSCATED DATABASE ROOT ---
// Replaces: const char* DB_ROOT = "https://arcolorbook-validation-default-rtdb.firebaseio.com/";
// The URL is stored as encrypted bytes (XOR Key: 0x99) and decrypted only when needed.

char _dbRootBuffer[256] = { 0 };

const char* GetDBRoot() {
	// If already decrypted, return it (Optimization)
	if (_dbRootBuffer[0] != 0) return _dbRootBuffer;

	// Encrypted Data: "https://arcolorbook-validation-default-rtdb.firebaseio.com/"
	// XOR Key: 0x99
	const unsigned char encUrl[] = {
		0xF1, 0xED, 0xED, 0xE9, 0xEA, 0xA3, 0xB6, 0xB6, // https://
		0xF8, 0xEB, 0xFA, 0xE6, 0xE5, 0xE6, 0xEB, 0xFB, // arcolorb
		0xE6, 0xE6, 0xE2, 0xB4, 0xEF, 0xF8, 0xE5, 0xE0, // ook-vali
		0xDD, 0xF8, 0xED, 0xE0, 0xE6, 0xE7, 0xB4, 0xDD, // dation-d
		0xFC, 0xFF, 0xF8, 0xEC, 0xE5, 0xED, 0xB4, 0xEB, // efault-r
		0xED, 0xDD, 0xFB, 0xB7, 0xFF, 0xE0, 0xEB, 0xFC, // tdb.fire
		0xFB, 0xF8, 0xEA, 0xFC, 0xE0, 0xE6, 0xB7, 0xFA, // baseio.c
		0xE6, 0xE4, 0xB6, 0x00                           // om/ + \0
	};

	// Decrypt into buffer
	int len = sizeof(encUrl);
	for (int i = 0; i < len; i++) {
		_dbRootBuffer[i] = encUrl[i] ^ 0x99;
	}
	return _dbRootBuffer;
}

// --- INTERNAL HELPERS ---
unsigned long long ExtractKey(const char* json) {
	const char* keyTag = "\"key\":\"";
	const char* found = strstr(json, keyTag);
	if (!found) return 0;
	found += strlen(keyTag);
	unsigned long long val = 0;
	sscanf(found, "%llu", &val);
	return val;
}
unsigned long long ExtractKeyFromJson(const char* json) {
	const char* keyTag = "\"key\":\"";
	const char* found = strstr(json, keyTag);
	if (!found) return 0;
	found += strlen(keyTag);
	unsigned long long extractedVal = 0;
	sscanf(found, "%llu", &extractedVal);
	return extractedVal;
}

unsigned long long HashInvoice(const char* invoice) {
	// Correct Salt Logic (From previous step)
	char salt[20];
	const unsigned char encSalt[] = {
		0xDF, 0xDC, 0xD5, 0xD0, 0xD7, 0xD8, 0xC6, 0xAB, 0xA9, 0xAB, 0xAC,
		0xC6, 0xCA, 0xDC, 0xDA, 0xCC, 0xCB, 0xDC, 0x99
	};
	for (int i = 0; i < 20; i++) salt[i] = encSalt[i] ^ 0x99;

	unsigned long long hash = 5381;
	int c;
	const char* p = invoice;
	while ((c = *p++)) hash = ((hash << 5) + hash) + c;
	p = salt;
	while ((c = *p++)) hash = ((hash << 5) + hash) + c;
	return hash;
}

// --- INTERNAL: HASH GENERATOR (Hidden) ---
unsigned long long GenerateHashInternal(const char* invoice) {
	// 1. SALT: "FELINA_2025_SECURE"
	char salt[20];
	const unsigned char encSalt[] = {
		0xDF, 0xDC, 0xD5, 0xD0, 0xD7, 0xD8, // FELINA
		0xC6,                               // _
		0xAB, 0xA9, 0xAB, 0xAC,             // 2025
		0xC6,                               // _
		0xCA, 0xDC, 0xDA, 0xCC, 0xCB, 0xDC, // SECURE
		0x99                                // \0
	};

	// Decrypt Salt
	for (int i = 0; i < sizeof(encSalt); i++) {
		salt[i] = encSalt[i] ^ 0x99;
	}

	// 2. DJB2 HASH
	unsigned long long hash = 5381;
	int c;

	// Hash Invoice
	const char* p = invoice;
	while ((c = *p++)) hash = ((hash << 5) + hash) + c;

	// Hash Salt
	const char* s = salt;
	while ((c = *s++)) hash = ((hash << 5) + hash) + c;

	return hash;
}

// --- CONSTANTS ---
// 30 Days in Ticks (10,000 ticks per ms * 1000 * 60 * 60 * 24 * 30)
// Hardcoded in binary. Cannot be edited in C#.
const long long CHECK_INTERVAL_TICKS = 25920000000000;

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

	// --- NEW: CONFIGURATION VAULT ---
	// ID 0: Check Interval (Days)
	// ID 1: Pref Key Status
	// ID 2: Pref Key Date
	// ID 3: Pref Key Cache

	EXPORT_API int GetConfigInt(int id) {
		switch (id) {
		case 0: return 30; // CHECK_INTERVAL_DAYS
		default: return 0;
		}
	}

	EXPORT_API void GetConfigString(int id, char* buffer, int maxLen) {
		if (!buffer || maxLen <= 0) return;

		const char* val = "";
		switch (id) {
		case 1: val = "sys_status";     break; // Pref Status
		case 2: val = "sys_check_ts";   break; // Pref Last Check
		case 3: val = "sys_cache";      break; // Pref Cache
		}

		// Safe Copy
#if defined(_WIN32)
		strncpy_s(buffer, maxLen, val, _TRUNCATE);
#else
		strncpy(buffer, val, maxLen);
		buffer[maxLen - 1] = '\0';
#endif
	}

	// --- NEW: Internal Helper for Aspect Ratio (Hidden logic) ---
	static inline void ComputeUVs(Float2* uvs) {
		//float ratio = width / height;
		//float uMin, uMax, vMin, vMax;

		//if (ratio < 1.0f) { // Portrait
		//	float s = 1.0f / ratio;
		//	vMin = (1.0f - s) * 0.5f; vMax = vMin + s;
		//	uMin = 0.0f; uMax = 1.0f;
		//}
		//else { // Landscape
		//	float s = ratio;
		//	uMin = (1.0f - s) * 0.5f; uMax = uMin + s;
		//	vMin = 0.0f; vMax = 1.0f;
		//}

		float uMin, uMax, vMin, vMax;

		uMin = 0.0f; uMax = uMin + 1.0f;
		vMin = 0.0f; vMax = 1.0f;

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

		// 3. Calculate Speeds (avoid sqrt by comparing squared values)
		if (dt <= 1e-5f) dt = 0.016f; // Safety against divide by zero

		// Compare squared speeds: moveSpeed^2 = distSq / (dt*dt)
		const float invDt = 1.0f / dt;
		const float moveSpeedSq = distSq * (invDt * invDt);
		const float maxMoveSpeedSq = maxMoveSpeed * maxMoveSpeed;

		// For rotation, avoid division: angleDeg <= maxRotSpeed * dt
		const float maxAngleAllowed = maxRotSpeed * dt;

		return (moveSpeedSq <= maxMoveSpeedSq) && (angleDeg <= maxAngleAllowed);
	}

	// --- HELPER MATH (Internal) ---
	static inline float Dot(Float3 a, Float3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }

	static inline float DistSq2(Float2 a, Float2 b) {
		const float dx = a.x - b.x; const float dy = a.y - b.y; return dx * dx + dy * dy;
	}

	static inline float DistSq3(Float3 a, Float3 b) {
		const float dx = a.x - b.x; const float dy = a.y - b.y; const float dz = a.z - b.z;
		return dx * dx + dy * dy + dz * dz;
	}

	static inline float Clamp01(float v) { return v < 0.0f ? 0.0f : (v > 1.0f ? 1.0f : v); }

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

	// --- STEP 4: HOMOGRAPHY CALCULATION ---
	// Generic transform matrix computation from image -> screen quad
	EXPORT_API void ComputeTransformMatrix(
		float screenW, float screenH,   // Screen Resolution
		Float2* rawScreenPoints,        // Raw Pixel Coordinates (Not Normalized)
		Float4x4* result                // Output Matrix
	) {
		bool allowed = true;

		if (!allowed)
		{
			// Return Identity Matrix (Stops tracking instantly)
			float* m = (float*)result;
			// Zero out
			memset(m, 0, 16 * sizeof(float));
			// Set Identity (1,1,1,1 on diagonal)
			m[0] = 1; m[5] = 1; m[10] = 1; m[15] = 1;
			return;
		}

		// 1. Calculate UVs internally (Hidden from C#)
		Float2 src[4];
		ComputeUVs(src);

		// 2. Normalize Screen Points internally
		Float2 dst[4];
		float invW = 1.0f / screenW;
		float invH = 1.0f / screenH;

		for (int i = 0; i < 4; i++) {
			dst[i].x = rawScreenPoints[i].x * invW;
			dst[i].y = rawScreenPoints[i].y * invH;
		}

		// 3. CLOSED-FORM HOMOGRAPHY for mapping a unit square to a quadrilateral
		// Remap source UV rectangle to unit square [0,1]^2
		const float uMin = src[0].x; const float vMin = src[0].y;
		const float uMax = src[1].x; const float vMax = src[3].y;
		const float uScale = uMax - uMin;
		const float vScale = vMax - vMin;

		if (uScale == 0.0f || vScale == 0.0f) {
			// Degenerate source -> return identity
			result->c0x = 1.0f; result->c0y = 0.0f; result->c0z = 0.0f; result->c0w = 0.0f;
			result->c1x = 0.0f; result->c1y = 1.0f; result->c1z = 0.0f; result->c1w = 0.0f;
			result->c2x = 0.0f; result->c2y = 0.0f; result->c2z = 1.0f; result->c2w = 0.0f;
			result->c3x = 0.0f; result->c3y = 0.0f; result->c3z = 0.0f; result->c3w = 1.0f;
			return;
		}

		// Destination quad points (corresponding to unit square corners)
		// p0 = dst(0,0), p1 = dst(1,0), p2 = dst(1,1), p3 = dst(0,1)
		float x0 = dst[0].x, y0 = dst[0].y;
		float x1 = dst[1].x, y1 = dst[1].y;
		float x2 = dst[2].x, y2 = dst[2].y;
		float x3 = dst[3].x, y3 = dst[3].y;

		// Compute differences
		float dx1 = x1 - x2;
		float dx2 = x3 - x2;
		float dx3 = x0 - x1 + x2 - x3;
		float dy1 = y1 - y2;
		float dy2 = y3 - y2;
		float dy3 = y0 - y1 + y2 - y3;

		const float EPS = 1e-9f;
		float h20 = 0.0f, h21 = 0.0f;
		float den = dx1 * dy2 - dy1 * dx2;
		if (fabsf(den) < EPS) {
			// Affine case (or degenerate) - build affine homography
			h20 = 0.0f; h21 = 0.0f;
		}
		else {
			h20 = (dx3 * dy2 - dy3 * dx2) / den;
			h21 = (dx1 * dy3 - dy1 * dx3) / den;
		}

		// Compose full homography H mapping unit-square to quad
		float h00 = x1 - x0 + h20 * x1;
		float h01 = x3 - x0 + h21 * x3;
		float h02 = x0;

		float h10 = y1 - y0 + h20 * y1;
		float h11 = y3 - y0 + h21 * y3;
		float h12 = y0;

		float h22 = 1.0f;

		// Compose with transform T that maps src rect -> unit square: H_final = H_unit * T
		const float invU = 1.0f / uScale;
		const float invV = 1.0f / vScale;

		float c00 = h00 * invU;
		float c10 = h10 * invU;
		float c20 = h20 * invU;

		float c01 = h01 * invV;
		float c11 = h11 * invV;
		float c21 = h21 * invV;

		float c02 = -h00 * (uMin * invU) - h01 * (vMin * invV) + h02;
		float c12 = -h10 * (uMin * invU) - h11 * (vMin * invV) + h12;
		float c22 = -h20 * (uMin * invU) - h21 * (vMin * invV) + h22;

		// Pack into column-major Float4x4
		result->c0x = c00; result->c0y = c10; result->c0z = c20; result->c0w = 0.0f;
		result->c1x = c01; result->c1y = c11; result->c1z = c21; result->c1w = 0.0f;
		result->c2x = c02; result->c2y = c12; result->c2z = c22; result->c2w = 0.0f;
		result->c3x = 0.0f; result->c3y = 0.0f; result->c3z = 0.0f; result->c3w = 1.0f;
	}
}

