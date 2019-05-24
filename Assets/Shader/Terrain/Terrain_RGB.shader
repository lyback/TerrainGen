Shader "Terrain/Terrain_RGB"
{
    Properties
    {
        _Control1 ("Control 1", 2D) = "black" { }
        _Control2 ("Control 2", 2D) = "black" { }
        _Control3 ("Control 3", 2D) = "black" { }
        _Splat1 ("Layer 1", 2D) = "black" { }
        _Splat2 ("Layer 2", 2D) = "black" { }
        _Splat3 ("Layer 3", 2D) = "black" { }
        _Splat4 ("Layer 4", 2D) = "black" { }
        _Splat5 ("Layer 5", 2D) = "black" { }
        _Splat6 ("Layer 6", 2D) = "black" { }
        _Splat7 ("Layer 7", 2D) = "black" { }
        _Splat8 ("Layer 8", 2D) = "black" { }
        _Splat9 ("Layer 9", 2D) = "black" { }
    }

    SubShader
    {
        Tags { "LightMode" = "ForwardBase" "Queue" = "Transparent-100" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

        Pass
        {

            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct a2v
            {
                float4 vertex: POSITION;
                float2 texcoord: TEXCOORD0;
                float3 normal: NORMAL;
            };

            struct v2f
            {
                float4 vertex: SV_POSITION;

                float2 uv0: TEXCOORD0;
                float2 uv1: TEXCOORD1;

                float4 normal: TEXCOORD2;
            };

            sampler2D _Control1, _Control2, _Control3;
            sampler2D _Splat1, _Splat2, _Splat3, _Splat4, _Splat5, _Splat6, _Splat7, _Splat8, _Splat9;

            float4 _Control1_ST, _Control2_ST, _Control3_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST, _Splat4_ST, _Splat5_ST, _Splat6_ST, _Splat7_ST, _Splat8_ST, _Splat9_ST;

            v2f vert(a2v v)
            {
                v2f o;
                
                v.vertex.y += (0.1);

                o.vertex = UnityObjectToClipPos(v.vertex);

                o.uv0 = TRANSFORM_TEX(v.texcoord, _Control1);
                o.uv1 = TRANSFORM_TEX(v.texcoord, _Splat1);

                o.normal.xyz = mul(v.normal, (float3x3)unity_WorldToObject);
                o.normal.w = v.vertex.y;

                return o;
            }

            fixed4 frag(v2f i): SV_Target
            {

                fixed3 col_control1 = tex2D(_Control1, i.uv0).rgb;
                fixed3 col_control2 = tex2D(_Control2, i.uv0).rgb;
                fixed3 col_control3 = tex2D(_Control3, i.uv0).rgb;

                fixed3 albedo = fixed3(1.0, 1.0, 1.0);
                albedo = tex2D(_Splat1, i.uv1).rgb * col_control1.r;
                albedo += tex2D(_Splat2, i.uv1).rgb * col_control1.g;
                albedo += tex2D(_Splat3, i.uv1).rgb * col_control1.b;
                albedo += tex2D(_Splat4, i.uv1).rgb * col_control2.r;
                albedo += tex2D(_Splat5, i.uv1).rgb * col_control2.g;
                albedo += tex2D(_Splat6, i.uv1).rgb * col_control2.b;
                albedo += tex2D(_Splat7, i.uv1).rgb * col_control3.r;
                albedo += tex2D(_Splat8, i.uv1).rgb * col_control3.g;
                albedo += tex2D(_Splat9, i.uv1).rgb * col_control3.b;

                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * albedo;

                float3 worldNormal = normalize(i.normal).xyz;
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 diffuse = _LightColor0.rgb * albedo * dot(worldNormal, lightDir);
                return fixed4(ambient + diffuse, 1);
            }
            ENDCG
            
        }
    }
    FallBack "Diffuse"
}
