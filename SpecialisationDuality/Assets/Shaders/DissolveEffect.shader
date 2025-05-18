Shader "Custom/DissolveEffect"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        
        // Dissolve properties
        _Dissolve ("Dissolve", Range(0,1)) = 0.0
        _DissolveTexture ("Dissolve Texture", 2D) = "white" {}
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        _EdgeWidth ("Edge Width", Range(0,0.1)) = 0.01
        
        // Add this property to control whether to use original color
        [Toggle] _PreserveOriginalColor ("Preserve Original Color", Float) = 1
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _DissolveTexture;
        
        struct Input
        {
            float2 uv_MainTex;
            float2 uv_DissolveTexture;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        half _Dissolve;
        fixed4 _EdgeColor;
        half _EdgeWidth;
        float _PreserveOriginalColor;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Sample the dissolve texture
            half dissolveValue = tex2D(_DissolveTexture, IN.uv_DissolveTexture).r;
            
            // Apply dissolve effect
            clip(dissolveValue - _Dissolve);
            
            // Calculate edge effect
            half edge = 1 - smoothstep(_Dissolve, _Dissolve + _EdgeWidth, dissolveValue);
            
            // Sample the main texture and apply color
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            
            // Only apply the shader's color if not preserving original
            if (_PreserveOriginalColor < 0.5) {
                c *= _Color;
            }
            
            // Apply edge color only to the edge, preserving original elsewhere
            o.Albedo = lerp(c.rgb, _EdgeColor.rgb, edge);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}