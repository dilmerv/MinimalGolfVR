#ifndef REVEAL_CLIP_HLSL
#define REVEAL_CLIP_HLSL

float4 _RevealCenter0;
float _RevealRadius0;
float _RevealSoftness0;
float4 _RevealCenter1;
float _RevealRadius1;
float _RevealSoftness1;
int _RevealCount;
int _RevealEnabled;
int _RevealInvert;

float RevealMask(float3 worldPos)
{
    if (_RevealEnabled == 0 || _RevealCount == 0) return 1.0;
    bool invert = _RevealInvert != 0;
    if (invert)
    {
        float maxA = 0.0;
        {
            float d = distance(worldPos, _RevealCenter0.xyz);
            float soft = max(0.001, _RevealSoftness0);
            float mask = smoothstep(_RevealRadius0, _RevealRadius0 + soft, d);
            maxA = max(maxA, 1.0 - mask);
        }
        if (_RevealCount > 1)
        {
            float d = distance(worldPos, _RevealCenter1.xyz);
            float soft = max(0.001, _RevealSoftness1);
            float mask = smoothstep(_RevealRadius1, _RevealRadius1 + soft, d);
            maxA = max(maxA, 1.0 - mask);
        }
        return maxA;
    }
    else
    {
        float minA = 1.0;
        {
            float d = distance(worldPos, _RevealCenter0.xyz);
            float soft = max(0.001, _RevealSoftness0);
            float mask = smoothstep(_RevealRadius0, _RevealRadius0 + soft, d);
            minA = min(minA, mask);
        }
        if (_RevealCount > 1)
        {
            float d = distance(worldPos, _RevealCenter1.xyz);
            float soft = max(0.001, _RevealSoftness1);
            float mask = smoothstep(_RevealRadius1, _RevealRadius1 + soft, d);
            minA = min(minA, mask);
        }
        return minA;
    }
}

#endif
