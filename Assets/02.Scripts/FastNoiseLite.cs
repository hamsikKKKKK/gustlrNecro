// MIT License
// Copyright(c) 2023 Jordan Peck (jordan.me2@gmail.com)
// https://github.com/Auburn/FastNoiseLite

using System;
using System.Runtime.CompilerServices;

public class FastNoiseLite
{
    public enum NoiseType { OpenSimplex2, OpenSimplex2S, Cellular, Perlin, ValueCubic, Value }
    public enum RotationType3D { None, ImproveXYPlanes, ImproveXZPlanes }
    public enum FractalType { None, FBm, Ridged, PingPong, DomainWarpProgressive, DomainWarpIndependent }
    public enum CellularDistanceFunction { Euclidean, EuclideanSq, Manhattan, Hybrid }
    public enum CellularReturnType { CellValue, Distance, Distance2, Distance2Add, Distance2Sub, Distance2Mul, Distance2Div }
    public enum DomainWarpType { OpenSimplex2, OpenSimplex2Reduced, BasicGrid }

    private int mSeed = 1337;
    private float mFrequency = 0.01f;
    private NoiseType mNoiseType = NoiseType.OpenSimplex2;
    private RotationType3D mRotationType3D = RotationType3D.None;
    private FractalType mFractalType = FractalType.None;
    private int mOctaves = 3;
    private float mLacunarity = 2.0f;
    private float mGain = 0.5f;
    private float mWeightedStrength = 0.0f;
    private float mPingPongStrength = 2.0f;
    private float mFractalBounding = 1 / 1.75f;
    private CellularDistanceFunction mCellularDistanceFunction = CellularDistanceFunction.EuclideanSq;
    private CellularReturnType mCellularReturnType = CellularReturnType.Distance;
    private float mCellularJitterModifier = 1.0f;
    private DomainWarpType mDomainWarpType = DomainWarpType.OpenSimplex2;
    private float mDomainWarpAmp = 1.0f;

    public FastNoiseLite(int seed = 1337)
    {
        SetSeed(seed);
    }

    public void SetSeed(int seed) { mSeed = seed; }
    public void SetFrequency(float frequency) { mFrequency = frequency; }
    public void SetNoiseType(NoiseType noiseType) { mNoiseType = noiseType; }
    public void SetRotationType3D(RotationType3D rotationType3D) { mRotationType3D = rotationType3D; }

    public void SetFractalType(FractalType fractalType) { mFractalType = fractalType; }
    public void SetFractalOctaves(int octaves)
    {
        mOctaves = octaves;
        CalculateFractalBounding();
    }
    public void SetFractalLacunarity(float lacunarity) { mLacunarity = lacunarity; }
    public void SetFractalGain(float gain)
    {
        mGain = gain;
        CalculateFractalBounding();
    }
    public void SetFractalWeightedStrength(float weightedStrength) { mWeightedStrength = weightedStrength; }
    public void SetFractalPingPongStrength(float pingPongStrength) { mPingPongStrength = pingPongStrength; }

    public void SetCellularDistanceFunction(CellularDistanceFunction cellularDistanceFunction) { mCellularDistanceFunction = cellularDistanceFunction; }
    public void SetCellularReturnType(CellularReturnType cellularReturnType) { mCellularReturnType = cellularReturnType; }
    public void SetCellularJitter(float cellularJitter) { mCellularJitterModifier = cellularJitter; }

    public void SetDomainWarpType(DomainWarpType domainWarpType) { mDomainWarpType = domainWarpType; }
    public void SetDomainWarpAmp(float domainWarpAmp) { mDomainWarpAmp = domainWarpAmp; }

    public float GetNoise(float x, float y)
    {
        x *= mFrequency;
        y *= mFrequency;

        switch (mFractalType)
        {
            case FractalType.FBm:
                return GenFractalFBm(x, y);
            case FractalType.Ridged:
                return GenFractalRidged(x, y);
            case FractalType.PingPong:
                return GenFractalPingPong(x, y);
            default:
                return GenNoiseSingle(mSeed, x, y);
        }
    }

    public float GetNoise(float x, float y, float z)
    {
        x *= mFrequency;
        y *= mFrequency;
        z *= mFrequency;

        switch (mFractalType)
        {
            case FractalType.FBm:
                return GenFractalFBm(x, y, z);
            case FractalType.Ridged:
                return GenFractalRidged(x, y, z);
            case FractalType.PingPong:
                return GenFractalPingPong(x, y, z);
            default:
                return GenNoiseSingle(mSeed, x, y, z);
        }
    }

    private void CalculateFractalBounding()
    {
        float gain = Math.Abs(mGain);
        float amp = gain;
        float ampFractal = 1.0f;
        for (int i = 1; i < mOctaves; i++)
        {
            ampFractal += amp;
            amp *= gain;
        }
        mFractalBounding = 1 / ampFractal;
    }

