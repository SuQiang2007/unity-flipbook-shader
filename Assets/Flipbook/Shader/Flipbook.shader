Shader "Sprites/LayeredSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ViewDepthOffset ("Depth Offset", Float) = 0
        _TopSortingOrder ("Top Sorting Order", Float) = -9999
        _BottomSortingOrder ("Bottom Sorting Order", Float) = 1111
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        //一些系统默认属性
        //SpriteRenderer.Color while Non-Batched/Instanced
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _SplitCenterWorld ("Split Center World", Vector) = (0,0,0,1)
        [HideInInspector] _SplitHalf ("Split Half", Float) = 0
    }
    
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #include "UnitySprites.cginc"
            #pragma vertex SpriteVertDepth
            #pragma fragment SpriteFragDepth
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON

            struct v2fDepth
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float side : TEXCOORD1;
                float2 pushedClipZW : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float _ViewDepthOffset;
            float4 _SplitCenterWorld;
            float _SplitHalf;

            // 屏幕位置保持真实深度；另存一份推远后的 clip 深度，以及相对图片中点的上下位置。
            v2fDepth SpriteVertDepth(appdata_t IN)
            {
                v2fDepth OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                v2f baseOut = SpriteVert(IN);
                OUT.vertex = baseOut.vertex;
                OUT.color = baseOut.color;
                OUT.texcoord = baseOut.texcoord;

                float4 flipped = UnityFlipSprite(IN.vertex, _Flip);
                //按顺序获取坐标（本地空间-世界空间-视图空间-裁剪空间-NDC空间-屏幕空间）
                //获取世界空间坐标
                float3 worldPos = mul(unity_ObjectToWorld, float4(flipped.xyz, 1)).xyz;
                float3 up = UnityObjectToWorldDir(float3(0, _Flip.y, 0));
                OUT.side = dot(worldPos - _SplitCenterWorld.xyz, up);

                //获取视图空间坐标
                float offsetZ = UnityObjectToViewPos(flipped.xyz).z - _ViewDepthOffset;
                float nearZ = - _ProjectionParams.y;
                float farZ = - _ProjectionParams.z;
                offsetZ = clamp(offsetZ, nearZ, farZ);
                //获取裁剪空间坐标（虚拟坐标）
                float4 pushTo = mul(UNITY_MATRIX_P, float4(0,0,offsetZ,1));
                //获取NDC空间坐标
                float3 selfNDC = float3(pushTo.xyz / pushTo.w);
                //再用OUT中的参数转回裁剪空间
                OUT.pushedClipZW = float2(selfNDC.z * OUT.vertex.w, OUT.vertex.w);

                return OUT;
            }

            fixed4 SpriteFragDepth(v2fDepth IN, out float depth : SV_Depth) : SV_Target
            {
                if(_SplitHalf > 0)//外界传入1，说明当前shader位于上半SpriteRenderer
                {
                    //裁掉下半部分——当side>0时该shader的图像高于中心
                    clip(IN.side + 1e-4);
                }
                else if(_SplitHalf < 0)
                {
                    clip(-IN.side - 1e-5);
                }

                depth = IN.vertex.z;
                if(IN.side > 0 && _ViewDepthOffset != 0)
                {
                    float push = IN.pushedClipZW.x / IN.pushedClipZW.y;
                    #if !defined(UNITY_REVERSED_Z)
                        push = push * 0.5 + 0.5;//原本这个值是-1到1，这种情况下要0到1，所以用这种方式转换一下，环境不同
                    #endif
                    depth = push;
                }

                fixed4 color = SampleSpriteTexture(IN.texcoord) * IN.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}