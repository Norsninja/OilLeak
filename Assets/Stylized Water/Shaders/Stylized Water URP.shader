Shader "Stylized/WaterURP"
{
    Properties
    {
        // Densities
        _DepthDensity ("Depth", Range(0,1)) = 0.5
        _DistanceDensity ("Distance", Range(0,1)) = 0.1

        // Normal
        [NoScaleOffset]_WaveNormalMap ("Wave Normal", 2D) = "bump" {}
        _WaveNormalScale ("Normal Scale", Float) = 10.0
        _WaveNormalSpeed ("Normal Speed", Float) = 1.0

        // Base Colors
        [HDR]_ShallowColor ("Shallow", Color) = (0.44, 0.95, 0.36, 1)
        [HDR]_DeepColor ("Deep", Color)    = (0.0, 0.05, 0.19, 1)
        [HDR]_FarColor ("Far", Color)      = (0.04, 0.27, 0.75, 1)

        // Reflection
        _ReflectionContribution ("Reflection", Range(0,1)) = 1.0

        // SSS
        [HDR]_SSSColor ("SSS Color", Color) = (1,1,1,1)

        // Foam
        [NoScaleOffset]_FoamTexture ("Foam", 2D) = "black" {}
        _FoamScale ("Foam Scale", Float) = 1.0
        _FoamSpeed ("Foam Speed", Float) = 1.0
        _FoamNoiseScale ("Foam Noise", Range(0,1)) = 0.5
        _FoamContribution ("Foam Amount", Range(0,1)) = 1.0

        // Sun Specular
        [HDR]_SunSpecularColor ("Sun Specular", Color) = (1,1,1,1)
        _SunSpecularExponent ("Sun Exponent", Float) = 1000

        // Sparkles (optional)
        [NoScaleOffset]_SparklesNormalMap ("Sparkle Normal", 2D) = "bump" {}
        _SparkleScale ("Sparkle Scale", Float) = 10
        _SparkleSpeed ("Sparkle Speed", Float) = 0.75
        [HDR]_SparkleColor ("Sparkle Color", Color) = (1,1,1,1)
        _SparkleExponent ("Sparkle Exponent", Float) = 10000

        // Edge Foam
        [HDR]_EdgeFoamColor ("Edge Foam Color", Color) = (1,1,1,1)
        _EdgeFoamDepth ("Edge Foam Depth", Float) = 10.0

        // Waves #1
        _Wave1Direction ("Wave1 Direction (0-1)", Range(0,1)) = 0
        _Wave1Amplitude ("Wave1 Amplitude", Float) = 1
        _Wave1Wavelength ("Wave1 Wavelength", Float) = 1
        _Wave1Speed ("Wave1 Speed", Float) = 1

        // Waves #2
        _Wave2Direction ("Wave2 Direction (0-1)", Range(0,1)) = 0
        _Wave2Amplitude ("Wave2 Amplitude", Float) = 1
        _Wave2Wavelength ("Wave2 Wavelength", Float) = 1
        _Wave2Speed ("Wave2 Speed", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _SPARKLES
            // If you add shadows later:
            // #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

            // Constants
            #define PI 3.14159265359
            #define TWO_PI 6.28318530718

            // Textures - _CameraOpaqueTexture is already declared in DeclareOpaqueTexture.hlsl
            TEXTURE2D(_WaveNormalMap);     SAMPLER(sampler_WaveNormalMap);
            TEXTURE2D(_FoamTexture);       SAMPLER(sampler_FoamTexture);
            TEXTURE2D(_SparklesNormalMap); SAMPLER(sampler_SparklesNormalMap);

            // Properties (auto-generated cbuffers by SRP Batcher if kept as uniforms)
            float    _DepthDensity, _DistanceDensity;
            float3   _ShallowColor, _DeepColor, _FarColor;
            float    _WaveNormalScale, _WaveNormalSpeed;
            float    _ReflectionContribution;
            float3   _SSSColor;
            float    _FoamScale, _FoamSpeed, _FoamNoiseScale, _FoamContribution;
            float3   _SunSpecularColor; float _SunSpecularExponent;
            float    _SparkleScale, _SparkleSpeed, _SparkleExponent; float3 _SparkleColor;
            float3   _EdgeFoamColor; float _EdgeFoamDepth;
            float    _Wave1Direction, _Wave1Amplitude, _Wave1Wavelength, _Wave1Speed;
            float    _Wave2Direction, _Wave2Amplitude, _Wave2Wavelength, _Wave2Speed;

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3x3 tbn       : TEXCOORD2; // tangent/binormal/normal (world)
                float2 screenUV    : TEXCOORD5; // normalized screen uv for opaque/depth
            };

            // ---- Helpers ----

            float2 DirFrom01(float sd) {
                float a = PI * sd;
                return float2(cos(a), sin(a));
            }

            float SimpleWave(float2 worldXZ, float2 dir, float wavelength, float amplitude, float speed, float t) {
                // Phase = dot(k, x) - wt ; k=2π/λ
                float k = TWO_PI / max(0.0001, wavelength);
                float phase = dot(dir, worldXZ) * k - speed * t * k;
                return sin(phase) * amplitude;
            }

            float GetWaveHeight(float2 worldXZ, float t) {
                float2 d1 = DirFrom01(_Wave1Direction);
                float2 d2 = DirFrom01(_Wave2Direction);
                float w1 = SimpleWave(worldXZ, d1, _Wave1Wavelength, _Wave1Amplitude, _Wave1Speed, t);
                float w2 = SimpleWave(worldXZ, d2, _Wave2Wavelength, _Wave2Amplitude, _Wave2Speed, t);
                return w1 + w2;
            }

            // Finite-difference surface basis
            float3x3 GetWaveTBN(float2 worldXZ, float d, float t) {
                float h  = GetWaveHeight(worldXZ, t);
                float hx = GetWaveHeight(worldXZ - float2(d,0), t);
                float hz = GetWaveHeight(worldXZ - float2(0,d), t);
                float3 tangent  = normalize(float3(0, h - hz, d));
                float3 binormal = normalize(float3(d, h - hx, 0));
                float3 normal   = normalize(cross(binormal, tangent));
                return transpose(float3x3(tangent, binormal, normal));
            }

            // 4-way panned normal; returns TS normal (–1..1), z reconstructed
            float3 MotionFourWayChaosNorm(TEXTURE2D_PARAM(tex, samp), float2 baseUV, float scale, float speed, float t) {
                float2 uv1 = (baseUV * (1/scale)) + float2(  t*speed*0.1,   t*speed*0.1);
                float2 uv2 = (baseUV * (1/scale)) + float2(  -t*speed*0.1,  -t*speed*0.1) + float2(0.418, 0.355);
                float2 uv3 = (baseUV * (1/scale)) + float2(-t*speed*0.1,     t*speed*0.1) + float2(0.865, 0.148);
                float2 uv4 = (baseUV * (1/scale)) + float2(  t*speed*0.1,   -t*speed*0.1) + float2(0.651, 0.752);

                float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uv1));
                float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uv2));
                float3 n3 = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uv3));
                float3 n4 = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uv4));

                float3 n = normalize(n1 + n2 + n3 + n4);
                return n; // TS
            }

            // 4-way panned texture (non-normal) for foam
            float3 MotionFourWayChaos(TEXTURE2D_PARAM(tex, samp), float2 baseUV, float scale, float speed, float t) {
                float2 uv1 = (baseUV * (1/scale)) + float2(  t*speed*0.1,   t*speed*0.1);
                float2 uv2 = (baseUV * (1/scale)) + float2(  -t*speed*0.1,  -t*speed*0.1) + float2(0.418, 0.355);
                float2 uv3 = (baseUV * (1/scale)) + float2(-t*speed*0.1,     t*speed*0.1) + float2(0.865, 0.148);
                float2 uv4 = (baseUV * (1/scale)) + float2(  t*speed*0.1,   -t*speed*0.1) + float2(0.651, 0.752);

                float3 s1 = SAMPLE_TEXTURE2D(tex, samp, uv1).rgb;
                float3 s2 = SAMPLE_TEXTURE2D(tex, samp, uv2).rgb;
                float3 s3 = SAMPLE_TEXTURE2D(tex, samp, uv3).rgb;
                float3 s4 = SAMPLE_TEXTURE2D(tex, samp, uv4).rgb;

                return (s1 + s2 + s3 + s4) * 0.25;
            }

            float FresnelSchlick(float cosTheta) {
                // basic Schlick with F0 ~ 0.02
                return saturate(pow(1.0 - cosTheta, 5.0));
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS).xyz;

                // Vertex displacement
                float t = _Time.y; // scaled time
                positionWS.y += GetWaveHeight(positionWS.xz, t);

                OUT.positionWS = positionWS;
                OUT.positionHCS = TransformWorldToHClip(positionWS);

                OUT.uv = IN.uv;

                // Screen UV for _CameraOpaqueTexture, _CameraDepthTexture
                float2 uv01 = GetNormalizedScreenSpaceUV(OUT.positionHCS);
                OUT.screenUV = uv01;

                // TBN for normal mapping (finite diff)
                OUT.tbn = GetWaveTBN(positionWS.xz, 0.01, t);

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // World normal from TS normal map through TBN
                float t = _Time.y;
                float3 nTS = MotionFourWayChaosNorm(TEXTURE2D_ARGS(_WaveNormalMap, sampler_WaveNormalMap),
                                      IN.positionWS.xz, _WaveNormalScale, _WaveNormalSpeed, t);
                float3 nWS = normalize(mul(IN.tbn, nTS));

                // Depth sampling
                #if defined(_USE_DEPTH_TEXTURE) || 1
                float rawDepth = SampleSceneDepth(IN.screenUV);
                float sceneLinearEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float thisLinearEye  = LinearEyeDepth(IN.positionHCS.z / IN.positionHCS.w, _ZBufferParams);
                float opticalDepth = abs(sceneLinearEye - thisLinearEye);
                #else
                float opticalDepth = 0;
                #endif

                // Distance mask
                float dist = distance(IN.positionWS, _WorldSpaceCameraPos);
                float transmittance = exp(-_DepthDensity * opticalDepth);
                float distanceMask  = exp(-_DistanceDensity * dist);

                // Scene color (refraction) via opaque texture
                float2 refractUV = IN.screenUV + (nTS.xy * 0.02); // small distortion
                float3 sceneColor = SampleSceneColor(refractUV);

                // Base water color blend
                float3 baseColor = sceneColor * _ShallowColor;
                baseColor = lerp(_DeepColor, baseColor, transmittance);
                baseColor = lerp(_FarColor, baseColor, distanceMask);

                // Fresnel reflection via reflection probe/IBL
                // Build surface data for URP IBL (minimal)
                float3 reflDir = reflect(-viewDirWS, nWS);
                // GlossyEnvironmentReflection returns prefiltered env
                float3 reflColor = GlossyEnvironmentReflection(reflDir, 0.0, 1.0);
                float fresnel = FresnelSchlick(saturate(dot(nWS, viewDirWS)));
                float3 reflected = reflColor * fresnel * distanceMask * _ReflectionContribution;

                // SSS (backlit)
                Light mainLight = GetMainLight();
                float sssMask = saturate(dot(viewDirWS, mainLight.direction));
                float3 sssColor = _SSSColor * GetWaveHeight(IN.positionWS.xz, t) * sssMask;

                // Foam: moving noise
                float2 foamUV = (IN.positionWS.xz / max(0.0001, _FoamScale)) + (_FoamNoiseScale * nTS.xz);
                float3 foamSample = MotionFourWayChaos(TEXTURE2D_ARGS(_FoamTexture, sampler_FoamTexture),
                                                       foamUV, _FoamScale, _FoamSpeed, t);
                float3 foamColor = foamSample * distanceMask * _FoamContribution;

                // Sun specular: hard-edged highlight
                float3 viewR = reflect(-viewDirWS, nWS);
                float sunSpec = saturate(dot(viewR, mainLight.direction));
                sunSpec = round(saturate(pow(sunSpec, _SunSpecularExponent)));
                float3 sunSpecColor = _SunSpecularColor * sunSpec;

                // Sparkles (optional)
                #ifdef _SPARKLES
                float3 sp1 = MotionFourWayChaosNorm(TEXTURE2D_ARGS(_SparklesNormalMap, sampler_SparklesNormalMap),
                                    IN.positionWS.xz, _SparkleScale, _SparkleSpeed, t);
                float3 sp2 = MotionFourWayChaosNorm(TEXTURE2D_ARGS(_SparklesNormalMap, sampler_SparklesNormalMap),
                                    IN.positionWS.xz, _SparkleScale * 0.5, _SparkleSpeed, t + 0.37);
                float sparkleMask = dot(sp1, sp2);
                sparkleMask = ceil(saturate(pow(sparkleMask, _SparkleExponent)));
                float3 sparkleColor = _SparkleColor * sparkleMask * distanceMask;
                #else
                float3 sparkleColor = 0;
                #endif

                // Edge foam via optical depth clip
                float edgeMask = round(exp(-opticalDepth / max(0.0001, _EdgeFoamDepth)));
                float3 edgeFoamColor = _EdgeFoamColor * edgeMask;

                // Compose
                float3 color = baseColor + reflected + sssColor + foamColor + sunSpecColor + sparkleColor + edgeFoamColor;

                // Optional: manual distance fog (URP volumes usually handle fog; omit for MVP)
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}