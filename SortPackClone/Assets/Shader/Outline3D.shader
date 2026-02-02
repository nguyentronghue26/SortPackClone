Shader "Custom/Outline3D"
{
    Properties
    {
        _Color ("Outline Color", Color) = (1,1,1,1)
        _Thickness ("Outline Thickness", Float) = 0.03
        
        // Điều chỉnh độ dày theo hướng
        _ThicknessX ("Thickness X", Range(0, 2)) = 0.5
        _ThicknessY ("Thickness Y", Range(0, 2)) = 0.3
        _ThicknessZ ("Thickness Z", Range(0, 2)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Cull Front
        ZWrite On
        ZTest LEqual
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            fixed4 _Color;
            float _Thickness;
            float _ThicknessX;
            float _ThicknessY;
            float _ThicknessZ;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            v2f vert (appdata v)
            {
                v2f o;
                
                float3 normal = normalize(v.normal);
                
                float3 scaledNormal = float3(
                    normal.x * _ThicknessX,
                    normal.y * _ThicknessY,
                    normal.z * _ThicknessZ
                );
                
                scaledNormal = normalize(scaledNormal) * _Thickness;
                
                float4 newVertex = v.vertex;
                newVertex.xyz += scaledNormal;
                
                o.pos = UnityObjectToClipPos(newVertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
    FallBack Off
}