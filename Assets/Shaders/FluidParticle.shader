Shader "Custom/FluidParticle"
{
    Properties
    {
        _Smoothness("Smoothness", Range(0, 1)) = 0.9
        _SpecGloss("Specular", Color) = (0.95, 0.97, 1, 1)
        _FresnelPower("Fresnel Power", Range(0.5, 6)) = 2.5
        _Opacity("Opacity", Range(0, 1)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _Smoothness;
            float4 _SpecGloss;
            float _FresnelPower;
            float _Opacity;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos = UnityObjectToClipPos(v.vertex);
                float3 posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = posWS;
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.viewDirWS = normalize(_WorldSpaceCameraPos.xyz - posWS);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float3 normal = normalize(i.normalWS);
                float3 viewDir = normalize(i.viewDirWS);

                float nDotV = saturate(dot(normal, viewDir));

                // Soft center blending: sphere centers merge into continuous fluid
                float centerWeight = pow(nDotV, 0.6);
                float fresnel = pow(1.0 - nDotV, _FresnelPower);

                // Edges semi-transparent so overlapping spheres merge smoothly
                float alpha = lerp(centerWeight * 0.6, 1.0, fresnel * 0.7) * _Opacity;

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = max(0, dot(normal, lightDir));

                float3 ambient = ShadeSH9(float4(normal, 1));

                // Wrap lighting for softer diffuse
                float wrap = 0.15;
                float diffuse = max(0, (NdotL + wrap) / (1 + wrap));
                float3 diffuseColor = color.rgb * (diffuse * _LightColor0.rgb * 1.4 + ambient * 0.5);

                float3 halfVec = normalize(lightDir + viewDir);
                float NdotH = max(0, dot(normal, halfVec));
                float spec = pow(NdotH, _Smoothness * 128 + 1);

                // Fresnel-Schlick for specular reflection
                float fresnelSchlick = _SpecGloss.a + (1.0 - _SpecGloss.a) * pow(1.0 - nDotV, 5.0);

                float3 finalColor = diffuseColor + _SpecGloss.rgb * spec * fresnelSchlick * 0.5;

                // Rim glow adds color boost
                finalColor += fresnel * 0.15 * color.rgb;

                return fixed4(finalColor, alpha);
            }
            ENDCG
        }
    }
    FallBack "Standard"
}
