/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

Shader "Hidden/DepthCopy"
{
    SubShader
	{

		Pass
		{
			//Name "Pass 1" // combine the mask's depth from previous pass with the envDepthTexture
            ColorMask RGBA
            ZWrite Off

            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            Texture2DArray_float _EnvironmentDepthTexture;
            SamplerState sampler_EnvironmentDepthTexture;

            struct Attributes
            {
                uint vertexId : SV_VertexID;
                uint instanceId : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                uint depthSlice : SV_RenderTargetArrayIndex;
            };

            Varyings vert(const Attributes input)
            {
                Varyings output;

                // see GetFullScreenTriangleTexCoord() and GetFullScreenTriangleVertexPosition() in Common.hlsl
                const uint vertexID = input.vertexId;
                const float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                output.uv = float2(uv.x, 1.0 - uv.y);
                output.positionCS = float4(uv * 2.0 - 1.0, 1.0, 1.0);

                output.depthSlice = input.instanceId;
                return output;
            }

            float4 frag(const Varyings input) : SV_Target
            {
                float3 uv = float3(input.uv, input.depthSlice);
                //const float maskDepth = _MaskTexture.Sample(sampler_MaskTexture, uv);
                const float envDepth = _EnvironmentDepthTexture.Sample(sampler_EnvironmentDepthTexture, uv);
                //const float depth = maskDepth < envDepth ? 1.0f : envDepth;
                return float4(envDepth, envDepth, envDepth, 1);
            }
            ENDCG
		}
	}
}
