// Keep Clarity — "Crisp Mode" fullscreen post effect.
// Replicates a ReShade CAS + Chromaticity(vibrance) stack in ONE pass.
// Built-in render pipeline image effect (Graphics.Blit). Target: Unity 2022.3.62f3.
//
// CAS = AMD FidelityFX Contrast Adaptive Sharpening (contrast-aware, low-halo).
// Vibrance = saturation boost weighted toward already-desaturated pixels (the "pop"),
//            a faithful stand-in for ReShade's Chromaticity.
//
// Assign this asset the AssetBundle name "crisp" and build for StandaloneWindows64.
Shader "Hidden/KeepClarity/Crisp"
{
    Properties
    {
        _MainTex   ("Texture", 2D)        = "white" {}
        _Sharpness ("Sharpness", Range(0,1)) = 0.6
        _Vibrance  ("Vibrance",  Range(0,1)) = 0.15
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex   vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float     _Sharpness;
            float     _Vibrance;

            float3 S(float2 uv) { return tex2D(_MainTex, uv).rgb; }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 ts = _MainTex_TexelSize.xy;
                float2 uv = i.uv;

                // 3x3 neighborhood (a b c / d e f / g h k), e = center.
                float3 a = S(uv + float2(-ts.x, -ts.y));
                float3 b = S(uv + float2( 0.0,  -ts.y));
                float3 c = S(uv + float2( ts.x, -ts.y));
                float3 d = S(uv + float2(-ts.x,  0.0 ));
                float3 e = S(uv);
                float3 f = S(uv + float2( ts.x,  0.0 ));
                float3 g = S(uv + float2(-ts.x,  ts.y));
                float3 h = S(uv + float2( 0.0,   ts.y));
                float3 k = S(uv + float2( ts.x,  ts.y));

                // CAS min/max: 5-tap cross, then fold in the 4 diagonals.
                float3 mn = min(min(min(d, e), min(f, b)), h);
                mn += min(mn, min(min(a, c), min(g, k)));
                float3 mx = max(max(max(d, e), max(f, b)), h);
                mx += max(mx, max(max(a, c), max(g, k)));

                // Amplification (per channel).
                float3 rcpMx = rcp(max(mx, 1e-5));
                float3 amp   = saturate(min(mn, 2.0 - mx) * rcpMx);
                amp = sqrt(amp);

                // Peak weight: -1/8 (soft) .. -1/5 (sharp). More negative = sharper.
                float  peak = -rcp(lerp(8.0, 5.0, saturate(_Sharpness)));
                float3 w    = amp * peak;

                // Sharpen: weighted cross + center, normalized.
                float3 sharp = saturate((b*w + d*w + f*w + h*w + e) * rcp(1.0 + 4.0 * w));

                // Vibrance: push saturation, more on low-sat pixels.
                float  luma = dot(sharp, float3(0.2126, 0.7152, 0.0722));
                float  sat  = max(sharp.r, max(sharp.g, sharp.b))
                            - min(sharp.r, min(sharp.g, sharp.b));
                float3 outc = lerp(luma.xxx, sharp, 1.0 + _Vibrance * (1.0 - sat));

                return fixed4(saturate(outc), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
