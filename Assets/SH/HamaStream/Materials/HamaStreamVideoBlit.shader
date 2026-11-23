/*
Derived from ArchiTech.ProTV 3.0.0-beta.31 ProTVBlit.shader (Video Correction pass).
(based on the ISC License)

Copyright (c) 2020-2025 ArchiTechVR

Permission to use, copy, modify, and/or distribute this asset for any
purpose, except for resale in whole or in part, with or without fee is hereby granted,
provided that the above copyright notice and this permission notice appear in all copies.
For clarity, permission is expressly granted to distribute and sell assets and prefabs
which make use of the functions and/or content of this asset.

THE ASSET IS PROVIDED "AS IS" AND THE AUTHOR(S) DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS ASSET INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY
AND FITNESS. IN NO EVENT SHALL THE AUTHOR(S) BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR
OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
PERFORMANCE OF THIS ASSET.
*/

Shader "Hidden/HamaStream/VideoCorrection"
{
    Properties
    {
        [HideInInspector] _MainTex ("Blit Texture", 2D) = "black" {}
        // hidden in inspector because this should only be assigned via script
        [HideInInspector] _MainTex_ST_Override ("Scale/Offset Override", Vector) = (1.0, 1.0, 0.0, 0.0)
    }
    SubShader
    {
        Pass
        {
            Name "Video Correction"
            // required for Blit to succeed on Android
            ZTest Always
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_ST_Override;

            float _AVPro;
            float _SkipGamma;
            float4 _GammaZone;

            struct vertdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct fragdata
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fragdata vert(vertdata v)
            {
                fragdata o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(fragdata i) : SV_Target
            {
                // because default properties are stupid while doing a Blit, allow a custom _ST override vector if the provided value is not the default one
                if (any(_MainTex_ST_Override != float4(1, 1, 0, 0))) _MainTex_ST = _MainTex_ST_Override;

                // scale/offset math for AVPro flip correction (this is the same as the TRANSFORM_TEX macro)
                float4 tex = tex2D(_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw);

                // check that the current UV is within the defined Gamma Zone
                bool uvCheck = all(i.uv >= _GammaZone.zw) && all(i.uv <= _GammaZone.zw + _GammaZone.xy);

                #ifndef UNITY_COLORSPACE_GAMMA
                // if not in gamma colorspace, handle avpro conversion
                tex.rgb = _AVPro && !_SkipGamma && uvCheck ? GammaToLinearSpace(tex.rgb) : tex.rgb;
                #endif

                return tex;
            }
            ENDCG
        }

    }
    Fallback Off
}