    // Hashing
    private const int PrimeX = 501125321;
    private const int PrimeY = 1136930381;
    private const int PrimeZ = 1720413743;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Hash(int seed, int xPrimed, int yPrimed)
    {
        int hash = seed ^ xPrimed ^ yPrimed;
        hash *= 0x27d4eb2d;
        return hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Hash(int seed, int xPrimed, int yPrimed, int zPrimed)
    {
        int hash = seed ^ xPrimed ^ yPrimed ^ zPrimed;
        hash *= 0x27d4eb2d;
        return hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ValCoord(int seed, int xPrimed, int yPrimed)
    {
        int hash = Hash(seed, xPrimed, yPrimed);
        hash *= hash;
        hash ^= hash << 19;
        return hash * (1 / 2147483648.0f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ValCoord(int seed, int xPrimed, int yPrimed, int zPrimed)
    {
        int hash = Hash(seed, xPrimed, yPrimed, zPrimed);
        hash *= hash;
        hash ^= hash << 19;
        return hash * (1 / 2147483648.0f);
    }

    // Gradients
    private static readonly float[] Gradients2D = {
        0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f, 0.793353340291235f,
        0.793353340291235f, 0.608761429008721f, 0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f,
        0.99144486137381f, -0.130526192220051f, 0.923879532511287f, -0.38268343236509f, 0.793353340291235f, -0.60876142900872f,
        0.608761429008721f, -0.793353340291235f, 0.38268343236509f, -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
        -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f, -0.793353340291235f,
        -0.793353340291235f, -0.608761429008721f, -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f,
        -0.99144486137381f, 0.130526192220051f, -0.923879532511287f, 0.38268343236509f, -0.793353340291235f, 0.608761429008721f,
        -0.608761429008721f, 0.793353340291235f, -0.38268343236509f, 0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
        0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f, 0.793353340291235f,
        0.793353340291235f, 0.608761429008721f, 0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f,
        0.99144486137381f, -0.130526192220051f, 0.923879532511287f, -0.38268343236509f, 0.793353340291235f, -0.60876142900872f,
        0.608761429008721f, -0.793353340291235f, 0.38268343236509f, -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
        -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f, -0.793353340291235f,
        -0.793353340291235f, -0.608761429008721f, -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f,
        -0.99144486137381f, 0.130526192220051f, -0.923879532511287f, 0.38268343236509f, -0.793353340291235f, 0.608761429008721f,
        -0.608761429008721f, 0.793353340291235f, -0.38268343236509f, 0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
        0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f, 0.793353340291235f,
        0.793353340291235f, 0.608761429008721f, 0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f,
        0.99144486137381f, -0.130526192220051f, 0.923879532511287f, -0.38268343236509f, 0.793353340291235f, -0.60876142900872f,
        0.608761429008721f, -0.793353340291235f, 0.38268343236509f, -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
        -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f, -0.793353340291235f,
        -0.793353340291235f, -0.608761429008721f, -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f,
        -0.99144486137381f, 0.130526192220051f, -0.923879532511287f, 0.38268343236509f, -0.793353340291235f, 0.608761429008721f,
        -0.608761429008721f, 0.793353340291235f, -0.38268343236509f, 0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
        0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f, 0.793353340291235f,
        0.793353340291235f, 0.608761429008721f, 0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f,
        0.99144486137381f, -0.130526192220051f, 0.923879532511287f, -0.38268343236509f, 0.793353340291235f, -0.60876142900872f,
        0.608761429008721f, -0.793353340291235f, 0.38268343236509f, -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
        -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f, -0.793353340291235f,
        -0.793353340291235f, -0.608761429008721f, -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f,
        -0.99144486137381f, 0.130526192220051f, -0.923879532511287f, 0.38268343236509f, -0.793353340291235f, 0.608761429008721f,
        -0.608761429008721f, 0.793353340291235f, -0.38268343236509f, 0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
        0.38268343236509f, 0.923879532511287f, 0.923879532511287f, 0.38268343236509f, 0.923879532511287f, -0.38268343236509f,
        0.38268343236509f, -0.923879532511287f, -0.38268343236509f, -0.923879532511287f, -0.923879532511287f, -0.38268343236509f,
        -0.923879532511287f, 0.38268343236509f, -0.38268343236509f, 0.923879532511287f
    };

    private static readonly float[] Gradients3D = {
        0, 1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0,
        1, 0, 1, 0, -1, 0, 1, 0, 1, 0, -1, 0, -1, 0, -1, 0,
        1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0, 0,
        0, 1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0,
        1, 0, 1, 0, -1, 0, 1, 0, 1, 0, -1, 0, -1, 0, -1, 0,
        1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0, 0,
        0, 1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0,
        1, 0, 1, 0, -1, 0, 1, 0, 1, 0, -1, 0, -1, 0, -1, 0,
        1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0, 0,
        0, 1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0,
        1, 0, 1, 0, -1, 0, 1, 0, 1, 0, -1, 0, -1, 0, -1, 0,
        1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0, 0,
        0, 1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0,
        1, 0, 1, 0, -1, 0, 1, 0, 1, 0, -1, 0, -1, 0, -1, 0,
        1, 1, 0, 0, -1, 1, 0, 0, 1, -1, 0, 0, -1, -1, 0, 0,
        1, 1, 0, 0, 0, -1, 1, 0, -1, 1, 0, 0, 0, -1, -1, 0
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GradCoord(int seed, int xPrimed, int yPrimed, float xd, float yd)
    {
        int hash = Hash(seed, xPrimed, yPrimed);
        hash ^= hash >> 15;
        hash &= 0xFE; // 254
        hash %= (Gradients2D.Length - 1);

        float xg = Gradients2D[hash];
        float yg = Gradients2D[hash | 1];

        return xd * xg + yd * yg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GradCoord(int seed, int xPrimed, int yPrimed, int zPrimed, float xd, float yd, float zd)
    {
        int hash = Hash(seed, xPrimed, yPrimed, zPrimed);
        hash ^= hash >> 15;
        hash &= 63 << 2;

        float xg = Gradients3D[hash];
        float yg = Gradients3D[hash | 1];
        float zg = Gradients3D[hash | 2];

        return xd * xg + yd * yg + zd * zg;
    }

    // Noise generation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FastFloor(float f) { return f >= 0 ? (int)f : (int)f - 1; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float InterpHermite(float t) { return t * t * (3 - 2 * t); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float InterpQuintic(float t) { return t * t * t * (t * (t * 6 - 15) + 10); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) { return a + t * (b - a); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CubicLerp(float a, float b, float c, float d, float t)
    {
        float p = (d - c) - (a - b);
        return t * t * t * p + t * t * ((a - b) - p) + t * (c - a) + b;
    }

    private float GenNoiseSingle(int seed, float x, float y)
    {
        switch (mNoiseType)
        {
            case NoiseType.OpenSimplex2:
                return SingleSimplex(seed, x, y);
            case NoiseType.OpenSimplex2S:
                return SingleOpenSimplex2S(seed, x, y);
            case NoiseType.Cellular:
                return SingleCellular(seed, x, y);
            case NoiseType.Perlin:
                return SinglePerlin(seed, x, y);
            case NoiseType.ValueCubic:
                return SingleValueCubic(seed, x, y);
            case NoiseType.Value:
                return SingleValue(seed, x, y);
            default:
                return 0;
        }
    }

    private float GenNoiseSingle(int seed, float x, float y, float z)
    {
        switch (mNoiseType)
        {
            case NoiseType.OpenSimplex2:
                return SingleOpenSimplex2(seed, x, y, z);
            case NoiseType.OpenSimplex2S:
                return SingleOpenSimplex2S(seed, x, y, z);
            case NoiseType.Cellular:
                return SingleCellular(seed, x, y, z);
            case NoiseType.Perlin:
                return SinglePerlin(seed, x, y, z);
            case NoiseType.ValueCubic:
                return SingleValueCubic(seed, x, y, z);
            case NoiseType.Value:
                return SingleValue(seed, x, y, z);
            default:
                return 0;
        }
    }

    // Fractal
    private float GenFractalFBm(float x, float y)
    {
        int seed = mSeed;
        float sum = 0;
        float amp = mFractalBounding;

        for (int i = 0; i < mOctaves; i++)
        {
            float noise = GenNoiseSingle(seed++, x, y);
            sum += noise * amp;
            amp *= Lerp(1.0f, Math.Min(noise + 1, 2) * 0.5f, mWeightedStrength);

            x *= mLacunarity;
            y *= mLacunarity;
            amp *= mGain;
        }

        return sum;
    }

    private float GenFractalFBm(float x, float y, float z)
    {
        int seed = mSeed;
        float sum = 0;
        float amp = mFractalBounding;

        for (int i = 0; i < mOctaves; i++)
        {
            float noise = GenNoiseSingle(seed++, x, y, z);
            sum += noise * amp;
            amp *= Lerp(1.0f, (noise + 1) * 0.5f, mWeightedStrength);

            x *= mLacunarity;
            y *= mLacunarity;
            z *= mLacunarity;
            amp *= mGain;
        }

        return sum;
    }

    private float GenFractalRidged(float x, float y)
    {
        int seed = mSeed;
        float sum = 0;
        float amp = mFractalBounding;

        for (int i = 0; i < mOctaves; i++)
        {
            float noise = Math.Abs(GenNoiseSingle(seed++, x, y));
            sum += (noise * -2 + 1) * amp;
            amp *= Lerp(1.0f, 1 - noise, mWeightedStrength);

            x *= mLacunarity;
            y *= mLacunarity;
            amp *= mGain;
        }

        return sum;
    }

    private float GenFractalRidged(float x, float y, float z)
    {
        int seed = mSeed;
        float sum = 0;
        float amp = mFractalBounding;

        for (int i = 0; i < mOctaves; i++)
        {
            float noise = Math.Abs(GenNoiseSingle(seed++, x, y, z));
            sum += (noise * -2 + 1) * amp;
            amp *= Lerp(1.0f, 1 - noise, mWeightedStrength);

            x *= mLacunarity;
            y *= mLacunarity;
            z *= mLacunarity;
            amp *= mGain;
        }

        return sum;
    }

    private float GenFractalPingPong(float x, float y)
    {
        int seed = mSeed;
        float sum = 0;
        float amp = mFractalBounding;

        for (int i = 0; i < mOctaves; i++)
        {
            float noise = PingPong((GenNoiseSingle(seed++, x, y) + 1) * mPingPongStrength);
            sum += (noise - 0.5f) * 2 * amp;
            amp *= Lerp(1.0f, noise, mWeightedStrength);

            x *= mLacunarity;
            y *= mLacunarity;
            amp *= mGain;
        }

        return sum;
    }

    private float GenFractalPingPong(float x, float y, float z)
    {
        int seed = mSeed;
        float sum = 0;
        float amp = mFractalBounding;

        for (int i = 0; i < mOctaves; i++)
        {
            float noise = PingPong((GenNoiseSingle(seed++, x, y, z) + 1) * mPingPongStrength);
            sum += (noise - 0.5f) * 2 * amp;
            amp *= Lerp(1.0f, noise, mWeightedStrength);

            x *= mLacunarity;
            y *= mLacunarity;
            z *= mLacunarity;
            amp *= mGain;
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PingPong(float t)
    {
        t -= (int)(t * 0.5f) * 2;
        return t < 1 ? t : 2 - t;
    }

    // Simplex Noise 2D
    private float SingleSimplex(int seed, float x, float y)
    {
        const float SQRT3 = 1.7320508075688772935274463415059f;
        const float F2 = 0.5f * (SQRT3 - 1);
        const float G2 = (3 - SQRT3) / 6;

        float t = (x + y) * F2;
        int i = FastFloor(x + t);
        int j = FastFloor(y + t);

        t = (i + j) * G2;
        float X0 = i - t;
        float Y0 = j - t;

        float x0 = x - X0;
        float y0 = y - Y0;

        int i1, j1;
        if (x0 > y0)
        {
            i1 = 1;
            j1 = 0;
        }
        else
        {
            i1 = 0;
            j1 = 1;
        }

        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1 + 2 * G2;
        float y2 = y0 - 1 + 2 * G2;

        float n0, n1, n2;

        t = 0.5f - x0 * x0 - y0 * y0;
        if (t < 0)
            n0 = 0;
        else
        {
            t *= t;
            n0 = t * t * GradCoord(seed, i * PrimeX, j * PrimeY, x0, y0);
        }

        t = 0.5f - x1 * x1 - y1 * y1;
        if (t < 0)
            n1 = 0;
        else
        {
            t *= t;
            n1 = t * t * GradCoord(seed, (i + i1) * PrimeX, (j + j1) * PrimeY, x1, y1);
        }

        t = 0.5f - x2 * x2 - y2 * y2;
        if (t < 0)
            n2 = 0;
        else
        {
            t *= t;
            n2 = t * t * GradCoord(seed, (i + 1) * PrimeX, (j + 1) * PrimeY, x2, y2);
        }

        return (n0 + n1 + n2) * 99.83685446303647f;
    }

    // OpenSimplex2S 2D
    private float SingleOpenSimplex2S(int seed, float x, float y)
    {
        const float SQRT3 = 1.7320508075688772935274463415059f;
        const float F2 = 0.5f * (SQRT3 - 1);
        const float G2 = (3 - SQRT3) / 6;

        float s = (x + y) * F2;
        x += s;
        y += s;

        int i = FastFloor(x);
        int j = FastFloor(y);
        float xi = x - i;
        float yi = y - j;

        float t = (xi + yi) * G2;
        float x0 = xi - t;
        float y0 = yi - t;

        i *= PrimeX;
        j *= PrimeY;

        float n0, n1, n2;

        float a = 0.5f - x0 * x0 - y0 * y0;
        if (a <= 0)
            n0 = 0;
        else
            n0 = (a * a) * (a * a) * GradCoord(seed, i, j, x0, y0);

        float c = 2 * (1 - 2 * G2) * (1 / G2 - 2) * t + ((-2 * (1 - 2 * G2) * (1 - 2 * G2)) + a);
        if (c <= 0)
            n2 = 0;
        else
        {
            float x2 = x0 + (2 * G2 - 1);
            float y2 = y0 + (2 * G2 - 1);
            n2 = (c * c) * (c * c) * GradCoord(seed, i + PrimeX, j + PrimeY, x2, y2);
        }

        if (y0 > x0)
        {
            float x1 = x0 + G2;
            float y1 = y0 + (G2 - 1);
            float b = 0.5f - x1 * x1 - y1 * y1;
            if (b <= 0)
                n1 = 0;
            else
                n1 = (b * b) * (b * b) * GradCoord(seed, i, j + PrimeY, x1, y1);
        }
        else
        {
            float x1 = x0 + (G2 - 1);
            float y1 = y0 + G2;
            float b = 0.5f - x1 * x1 - y1 * y1;
            if (b <= 0)
                n1 = 0;
            else
                n1 = (b * b) * (b * b) * GradCoord(seed, i + PrimeX, j, x1, y1);
        }

        return (n0 + n1 + n2) * 45.23065f;
    }

    // OpenSimplex2 3D
    private float SingleOpenSimplex2(int seed, float x, float y, float z)
    {
        int i = FastFloor(x);
        int j = FastFloor(y);
        int k = FastFloor(z);
        float xi = x - i;
        float yi = y - j;
        float zi = z - k;

        i *= PrimeX;
        j *= PrimeY;
        k *= PrimeZ;

        int seed2 = seed + 1293373;

        int xNMask = (int)(-0.5f - xi);
        int yNMask = (int)(-0.5f - yi);
        int zNMask = (int)(-0.5f - zi);

        float x0 = xi + xNMask;
        float y0 = yi + yNMask;
        float z0 = zi + zNMask;
        float a0 = 0.75f - x0 * x0 - y0 * y0 - z0 * z0;
        float value = (a0 * a0) * (a0 * a0) * GradCoord(seed,
            i + (xNMask & PrimeX), j + (yNMask & PrimeY), k + (zNMask & PrimeZ), x0, y0, z0);

        float x1 = xi - 0.5f;
        float y1 = yi - 0.5f;
        float z1 = zi - 0.5f;
        float a1 = 0.75f - x1 * x1 - y1 * y1 - z1 * z1;
        value += (a1 * a1) * (a1 * a1) * GradCoord(seed2,
            i + PrimeX, j + PrimeY, k + PrimeZ, x1, y1, z1);

        float xAFlipMask0 = ((xNMask | 1) << 1) * x1;
        float yAFlipMask0 = ((yNMask | 1) << 1) * y1;
        float zAFlipMask0 = ((zNMask | 1) << 1) * z1;
        float xAFlipMask1 = (-2 - (xNMask << 2)) * x1 - 1.0f;
        float yAFlipMask1 = (-2 - (yNMask << 2)) * y1 - 1.0f;
        float zAFlipMask1 = (-2 - (zNMask << 2)) * z1 - 1.0f;

        bool skip5 = false;
        float a2 = xAFlipMask0 + a0;
        if (a2 > 0)
        {
            float x2 = x0 - (xNMask | 1);
            value += (a2 * a2) * (a2 * a2) * GradCoord(seed,
                i + (~xNMask & PrimeX), j + (yNMask & PrimeY), k + (zNMask & PrimeZ), x2, y0, z0);
        }
        else
        {
            float a3 = yAFlipMask0 + zAFlipMask0 + a0;
            if (a3 > 0)
            {
                float y3 = y0 - (yNMask | 1);
                float z3 = z0 - (zNMask | 1);
                value += (a3 * a3) * (a3 * a3) * GradCoord(seed,
                    i + (xNMask & PrimeX), j + (~yNMask & PrimeY), k + (~zNMask & PrimeZ), x0, y3, z3);
            }

            float a4 = xAFlipMask1 + a1;
            if (a4 > 0)
            {
                float x4 = (xNMask | 1) + x1;
                value += (a4 * a4) * (a4 * a4) * GradCoord(seed2,
                    i + (xNMask & (PrimeX * 2)), j + PrimeY, k + PrimeZ, x4, y1, z1);
                skip5 = true;
            }
        }

        bool skip9 = false;
        float a6 = yAFlipMask0 + a0;
        if (a6 > 0)
        {
            float y6 = y0 - (yNMask | 1);
            value += (a6 * a6) * (a6 * a6) * GradCoord(seed,
                i + (xNMask & PrimeX), j + (~yNMask & PrimeY), k + (zNMask & PrimeZ), x0, y6, z0);
        }
        else
        {
            float a7 = xAFlipMask0 + zAFlipMask0 + a0;
            if (a7 > 0)
            {
                float x7 = x0 - (xNMask | 1);
                float z7 = z0 - (zNMask | 1);
                value += (a7 * a7) * (a7 * a7) * GradCoord(seed,
                    i + (~xNMask & PrimeX), j + (yNMask & PrimeY), k + (~zNMask & PrimeZ), x7, y0, z7);
            }

            float a8 = yAFlipMask1 + a1;
            if (a8 > 0)
            {
                float y8 = (yNMask | 1) + y1;
                value += (a8 * a8) * (a8 * a8) * GradCoord(seed2,
                    i + PrimeX, j + (yNMask & (PrimeY << 1)), k + PrimeZ, x1, y8, z1);
                skip9 = true;
            }
        }

        bool skipD = false;
        float aA = zAFlipMask0 + a0;
        if (aA > 0)
        {
            float zA = z0 - (zNMask | 1);
            value += (aA * aA) * (aA * aA) * GradCoord(seed,
                i + (xNMask & PrimeX), j + (yNMask & PrimeY), k + (~zNMask & PrimeZ), x0, y0, zA);
        }
        else
        {
            float aB = xAFlipMask0 + yAFlipMask0 + a0;
            if (aB > 0)
            {
                float xB = x0 - (xNMask | 1);
                float yB = y0 - (yNMask | 1);
                value += (aB * aB) * (aB * aB) * GradCoord(seed,
                    i + (~xNMask & PrimeX), j + (~yNMask & PrimeY), k + (zNMask & PrimeZ), xB, yB, z0);
            }

            float aC = zAFlipMask1 + a1;
            if (aC > 0)
            {
                float zC = (zNMask | 1) + z1;
                value += (aC * aC) * (aC * aC) * GradCoord(seed2,
                    i + PrimeX, j + PrimeY, k + (zNMask & (PrimeZ << 1)), x1, y1, zC);
                skipD = true;
            }
        }

        if (!skip5)
        {
            float a5 = yAFlipMask1 + zAFlipMask1 + a1;
            if (a5 > 0)
            {
                float y5 = (yNMask | 1) + y1;
                float z5 = (zNMask | 1) + z1;
                value += (a5 * a5) * (a5 * a5) * GradCoord(seed2,
                    i + PrimeX, j + (yNMask & (PrimeY << 1)), k + (zNMask & (PrimeZ << 1)), x1, y5, z5);
            }
        }

        if (!skip9)
        {
            float a9 = xAFlipMask1 + zAFlipMask1 + a1;
            if (a9 > 0)
            {
                float x9 = (xNMask | 1) + x1;
                float z9 = (zNMask | 1) + z1;
                value += (a9 * a9) * (a9 * a9) * GradCoord(seed2,
                    i + (xNMask & (PrimeX * 2)), j + PrimeY, k + (zNMask & (PrimeZ << 1)), x9, y1, z9);
            }
        }

        if (!skipD)
        {
            float aD = xAFlipMask1 + yAFlipMask1 + a1;
            if (aD > 0)
            {
                float xD = (xNMask | 1) + x1;
                float yD = (yNMask | 1) + y1;
                value += (aD * aD) * (aD * aD) * GradCoord(seed2,
                    i + (xNMask & (PrimeX << 1)), j + (yNMask & (PrimeY << 1)), k + PrimeZ, xD, yD, z1);
            }
        }

        return value * 9.046026385208288f;
    }

    // OpenSimplex2S 3D
    private float SingleOpenSimplex2S(int seed, float x, float y, float z)
    {
        int i = FastFloor(x);
        int j = FastFloor(y);
        int k = FastFloor(z);
        float xi = x - i;
        float yi = y - j;
        float zi = z - k;

        i *= PrimeX;
        j *= PrimeY;
        k *= PrimeZ;

        int seed2 = seed + 1293373;

        float value = 0;

        float aB = 0.6f - xi * xi - (yi - 1) * (yi - 1) - zi * zi;
        if (aB > 0) value = (aB * aB) * (aB * aB) * GradCoord(seed, i, j + PrimeY, k, xi, yi - 1, zi);
        float aC = 0.6f - xi * xi - yi * yi - (zi - 1) * (zi - 1);
        if (aC > 0) value += (aC * aC) * (aC * aC) * GradCoord(seed, i, j, k + PrimeZ, xi, yi, zi - 1);
        float aD = 0.6f - (xi - 1) * (xi - 1) - yi * yi - zi * zi;
        if (aD > 0) value += (aD * aD) * (aD * aD) * GradCoord(seed, i + PrimeX, j, k, xi - 1, yi, zi);

        float aA = 0.6f - xi * xi - yi * yi - zi * zi;
        if (aA > 0) value += (aA * aA) * (aA * aA) * GradCoord(seed2, i, j, k, xi, yi, zi);
        float aE = 0.6f - (xi - 1) * (xi - 1) - (yi - 1) * (yi - 1) - zi * zi;
        if (aE > 0) value += (aE * aE) * (aE * aE) * GradCoord(seed2, i + PrimeX, j + PrimeY, k, xi - 1, yi - 1, zi);
        float aF = 0.6f - (xi - 1) * (xi - 1) - yi * yi - (zi - 1) * (zi - 1);
        if (aF > 0) value += (aF * aF) * (aF * aF) * GradCoord(seed2, i + PrimeX, j, k + PrimeZ, xi - 1, yi, zi - 1);
        float aG = 0.6f - xi * xi - (yi - 1) * (yi - 1) - (zi - 1) * (zi - 1);
        if (aG > 0) value += (aG * aG) * (aG * aG) * GradCoord(seed2, i, j + PrimeY, k + PrimeZ, xi, yi - 1, zi - 1);
        float aH = 0.6f - (xi - 1) * (xi - 1) - (yi - 1) * (yi - 1) - (zi - 1) * (zi - 1);
        if (aH > 0) value += (aH * aH) * (aH * aH) * GradCoord(seed, i + PrimeX, j + PrimeY, k + PrimeZ, xi - 1, yi - 1, zi - 1);

        return value * 32.69428253173828125f;
    }

    // Cellular Noise 2D
    private float SingleCellular(int seed, float x, float y)
    {
        int xr = FastFloor(x) - 1;
        int yr = FastFloor(y) - 1;

        float distance0 = float.MaxValue;
        float distance1 = float.MaxValue;
        int closestHash = 0;

        float cellularJitter = 0.43701595f * mCellularJitterModifier;

        for (int xi = xr; xi <= xr + 2; xi++)
        {
            for (int yi = yr; yi <= yr + 2; yi++)
            {
                int hash = Hash(seed, xi * PrimeX, yi * PrimeY);
                int idx = hash & (255 << 1);

                float vecX = (float)(xi - x) + Gradients2D[idx] * cellularJitter;
                float vecY = (float)(yi - y) + Gradients2D[idx | 1] * cellularJitter;

                float newDistance = 0;
                switch (mCellularDistanceFunction)
                {
                    case CellularDistanceFunction.Euclidean:
                        newDistance = (float)Math.Sqrt(vecX * vecX + vecY * vecY);
                        break;
                    case CellularDistanceFunction.EuclideanSq:
                        newDistance = vecX * vecX + vecY * vecY;
                        break;
                    case CellularDistanceFunction.Manhattan:
                        newDistance = Math.Abs(vecX) + Math.Abs(vecY);
                        break;
                    case CellularDistanceFunction.Hybrid:
                        newDistance = Math.Abs(vecX) + Math.Abs(vecY) + (vecX * vecX + vecY * vecY);
                        break;
                }

                if (newDistance < distance0)
                {
                    distance1 = distance0;
                    distance0 = newDistance;
                    closestHash = hash;
                }
                else if (newDistance < distance1)
                {
                    distance1 = newDistance;
                }
            }
        }

        switch (mCellularReturnType)
        {
            case CellularReturnType.CellValue:
                return closestHash * (1 / 2147483648.0f);
            case CellularReturnType.Distance:
                return distance0 - 1;
            case CellularReturnType.Distance2:
                return distance1 - 1;
            case CellularReturnType.Distance2Add:
                return (distance1 + distance0) * 0.5f - 1;
            case CellularReturnType.Distance2Sub:
                return distance1 - distance0 - 1;
            case CellularReturnType.Distance2Mul:
                return distance1 * distance0 * 0.5f - 1;
            case CellularReturnType.Distance2Div:
                return distance0 / distance1 - 1;
            default:
                return 0;
        }
    }

    // Cellular Noise 3D
    private float SingleCellular(int seed, float x, float y, float z)
    {
        int xr = FastFloor(x) - 1;
        int yr = FastFloor(y) - 1;
        int zr = FastFloor(z) - 1;

        float distance0 = float.MaxValue;
        float distance1 = float.MaxValue;
        int closestHash = 0;

        float cellularJitter = 0.39614353f * mCellularJitterModifier;

        for (int xi = xr; xi <= xr + 2; xi++)
        {
            for (int yi = yr; yi <= yr + 2; yi++)
            {
                for (int zi = zr; zi <= zr + 2; zi++)
                {
                    int hash = Hash(seed, xi * PrimeX, yi * PrimeY, zi * PrimeZ);
                    int idx = hash & (255 << 2);

                    float vecX = (float)(xi - x) + Gradients3D[idx] * cellularJitter;
                    float vecY = (float)(yi - y) + Gradients3D[idx | 1] * cellularJitter;
                    float vecZ = (float)(zi - z) + Gradients3D[idx | 2] * cellularJitter;

                    float newDistance = 0;
                    switch (mCellularDistanceFunction)
                    {
                        case CellularDistanceFunction.Euclidean:
                            newDistance = (float)Math.Sqrt(vecX * vecX + vecY * vecY + vecZ * vecZ);
                            break;
                        case CellularDistanceFunction.EuclideanSq:
                            newDistance = vecX * vecX + vecY * vecY + vecZ * vecZ;
                            break;
                        case CellularDistanceFunction.Manhattan:
                            newDistance = Math.Abs(vecX) + Math.Abs(vecY) + Math.Abs(vecZ);
                            break;
                        case CellularDistanceFunction.Hybrid:
                            newDistance = Math.Abs(vecX) + Math.Abs(vecY) + Math.Abs(vecZ) + (vecX * vecX + vecY * vecY + vecZ * vecZ);
                            break;
                    }

                    if (newDistance < distance0)
                    {
                        distance1 = distance0;
                        distance0 = newDistance;
                        closestHash = hash;
                    }
                    else if (newDistance < distance1)
                    {
                        distance1 = newDistance;
                    }
                }
            }
        }

        switch (mCellularReturnType)
        {
            case CellularReturnType.CellValue:
                return closestHash * (1 / 2147483648.0f);
            case CellularReturnType.Distance:
                return distance0 - 1;
            case CellularReturnType.Distance2:
                return distance1 - 1;
            case CellularReturnType.Distance2Add:
                return (distance1 + distance0) * 0.5f - 1;
            case CellularReturnType.Distance2Sub:
                return distance1 - distance0 - 1;
            case CellularReturnType.Distance2Mul:
                return distance1 * distance0 * 0.5f - 1;
            case CellularReturnType.Distance2Div:
                return distance0 / distance1 - 1;
            default:
                return 0;
        }
    }

    // Perlin Noise 2D
    private float SinglePerlin(int seed, float x, float y)
    {
        int x0 = FastFloor(x);
        int y0 = FastFloor(y);

        float xd0 = x - x0;
        float yd0 = y - y0;
        float xd1 = xd0 - 1;
        float yd1 = yd0 - 1;

        float xs = InterpQuintic(xd0);
        float ys = InterpQuintic(yd0);

        x0 *= PrimeX;
        y0 *= PrimeY;
        int x1 = x0 + PrimeX;
        int y1 = y0 + PrimeY;

        float xf0 = Lerp(GradCoord(seed, x0, y0, xd0, yd0), GradCoord(seed, x1, y0, xd1, yd0), xs);
        float xf1 = Lerp(GradCoord(seed, x0, y1, xd0, yd1), GradCoord(seed, x1, y1, xd1, yd1), xs);

        return Lerp(xf0, xf1, ys) * 1.4247691104677813f;
    }

    // Perlin Noise 3D
    private float SinglePerlin(int seed, float x, float y, float z)
    {
        int x0 = FastFloor(x);
        int y0 = FastFloor(y);
        int z0 = FastFloor(z);

        float xd0 = x - x0;
        float yd0 = y - y0;
        float zd0 = z - z0;
        float xd1 = xd0 - 1;
        float yd1 = yd0 - 1;
        float zd1 = zd0 - 1;

        float xs = InterpQuintic(xd0);
        float ys = InterpQuintic(yd0);
        float zs = InterpQuintic(zd0);

        x0 *= PrimeX;
        y0 *= PrimeY;
        z0 *= PrimeZ;
        int x1 = x0 + PrimeX;
        int y1 = y0 + PrimeY;
        int z1 = z0 + PrimeZ;

        float xf00 = Lerp(GradCoord(seed, x0, y0, z0, xd0, yd0, zd0), GradCoord(seed, x1, y0, z0, xd1, yd0, zd0), xs);
        float xf10 = Lerp(GradCoord(seed, x0, y1, z0, xd0, yd1, zd0), GradCoord(seed, x1, y1, z0, xd1, yd1, zd0), xs);
        float xf01 = Lerp(GradCoord(seed, x0, y0, z1, xd0, yd0, zd1), GradCoord(seed, x1, y0, z1, xd1, yd0, zd1), xs);
        float xf11 = Lerp(GradCoord(seed, x0, y1, z1, xd0, yd1, zd1), GradCoord(seed, x1, y1, z1, xd1, yd1, zd1), xs);

        float yf0 = Lerp(xf00, xf10, ys);
        float yf1 = Lerp(xf01, xf11, ys);

        return Lerp(yf0, yf1, zs) * 0.964921414852142333984375f;
    }

    // Value Cubic Noise 2D
    private float SingleValueCubic(int seed, float x, float y)
    {
        int x1 = FastFloor(x);
        int y1 = FastFloor(y);

        float xs = x - x1;
        float ys = y - y1;

        x1 *= PrimeX;
        y1 *= PrimeY;
        int x0 = x1 - PrimeX;
        int y0 = y1 - PrimeY;
        int x2 = x1 + PrimeX;
        int y2 = y1 + PrimeY;
        int x3 = x1 + (PrimeX << 1);
        int y3 = y1 + (PrimeY << 1);

        return CubicLerp(
            CubicLerp(ValCoord(seed, x0, y0), ValCoord(seed, x1, y0), ValCoord(seed, x2, y0), ValCoord(seed, x3, y0), xs),
            CubicLerp(ValCoord(seed, x0, y1), ValCoord(seed, x1, y1), ValCoord(seed, x2, y1), ValCoord(seed, x3, y1), xs),
            CubicLerp(ValCoord(seed, x0, y2), ValCoord(seed, x1, y2), ValCoord(seed, x2, y2), ValCoord(seed, x3, y2), xs),
            CubicLerp(ValCoord(seed, x0, y3), ValCoord(seed, x1, y3), ValCoord(seed, x2, y3), ValCoord(seed, x3, y3), xs),
            ys) * (1 / (1.5f * 1.5f));
    }

    // Value Cubic Noise 3D
    private float SingleValueCubic(int seed, float x, float y, float z)
    {
        int x1 = FastFloor(x);
        int y1 = FastFloor(y);
        int z1 = FastFloor(z);

        float xs = x - x1;
        float ys = y - y1;
        float zs = z - z1;

        x1 *= PrimeX;
        y1 *= PrimeY;
        z1 *= PrimeZ;
        int x0 = x1 - PrimeX;
        int y0 = y1 - PrimeY;
        int z0 = z1 - PrimeZ;
        int x2 = x1 + PrimeX;
        int y2 = y1 + PrimeY;
        int z2 = z1 + PrimeZ;
        int x3 = x1 + (PrimeX << 1);
        int y3 = y1 + (PrimeY << 1);
        int z3 = z1 + (PrimeZ << 1);

        return CubicLerp(
            CubicLerp(
                CubicLerp(ValCoord(seed, x0, y0, z0), ValCoord(seed, x1, y0, z0), ValCoord(seed, x2, y0, z0), ValCoord(seed, x3, y0, z0), xs),
                CubicLerp(ValCoord(seed, x0, y1, z0), ValCoord(seed, x1, y1, z0), ValCoord(seed, x2, y1, z0), ValCoord(seed, x3, y1, z0), xs),
                CubicLerp(ValCoord(seed, x0, y2, z0), ValCoord(seed, x1, y2, z0), ValCoord(seed, x2, y2, z0), ValCoord(seed, x3, y2, z0), xs),
                CubicLerp(ValCoord(seed, x0, y3, z0), ValCoord(seed, x1, y3, z0), ValCoord(seed, x2, y3, z0), ValCoord(seed, x3, y3, z0), xs),
                ys),
            CubicLerp(
                CubicLerp(ValCoord(seed, x0, y0, z1), ValCoord(seed, x1, y0, z1), ValCoord(seed, x2, y0, z1), ValCoord(seed, x3, y0, z1), xs),
                CubicLerp(ValCoord(seed, x0, y1, z1), ValCoord(seed, x1, y1, z1), ValCoord(seed, x2, y1, z1), ValCoord(seed, x3, y1, z1), xs),
                CubicLerp(ValCoord(seed, x0, y2, z1), ValCoord(seed, x1, y2, z1), ValCoord(seed, x2, y2, z1), ValCoord(seed, x3, y2, z1), xs),
                CubicLerp(ValCoord(seed, x0, y3, z1), ValCoord(seed, x1, y3, z1), ValCoord(seed, x2, y3, z1), ValCoord(seed, x3, y3, z1), xs),
                ys),
            CubicLerp(
                CubicLerp(ValCoord(seed, x0, y0, z2), ValCoord(seed, x1, y0, z2), ValCoord(seed, x2, y0, z2), ValCoord(seed, x3, y0, z2), xs),
                CubicLerp(ValCoord(seed, x0, y1, z2), ValCoord(seed, x1, y1, z2), ValCoord(seed, x2, y1, z2), ValCoord(seed, x3, y1, z2), xs),
                CubicLerp(ValCoord(seed, x0, y2, z2), ValCoord(seed, x1, y2, z2), ValCoord(seed, x2, y2, z2), ValCoord(seed, x3, y2, z2), xs),
                CubicLerp(ValCoord(seed, x0, y3, z2), ValCoord(seed, x1, y3, z2), ValCoord(seed, x2, y3, z2), ValCoord(seed, x3, y3, z2), xs),
                ys),
            CubicLerp(
                CubicLerp(ValCoord(seed, x0, y0, z3), ValCoord(seed, x1, y0, z3), ValCoord(seed, x2, y0, z3), ValCoord(seed, x3, y0, z3), xs),
                CubicLerp(ValCoord(seed, x0, y1, z3), ValCoord(seed, x1, y1, z3), ValCoord(seed, x2, y1, z3), ValCoord(seed, x3, y1, z3), xs),
                CubicLerp(ValCoord(seed, x0, y2, z3), ValCoord(seed, x1, y2, z3), ValCoord(seed, x2, y2, z3), ValCoord(seed, x3, y2, z3), xs),
                CubicLerp(ValCoord(seed, x0, y3, z3), ValCoord(seed, x1, y3, z3), ValCoord(seed, x2, y3, z3), ValCoord(seed, x3, y3, z3), xs),
                ys),
            zs) * (1 / (1.5f * 1.5f * 1.5f));
    }

    // Value Noise 2D
    private float SingleValue(int seed, float x, float y)
    {
        int x0 = FastFloor(x);
        int y0 = FastFloor(y);

        float xs = InterpHermite(x - x0);
        float ys = InterpHermite(y - y0);

        x0 *= PrimeX;
        y0 *= PrimeY;
        int x1 = x0 + PrimeX;
        int y1 = y0 + PrimeY;

        float xf0 = Lerp(ValCoord(seed, x0, y0), ValCoord(seed, x1, y0), xs);
        float xf1 = Lerp(ValCoord(seed, x0, y1), ValCoord(seed, x1, y1), xs);

        return Lerp(xf0, xf1, ys);
    }

    // Value Noise 3D
    private float SingleValue(int seed, float x, float y, float z)
    {
        int x0 = FastFloor(x);
        int y0 = FastFloor(y);
        int z0 = FastFloor(z);

        float xs = InterpHermite(x - x0);
        float ys = InterpHermite(y - y0);
        float zs = InterpHermite(z - z0);

        x0 *= PrimeX;
        y0 *= PrimeY;
        z0 *= PrimeZ;
        int x1 = x0 + PrimeX;
        int y1 = y0 + PrimeY;
        int z1 = z0 + PrimeZ;

        float xf00 = Lerp(ValCoord(seed, x0, y0, z0), ValCoord(seed, x1, y0, z0), xs);
        float xf10 = Lerp(ValCoord(seed, x0, y1, z0), ValCoord(seed, x1, y1, z0), xs);
        float xf01 = Lerp(ValCoord(seed, x0, y0, z1), ValCoord(seed, x1, y0, z1), xs);
        float xf11 = Lerp(ValCoord(seed, x0, y1, z1), ValCoord(seed, x1, y1, z1), xs);

        float yf0 = Lerp(xf00, xf10, ys);
        float yf1 = Lerp(xf01, xf11, ys);

        return Lerp(yf0, yf1, zs);
    }
}
