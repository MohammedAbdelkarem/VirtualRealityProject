Shader "Custom/FluidParticle"
{
    Properties
    {
        _Smoothness("Smoothness", Range(0, 1)) = 0.85
        _SpecGloss("Specular", Color) = (0.9, 0.95, 1, 1)
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 3
        _Opacity("Opacity", Range(0, 1)) = 0.8
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

                float fresnel = 1.0 - saturate(dot(normal, viewDir));
                fresnel = pow(fresnel, _FresnelPower);

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = max(0, dot(normal, lightDir));
                float3 ambient = ShadeSH9(float4(normal, 1));
                float3 diffuse = color.rgb * (NdotL * _LightColor0.rgb + ambient);

                float3 halfVec = normalize(lightDir + viewDir);
                float NdotH = max(0, dot(normal, halfVec));
                float spec = pow(NdotH, _Smoothness * 128 + 1);

                float3 finalColor = diffuse + _SpecGloss.rgb * spec * 0.5;
                float alpha = lerp(_Opacity, 1.0, fresnel);

                return fixed4(finalColor, alpha);
            }
            ENDCG
        }
    }
    FallBack "Standard"
}
