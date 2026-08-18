Shader "MinimalGolf/StarrySky"
{
    Properties
    {
        [Header(Sky Gradient)]
        _HorizonColor("Horizon Color", Color) = (0.22, 0.32, 0.52, 1)
        _ZenithColor("Zenith Color", Color) = (0.02, 0.04, 0.14, 1)
        _HorizonHeight("Horizon Height", Range(-0.3, 0.5)) = 0.02
        _HorizonFalloff("Horizon Falloff", Range(0.01, 0.8)) = 0.25

        [Header(Stars)]
        _StarDensity("Star Density", Range(0.2, 4.0)) = 1.2
        _StarSharpness("Star Sharpness", Range(5, 200)) = 42
        _StarIntensity("Star Intensity", Range(0, 2)) = 1.0
        _StarColor("Star Color", Color) = (1, 1, 1, 1)
        _StarColorVariation("Star Color Variation", Range(0, 1)) = 0.35
        _StarMinBrightness("Star Min Brightness", Range(0, 1)) = 0.35

        [Header(Animation)]
        _TwinkleSpeed("Twinkle Speed", Range(0, 5)) = 1.2
        _TwinkleAmount("Twinkle Amount", Range(0, 1)) = 0.35
        _StarRotationSpeed("Star Rotation Speed", Range(-2, 2)) = 0.06
        _TimeScale("Time Scale", Range(0, 3)) = 1.0

        [Header(Comets)]
        _EnableComets("Enable Comets", Float) = 1
        _CometColor("Comet Color", Color) = (0.85, 0.95, 1, 1)
        _CometIntensity("Comet Intensity", Range(0, 2)) = 0.28
        _CometSpeed("Comet Speed", Range(0, 3)) = 0.35
        _CometLength("Comet Length", Range(0.05, 1.0)) = 0.35
        _CometSharpness("Comet Sharpness", Range(5, 100)) = 38
        _CometFrequency("Comet Frequency", Range(0, 0.15)) = 0.015
        _CometTailFalloff("Comet Tail Falloff", Range(0.5, 8)) = 3.5
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            half4 _HorizonColor;
            half4 _ZenithColor;
            half _HorizonHeight;
            half _HorizonFalloff;
            half _StarDensity;
            half _StarSharpness;
            half _StarIntensity;
            half4 _StarColor;
            half _StarColorVariation;
            half _StarMinBrightness;
            half _TwinkleSpeed;
            half _TwinkleAmount;
            half _StarRotationSpeed;
            half _TimeScale;
            half _EnableComets;
            half4 _CometColor;
            half _CometIntensity;
            half _CometSpeed;
            half _CometLength;
            half _CometSharpness;
            half _CometFrequency;
            half _CometTailFalloff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // -- Hash helpers --
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }
            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float2 hash22(float2 p)
            {
                return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453);
            }

            float3 rotateY(float3 p, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3(p.x * c + p.z * s, p.y, -p.x * s + p.z * c);
            }

            float starField(float3 dir, out float starHash, out float2 cellId)
            {
                float3 d = dir;
                float2 uv = d.xz / (abs(d.y) * 0.5 + 1.0);
                uv *= _StarDensity * 2.0;

                float2 gv = frac(uv) - 0.5;
                float2 id = floor(uv);

                float bestDist = 10.0;
                float bestHash = 0;
                float2 bestCell = id;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbour = float2((float)x, (float)y);
                        float2 cell = id + neighbour;
                        float2 p = hash22(cell) - 0.5;
                        p *= 0.78;
                        float2 diff = neighbour + p - gv;
                        float d2 = dot(diff, diff);
                        if (d2 < bestDist)
                        {
                            bestDist = d2;
                            bestHash = hash21(cell);
                            bestCell = cell;
                        }
                    }
                }
                starHash = bestHash;
                cellId = bestCell;
                return bestDist;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                // Unity skybox uses texcoord as direction; fallback to vertex.xyz if needed
                // Built-in skybox mesh provides texcoord as direction, vertex as position
                float3 dir = v.texcoord;
                // Some meshes may not have texcoord - use vertex direction
                if (dot(dir, dir) < 0.001) dir = v.vertex.xyz;
                o.dir = dir;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 dir = normalize(i.dir);
                float time = _Time.y * _TimeScale;
                if (abs(_StarRotationSpeed) > 0.001)
                {
                    float angle = time * _StarRotationSpeed * 0.15;
                    dir = rotateY(dir, angle);
                }

                float h = dir.y;
                float horizonMask01 = smoothstep(_HorizonHeight, _HorizonHeight + _HorizonFalloff, h);
                float starHorizonMask = smoothstep(_HorizonHeight - 0.02, _HorizonHeight + 0.08, h);
                half3 skyCol = lerp(_HorizonColor.rgb, _ZenithColor.rgb, saturate(horizonMask01 * 1.15));

                float starHash;
                float2 cellId;
                float dist2 = starField(dir, starHash, cellId);
                float dist = sqrt(dist2);

                float brightnessSelect = hash11(starHash * 437.0);
                float starPresence = step(0.38, brightnessSelect);
                float magnitude = lerp(_StarMinBrightness, 1.0, hash11(starHash * 812.0));

                float star = 1.0 - saturate(dist * _StarSharpness * 0.25);
                star = pow(star, 9.0) * starPresence * magnitude;

                float twPhase = starHash * 6.2831853 * 1.7;
                float tw = sin(time * _TwinkleSpeed * (0.7 + 0.6 * hash11(starHash * 91.0)) + twPhase);
                tw = tw * 0.5 + 0.5;
                float twinkle = lerp(1.0, tw, _TwinkleAmount);
                twinkle = lerp(0.85, twinkle, 0.5 + 0.5 * brightnessSelect);
                star *= twinkle;

                float colorShift = (hash11(starHash * 123.45) - 0.5) * 2.0;
                half3 starTint = _StarColor.rgb + colorShift * _StarColorVariation * half3(0.25, 0.15, -0.2);
                starTint = max(starTint, half3(0.3, 0.3, 0.5));

                half3 col = skyCol + star * starTint * _StarIntensity * starHorizonMask;

                if (_EnableComets > 0.5 && _CometFrequency > 0.0001 && _CometIntensity > 0.001)
                {
                    float cometTime = time * _CometSpeed;
                    float2 trailDir = normalize(float2(0.72, 0.68));
                    float2 cometUVBase = dir.xz / (abs(dir.y) * 0.5 + 1.0) * _StarDensity * 2.0;
                    float2 cometUV = cometUVBase - trailDir * cometTime * 0.55;

                    float2 cId = floor(cometUV);
                    float2 cFract = frac(cometUV) - 0.5;
                    float cHash = hash21(cId);
                    float spawnThreshold = 1.0 - _CometFrequency;
                    float isCometCell = step(spawnThreshold, cHash);

                    if (isCometCell > 0.5)
                    {
                        float2 cometOffset = hash22(cId + 17.3) - 0.5;
                        float2 headOffset = cometOffset * 0.6;

                        float alongTrail = dot(cFract - headOffset, -trailDir);
                        float perp = length(cFract - headOffset - alongTrail * (-trailDir));
                        float behind = step(0.0, alongTrail);
                        float tailMask = exp(-alongTrail * _CometTailFalloff) * behind;
                        tailMask *= exp(-perp * _CometSharpness * 4.5);
                        float headMask = exp(-length(cFract - headOffset) * _CometSharpness * 8.0);

                        float cometMask = saturate(headMask * 1.2 + tailMask * 0.25 * _CometLength);
                        float lifePhase = frac(cHash * 2.4 + cometTime * 0.08);
                        float lifeWindow = smoothstep(0.0, 0.2, lifePhase) * (1.0 - smoothstep(0.75, 1.0, lifePhase));
                        float lifeJitter = 0.55 + 0.45 * sin(time * 0.22 + cHash * 6.28);
                        lifeWindow *= lifeJitter;

                        cometMask *= starHorizonMask * lifeWindow;

                        half3 cometCol = _CometColor.rgb * cometMask * _CometIntensity * 1.2;
                        cometCol *= lerp(half3(1.0, 0.92, 0.78), half3(1,1,1), headMask / max(cometMask, 0.001));
                        col += cometCol;
                    }
                }

                return half4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
