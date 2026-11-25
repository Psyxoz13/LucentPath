#ifndef LUCENT_PATH_CHECKERBOARD_INCLUDED
#define LUCENT_PATH_CHECKERBOARD_INCLUDED

int _EnableCheckerboard;
int _FrameParity;
int _CheckerboardFullRateHistoryFrames;
int _CheckerboardPromotionPhase;
Texture2D<float4> _CheckerboardHistoryMoments;

bool CheckerboardShadeThisFrame(uint2 pixel, int frameParity)
{
    return ((pixel.x + pixel.y + (uint)frameParity) & 1u) == 0u;
}

bool IsCheckerboardHistoryYoung(uint2 pixel)
{
    return _CheckerboardFullRateHistoryFrames > 0 &&
        _CheckerboardHistoryMoments.Load(int3(pixel, 0)).z < (float)_CheckerboardFullRateHistoryFrames;
}

bool CheckerboardShadePixel(uint2 pixel)
{
    if (_EnableCheckerboard == 0)
    {
        return true;
    }

    if (CheckerboardShadeThisFrame(pixel, _FrameParity))
        return true;

    if (_CheckerboardPromotionPhase > 0)
    {
        uint phase = (uint)(_CheckerboardPromotionPhase - 1);
        if ((((pixel.x >> 1u) + pixel.y + phase) & 3u) != 0u)
            return false;
    }

    return IsCheckerboardHistoryYoung(pixel);
}

#endif
