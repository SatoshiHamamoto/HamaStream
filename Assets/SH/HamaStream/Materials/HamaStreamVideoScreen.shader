/*
Modified from ArchiTech.ProTV 3.0.0-beta.31 ProTVVideoScreen.shader.
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

Shader "HamaStream/VideoScreen"
{
    Properties
    {
        [MainTexture] _MainTex ("Standby Texture", 2D) = "black" {}
        _VideoTex ("Corrected Video Texture", 2D) = "black" {}
        [HideInInspector] _UseVideoTex ("Use Video Texture", Float) = 0
        [HideInInspector] _SourceAspect ("Source Aspect Ratio", Float) = 1.777778
        _ScreenAspect ("Screen Aspect Ratio", Float) = 1.777778
        [Enum(Fit Inside, 0, Fill Screen, 1)] _AspectFitMode ("Aspect Fit Mode", Float) = 0
        _Brightness ("Brightness", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Back
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _VideoTex;
            float _UseVideoTex;
            float _SourceAspect;
            float _ScreenAspect;
            float _AspectFitMode;
            float _Brightness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float sourceAspect = max(_SourceAspect, 0.001);
                float screenAspect = max(_ScreenAspect, 0.001);
                float2 uv = i.uv;
                if (_AspectFitMode < 0.5)
                {
                    float2 displayScale = sourceAspect > screenAspect
                        ? float2(1.0, screenAspect / sourceAspect)
                        : float2(sourceAspect / screenAspect, 1.0);
                    uv = (uv - 0.5) / displayScale + 0.5;
                    if (any(uv < 0.0) || any(uv > 1.0)) return float4(0, 0, 0, 1);
                }
                else if (sourceAspect > screenAspect)
                {
                    uv.x = (uv.x - 0.5) * screenAspect / sourceAspect + 0.5;
                }
                else
                {
                    uv.y = (uv.y - 0.5) * sourceAspect / screenAspect + 0.5;
                }

                float4 color = _UseVideoTex > 0.5
                    ? tex2D(_VideoTex, uv)
                    : tex2D(_MainTex, uv);
                color.rgb *= _Brightness;
                return float4(color.rgb, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
