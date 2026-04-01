using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

using Dalamud.Bindings.ImGui;
using Dalamud.Logging.Internal;

using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

using static TerraFX.Interop.Windows.Windows;

namespace Dalamud.Interface.ImGuiBackend.Renderers;

/// <summary>
/// Deals with rendering ImGui using DirectX 11.
/// See https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_dx11.cpp for the original implementation.
/// </summary>
internal unsafe partial class Dx11Renderer
{
    private class BlurHandler : IDisposable
    {
        private static readonly ModuleLog Log = ModuleLog.Create<BlurHandler>();

        private readonly Dx11Renderer renderer;
        private readonly IDXGISwapChain* swapChain;
        private readonly bool initialized;

        private readonly D3D11_VIEWPORT* oldViewports;

        private ID3D11VertexShader* vs;
        private ID3D11PixelShader* ps;

        private ID3D11VertexShader* vsDown;
        private ID3D11PixelShader* psDown;

        private ID3D11VertexShader* vsUp;
        private ID3D11PixelShader* psUp;

        private ID3D11VertexShader* vsDraw;
        private ID3D11PixelShader* psDraw;

        private ID3D11Texture2D* rtBackBufferCopy;
        private ID3D11ShaderResourceView* srvBackBufferCopy;

        private ID3D11RenderTargetView* rtvBackBuffer;

        private ID3D11Texture2D* rtFull;
        private ID3D11RenderTargetView* rtvFull;
        private ID3D11ShaderResourceView* srvFull;

        private ID3D11Texture2D* rtHalf;
        private ID3D11RenderTargetView* rtvHalf;
        private ID3D11ShaderResourceView* srvHalf;

        private ID3D11Texture2D* rtQuarter;
        private ID3D11RenderTargetView* rtvQuarter;
        private ID3D11ShaderResourceView* srvQuarter;

        private ID3D11Texture2D* rtEights;
        private ID3D11RenderTargetView* rtvEights;
        private ID3D11ShaderResourceView* srvEights;

        private ID3D11Buffer* constantBuffer;
        private ID3D11SamplerState* sampler;
        private ID3D11BlendState* blendState;
        private ID3D11RasterizerState* rasterState;

        private D3D11_TEXTURE2D_DESC bbDesc;

        private float blurStrength = 2.0f;

        public BlurHandler(Dx11Renderer renderer, IDXGISwapChain* swapChain)
        {
            this.renderer = renderer;
            this.swapChain = swapChain;
            this.oldViewports = (D3D11_VIEWPORT*)Marshal.AllocHGlobal(sizeof(D3D11_VIEWPORT) * D3D11.D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE);
            this.initialized = this.Initialize();
        }

        public void Dispose()
        {
            this.ReleaseRenderTargets();

            Release(ref this.psDraw);
            Release(ref this.vsDraw);

            Release(ref this.psDown);
            Release(ref this.vsDown);

            Release(ref this.psUp);
            Release(ref this.vsUp);

            Release(ref this.ps);
            Release(ref this.vs);

            Release(ref this.rtBackBufferCopy);
            Release(ref this.srvBackBufferCopy);

            Release(ref this.constantBuffer);
            Release(ref this.sampler);
            Release(ref this.blendState);
            Release(ref this.rasterState);

            if (this.oldViewports != null)
                Marshal.FreeHGlobal((nint)this.oldViewports);
        }

        public void BlurCallback(in ImDrawListPtr parentList, in ImDrawCmd cmd)
        {
            if (!this.initialized)
                return;

            if (cmd.UserCallbackData == null)
                return;

            if (!this.UpdateBackBuffer())
                return;

            if (!this.UpdateRenderTargets())
                return;

            if (this.vs == null || this.ps == null || this.constantBuffer == null)
                return;

            var context = this.renderer.context.Get();

            var blurParams = *(BlurCallbackParams*)cmd.UserCallbackData;
            this.blurStrength = blurParams.Strength;

            // Capture current state
            ID3D11VertexShader* oldVS = null;
            context->VSGetShader(&oldVS, null, null);

            ID3D11PixelShader* oldPS = null;
            context->PSGetShader(&oldPS, null, null);

            ID3D11ShaderResourceView* oldSRV = null;
            context->PSGetShaderResources(0, 1, &oldSRV);

            ID3D11SamplerState* oldSampler = null;
            context->PSGetSamplers(0, 1, &oldSampler);

            ID3D11Buffer* oldVSCB = null;
            context->VSGetConstantBuffers(0, 1, &oldVSCB);

            ID3D11Buffer* oldPSCB = null;
            context->PSGetConstantBuffers(0, 1, &oldPSCB);

            uint oldNumViewports = 0;
            context->RSGetViewports(&oldNumViewports, null);

            if (oldNumViewports > 0)
                context->RSGetViewports(&oldNumViewports, this.oldViewports);

            ID3D11RenderTargetView* oldRTV = null;
            ID3D11DepthStencilView* oldDSV = null;
            context->OMGetRenderTargets(1, &oldRTV, &oldDSV);

            var size = new Vector2(this.bbDesc.Width, this.bbDesc.Height);

            // Copy Backbuffer to Full RT
            this.RenderPass(this.vs, this.ps, this.srvBackBufferCopy, this.rtvFull, size);

            // Downsample
            this.RenderPass(this.vsDown, this.psDown, this.srvFull, this.rtvHalf, size / 2);
            this.RenderPass(this.vsDown, this.psDown, this.srvHalf, this.rtvQuarter, size / 4);
            this.RenderPass(this.vsDown, this.psDown, this.srvQuarter, this.rtvEights, size / 8);

            // Upsample
            this.RenderPass(this.vsUp, this.psUp, this.srvEights, this.rtvQuarter, size / 4);
            this.RenderPass(this.vsUp, this.psUp, this.srvQuarter, this.rtvHalf, size / 2);
            this.RenderPass(this.vsUp, this.psUp, this.srvHalf, this.rtvFull, size);

            // Draw result
            this.DrawPass(blurParams);

            // Restore state
            context->OMSetRenderTargets(1, &oldRTV, oldDSV);
            if (oldNumViewports != 0)
                context->RSSetViewports(oldNumViewports, this.oldViewports);
            context->PSSetConstantBuffers(0, 1, &oldPSCB);
            context->VSSetConstantBuffers(0, 1, &oldVSCB);
            context->PSSetSamplers(0, 1, &oldSampler);
            context->PSSetShaderResources(0, 1, &oldSRV);
            context->PSSetShader(oldPS, null, 0);
            context->VSSetShader(oldVS, null, 0);

            Release(ref oldVS);
            Release(ref oldPS);
            Release(ref oldSRV);
            Release(ref oldSampler);
            Release(ref oldVSCB);
            Release(ref oldPSCB);
            Release(ref oldRTV);
            Release(ref oldDSV);
        }

        private static void Release<T>(ref T* obj) where T : unmanaged
        {
            if (obj != null)
            {
                ((IUnknown*)obj)->Release();
                obj = null;
            }
        }

        private static bool CompileShader(byte[] shaderBytes, ReadOnlySpan<byte> entrypoint, ReadOnlySpan<byte> target, ID3DBlob** ppCode)
        {
            ID3DBlob* pErrorMsgs = null;
#if DEBUG
            uint flags = D3DCOMPILE.D3DCOMPILE_DEBUG;
#else
        uint flags = 0;
#endif

            fixed (byte* pSrcData = shaderBytes)
            fixed (byte* pEntrypoint = entrypoint)
            fixed (byte* pTarget = target)
            {
                var hr = DirectX.D3DCompile(
                    pSrcData, (nuint)shaderBytes.Length,
                    null, null, null,
                    (sbyte*)pEntrypoint, (sbyte*)pTarget,
                    flags, 0, ppCode, &pErrorMsgs);

                if (hr.FAILED)
                {
                    if (pErrorMsgs != null)
                    {
                        var msg = Marshal.PtrToStringAnsi((nint)pErrorMsgs->GetBufferPointer());
                        pErrorMsgs->Release();
                        Log.Error($"Shader compilaton failed: {msg}");
                        return false;
                    }

                    Log.Error("Shader compilaton failed");
                    return false;
                }
            }

            return true;
        }

        private bool Initialize()
        {
            var device = this.renderer.device.Get();

            Guid iid = __uuidof<ID3D11Device>();

            // Create Sampler
            D3D11_SAMPLER_DESC sDesc = new()
            {
                Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
                AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            };

            fixed (ID3D11SamplerState** pSampler = &this.sampler)
            {
                var hr = device->CreateSamplerState(&sDesc, pSampler);
                if (hr.FAILED)
                {
                    Log.Error("CreateSamplerState failed");
                    return false;
                }
            }

            // Create Constant Buffer
            D3D11_BUFFER_DESC bDesc = new()
            {
                ByteWidth = (uint)sizeof(ShaderParams),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = (uint)D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
            };

            fixed (ID3D11Buffer** pConstantBuffer = &this.constantBuffer)
            {
                var hr = device->CreateBuffer(&bDesc, null, pConstantBuffer);
                if (hr.FAILED)
                {
                    Log.Error("CreateBuffer failed");
                    return false;
                }
            }

            // Create Blend State
            D3D11_BLEND_DESC blendDesc = default;
            blendDesc.RenderTarget[0].BlendEnable = false; // Force opaque
            blendDesc.RenderTarget[0].RenderTargetWriteMask = (byte)D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_ALL;

            fixed (ID3D11BlendState** pBlend = &this.blendState)
                device->CreateBlendState(&blendDesc, pBlend);

            // Create Rasterizer State
            D3D11_RASTERIZER_DESC rasterDesc = default;
            rasterDesc.CullMode = D3D11_CULL_MODE.D3D11_CULL_NONE;
            rasterDesc.FillMode = D3D11_FILL_MODE.D3D11_FILL_SOLID;

            fixed (ID3D11RasterizerState** pRaster = &this.rasterState)
                device->CreateRasterizerState(&rasterDesc, pRaster);

            // Create Shaders
            using var stream = typeof(Dx11Renderer).Assembly.GetManifestResourceStream("BlurShader.hlsl");
            if (stream == null)
            {
                Log.Error("BlurShader.hlsl not found");
                return false;
            }

            using var reader = new StreamReader(stream);
            var shaderCode = reader.ReadToEnd();
            var shaderBytes = Encoding.ASCII.GetBytes(shaderCode);

            // Compile Copy Vertex Shader and Pixel Shader
            ID3DBlob* vsBlob = null;
            if (CompileShader(shaderBytes, "Vert"u8, "vs_5_0"u8, &vsBlob))
            {
                fixed (ID3D11VertexShader** pVS = &this.vs)
                {
                    var hr = device->CreateVertexShader(vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(), null, pVS);
                    if (hr.FAILED)
                    {
                        Log.Error("Vert CreateVertexShader failed");
                        return false;
                    }
                }

                vsBlob->Release();
            }

            ID3DBlob* psBlob = null;
            if (CompileShader(shaderBytes, "Frag"u8, "ps_5_0"u8, &psBlob))
            {
                fixed (ID3D11PixelShader** pPS = &this.ps)
                {
                    var hr = device->CreatePixelShader(psBlob->GetBufferPointer(), psBlob->GetBufferSize(), null, pPS);
                    if (hr.FAILED)
                    {
                        Log.Error("Frag CreatePixelShader failed");
                        return false;
                    }
                }

                psBlob->Release();
            }

            // Compile Downsample Vertex and Pixel Shader
            ID3DBlob* vsDownBlob = null;
            if (CompileShader(shaderBytes, "Vert_DownSample"u8, "vs_5_0"u8, &vsDownBlob))
            {
                fixed (ID3D11VertexShader** pVS = &this.vsDown)
                {
                    var hr = device->CreateVertexShader(vsDownBlob->GetBufferPointer(), vsDownBlob->GetBufferSize(), null, pVS);
                    if (hr.FAILED)
                    {
                        Log.Error("Vert_DownSample CreateVertexShader failed");
                        return false;
                    }
                }

                vsDownBlob->Release();
            }

            ID3DBlob* psDownBlob = null;
            if (CompileShader(shaderBytes, "Frag_DownSample"u8, "ps_5_0"u8, &psDownBlob))
            {
                fixed (ID3D11PixelShader** pPS = &this.psDown)
                {
                    var hr = device->CreatePixelShader(psDownBlob->GetBufferPointer(), psDownBlob->GetBufferSize(), null, pPS);
                    if (hr.FAILED)
                    {
                        Log.Error("Frag_DownSample CreatePixelShader failed");
                        return false;
                    }
                }

                psDownBlob->Release();
            }

            // Compile Upsample Vertex and Pixel Shader
            ID3DBlob* vsUpBlob = null;
            if (CompileShader(shaderBytes, "Vert_UpSample"u8, "vs_5_0"u8, &vsUpBlob))
            {
                fixed (ID3D11VertexShader** pVS = &this.vsUp)
                {
                    var hr = device->CreateVertexShader(vsUpBlob->GetBufferPointer(), vsUpBlob->GetBufferSize(), null, pVS);
                    if (hr.FAILED)
                    {
                        Log.Error("Vert_UpSample CreateVertexShader failed");
                        return false;
                    }
                }

                vsUpBlob->Release();
            }

            ID3DBlob* psUpBlob = null;
            if (CompileShader(shaderBytes, "Frag_UpSample"u8, "ps_5_0"u8, &psUpBlob))
            {
                fixed (ID3D11PixelShader** pPS = &this.psUp)
                {
                    var hr = device->CreatePixelShader(psUpBlob->GetBufferPointer(), psUpBlob->GetBufferSize(), null, pPS);
                    if (hr.FAILED)
                    {
                        Log.Error("Frag_UpSample CreatePixelShader failed");
                        return false;
                    }
                }

                psUpBlob->Release();
            }

            // Compile Upsample Vertex and Pixel Shader
            ID3DBlob* vsDrawBlob = null;
            if (CompileShader(shaderBytes, "Vert_Draw"u8, "vs_5_0"u8, &vsDrawBlob))
            {
                fixed (ID3D11VertexShader** pVS = &this.vsDraw)
                {
                    var hr = device->CreateVertexShader(vsDrawBlob->GetBufferPointer(), vsDrawBlob->GetBufferSize(), null, pVS);
                    if (hr.FAILED)
                    {
                        Log.Error("Vert_Draw CreateVertexShader failed");
                        return false;
                    }
                }

                vsDrawBlob->Release();
            }

            ID3DBlob* psDrawBlob = null;
            if (CompileShader(shaderBytes, "Frag_UpSample"u8, "ps_5_0"u8, &psDrawBlob))
            {
                fixed (ID3D11PixelShader** pPS = &this.psDraw)
                {
                    var hr = device->CreatePixelShader(psDrawBlob->GetBufferPointer(), psDrawBlob->GetBufferSize(), null, pPS);
                    if (hr.FAILED)
                    {
                        Log.Error("Frag_UpSample CreatePixelShader failed");
                        return false;
                    }
                }

                psDrawBlob->Release();
            }

            return
               this.vs != null && this.ps != null &&
               this.vsDown != null && this.psDown != null &&
               this.vsUp != null && this.psUp != null &&
               this.vsDraw != null && this.psDraw != null;
        }

        private bool UpdateBackBuffer()
        {
            ID3D11Texture2D* backBuffer = null;
            Guid iid = __uuidof<ID3D11Texture2D>();
            this.swapChain->GetBuffer(0, &iid, (void**)&backBuffer);
            if (backBuffer == null)
            {
                Log.Error("No backbuffer");
                return false;
            }

            D3D11_TEXTURE2D_DESC bbDesc;
            backBuffer->GetDesc(&bbDesc);
            this.bbDesc = bbDesc;

            if (this.rtBackBufferCopy != null)
            {
                D3D11_TEXTURE2D_DESC copyDesc;
                this.rtBackBufferCopy->GetDesc(&copyDesc);
                if (copyDesc.Width != this.bbDesc.Width || copyDesc.Height != this.bbDesc.Height)
                {
                    Release(ref this.rtBackBufferCopy);
                    Release(ref this.srvBackBufferCopy);
                }
            }

            if (this.rtBackBufferCopy == null)
            {
                var device = this.renderer.device.Get();

                this.bbDesc.BindFlags = (uint)D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE;
                this.bbDesc.Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT;
                this.bbDesc.CPUAccessFlags = 0;

                fixed (ID3D11Texture2D** p = &this.rtBackBufferCopy)
                {
                    var hr = device->CreateTexture2D(&bbDesc, null, p);
                    if (hr.FAILED)
                    {
                        Log.Error("BackBufferCopy CreateTexture2D failed");
                        return false;
                    }
                }

                fixed (ID3D11ShaderResourceView** p = &this.srvBackBufferCopy)
                {
                    var hr = device->CreateShaderResourceView((ID3D11Resource*)this.rtBackBufferCopy, null, p);
                    if (hr.FAILED)
                    {
                        Log.Error("BackBufferCopy CreateShaderResourceView failed");
                        return false;
                    }
                }

                // Original BackBuffer RenderTargetView
                fixed (ID3D11RenderTargetView** pRenderTargetView = &this.rtvBackBuffer)
                {
                    var hr = device->CreateRenderTargetView((ID3D11Resource*)backBuffer, null, pRenderTargetView);
                    if (hr.FAILED)
                    {
                        Log.Error("BackBuffer CreateRenderTargetView failed");
                        return false;
                    }
                }
            }

            this.renderer.context.Get()->CopyResource((ID3D11Resource*)this.rtBackBufferCopy, (ID3D11Resource*)backBuffer);

            backBuffer->Release();
            return true;
        }

        private bool UpdateRenderTargets()
        {
            var device = this.renderer.device.Get();
            var bbDesc = this.bbDesc;

            // Check if update is needed
            if (this.rtFull != null)
            {
                D3D11_TEXTURE2D_DESC desc;
                this.rtFull->GetDesc(&desc);
                if (desc.Width == bbDesc.Width && desc.Height == bbDesc.Height)
                    return true;
            }

            this.ReleaseRenderTargets();

            D3D11_TEXTURE2D_DESC texDesc = new()
            {
                Width = bbDesc.Width,
                Height = bbDesc.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = bbDesc.Format,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = (uint)(D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE),
            };

            fixed (ID3D11Texture2D** pTexture = &this.rtFull)
            {
                var hr = device->CreateTexture2D(&texDesc, null, pTexture);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Full CreateTexture2D failed");
                    return false;
                }
            }

            fixed (ID3D11RenderTargetView** pRenderTargetView = &this.rtvFull)
            {
                var hr = device->CreateRenderTargetView((ID3D11Resource*)this.rtFull, null, pRenderTargetView);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Full CreateRenderTargetView failed");
                    return false;
                }
            }

            fixed (ID3D11ShaderResourceView** pShaderResourceView = &this.srvFull)
            {
                var hr = device->CreateShaderResourceView((ID3D11Resource*)this.rtFull, null, pShaderResourceView);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Full CreateShaderResourceView failed");
                    return false;
                }
            }

            texDesc.Width = bbDesc.Width / 2;
            texDesc.Height = bbDesc.Height / 2;

            fixed (ID3D11Texture2D** pTexture = &this.rtHalf)
            {
                var hr = device->CreateTexture2D(&texDesc, null, pTexture);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Half CreateTexture2D failed");
                    return false;
                }
            }

            fixed (ID3D11RenderTargetView** pRenderTargetView = &this.rtvHalf)
            {
                var hr = device->CreateRenderTargetView((ID3D11Resource*)this.rtHalf, null, pRenderTargetView);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Half CreateRenderTargetView failed");
                    return false;
                }
            }

            fixed (ID3D11ShaderResourceView** pShaderResourceView = &this.srvHalf)
            {
                var hr = device->CreateShaderResourceView((ID3D11Resource*)this.rtHalf, null, pShaderResourceView);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Half CreateShaderResourceView failed");
                    return false;
                }
            }

            texDesc.Width = bbDesc.Width / 4;
            texDesc.Height = bbDesc.Height / 4;

            fixed (ID3D11Texture2D** pTexture = &this.rtQuarter)
            {
                var hr = device->CreateTexture2D(&texDesc, null, pTexture);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Quarter CreateTexture2D failed");
                    return false;
                }
            }

            fixed (ID3D11RenderTargetView** pRenderTargetView = &this.rtvQuarter)
            {
                var hr = device->CreateRenderTargetView((ID3D11Resource*)this.rtQuarter, null, pRenderTargetView);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Quarter CreateRenderTargetView failed");
                    return false;
                }
            }

            fixed (ID3D11ShaderResourceView** pShaderResourceView = &this.srvQuarter)
            {
                var hr = device->CreateShaderResourceView((ID3D11Resource*)this.rtQuarter, null, pShaderResourceView);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Quarter CreateShaderResourceView failed");
                    return false;
                }
            }

            texDesc.Width = bbDesc.Width / 8;
            texDesc.Height = bbDesc.Height / 8;

            fixed (ID3D11Texture2D** pTexture = &this.rtEights)
            {
                var hr = device->CreateTexture2D(&texDesc, null, pTexture);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Eights CreateTexture2D failed");
                    return false;
                }
            }

            fixed (ID3D11RenderTargetView** pRenderTargetView = &this.rtvEights)
            {
                var hr = device->CreateRenderTargetView((ID3D11Resource*)this.rtEights, null, pRenderTargetView);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Eights CreateRenderTargetView failed");
                    return false;
                }
            }

            fixed (ID3D11ShaderResourceView** pShaderResourceView = &this.srvEights)
            {
                var hr = device->CreateShaderResourceView((ID3D11Resource*)this.rtEights, null, pShaderResourceView);
                if (hr.FAILED)
                {
                    Log.Error("RenderTarget Eights CreateShaderResourceView failed");
                    return false;
                }
            }

            return true;
        }

        private void RenderPass(
            ID3D11VertexShader* pVertexShader,
            ID3D11PixelShader* pPixelShader,
            ID3D11ShaderResourceView* pShaderResourceView,
            ID3D11RenderTargetView* pRenderTargetView,
            Vector2 size)
        {
            var context = this.renderer.context.Get();

            var clearColor = stackalloc float[4] { 0, 0, 0, 0 };
            context->ClearRenderTargetView(pRenderTargetView, clearColor);

            D3D11_VIEWPORT vp = new()
            {
                Width = size.X,
                Height = size.Y,
                MaxDepth = 1.0f,
            };
            context->RSSetViewports(1, &vp);

            var parms = new ShaderParams
            {
                TexelSize = new Vector4(1f / size.X, 1f / size.Y, size.X, size.Y),
                BlurOffset = this.blurStrength,
            };
            context->UpdateSubresource((ID3D11Resource*)this.constantBuffer, 0, null, &parms, 0, 0);

            var cb = this.constantBuffer;
            context->VSSetConstantBuffers(0, 1, &cb);
            context->PSSetConstantBuffers(0, 1, &cb);

            var samp = this.sampler;
            context->PSSetSamplers(0, 1, &samp);

            context->VSSetShader(pVertexShader, null, 0);
            context->PSSetShader(pPixelShader, null, 0);
            context->PSSetShaderResources(0, 1, &pShaderResourceView); // input
            context->OMSetRenderTargets(1, &pRenderTargetView, null); // output
            context->Draw(3, 0);

            ID3D11RenderTargetView* nullRTV = null;
            context->OMSetRenderTargets(1, &nullRTV, null);

            ID3D11ShaderResourceView* nullSRV = null;
            context->PSSetShaderResources(0, 1, &nullSRV);
        }

        private void DrawPass(BlurCallbackParams blurParams)
        {
            var device = this.renderer.device.Get();
            var context = this.renderer.context.Get();

            // context->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

            D3D11_VIEWPORT vp = new()
            {
                Width = this.bbDesc.Width,
                Height = this.bbDesc.Height,
                MaxDepth = 1.0f,
            };
            context->RSSetViewports(1, &vp);

            var parms = new ShaderParams
            {
                TexelSize = new Vector4(1f / this.bbDesc.Width, 1f / this.bbDesc.Height, this.bbDesc.Width, this.bbDesc.Height),
            };
            context->UpdateSubresource((ID3D11Resource*)this.constantBuffer, 0, null, &parms, 0, 0);

            /*
            var rasterizerDesc = new D3D11_RASTERIZER_DESC
            {
                FillMode = D3D11_FILL_MODE.D3D11_FILL_SOLID,
                CullMode = D3D11_CULL_MODE.D3D11_CULL_NONE,
                ScissorEnable = true,
            };
            ID3D11RasterizerState* pRasterizerState = null;
            var hr = device->CreateRasterizerState(&rasterizerDesc, &pRasterizerState);
            if (hr.FAILED)
            {
                Log.Error("Draw CreateRasterizerState failed");
                return;
            }

            context->RSSetState(pRasterizerState);

            var rect = new RECT()
            {
                top = (int)blurParams.Position.Y,
                left = (int)blurParams.Position.X,
                right = (int)blurParams.Position.X + (int)blurParams.Size.X,
                bottom = (int)blurParams.Position.Y + (int)blurParams.Size.Y,
            };
            context->RSSetScissorRects(1, &rect);
            */
            var cb = this.constantBuffer;
            context->VSSetConstantBuffers(0, 1, &cb);
            context->PSSetConstantBuffers(0, 1, &cb);

            var samp = this.sampler;
            context->PSSetSamplers(0, 1, &samp);

            context->VSSetShader(this.vsDraw, null, 0);
            context->PSSetShader(this.psDraw, null, 0);

            var pShaderResourceView = this.srvFull;
            var pRenderTargetView = this.rtvBackBuffer;
            context->PSSetShaderResources(0, 1, &pShaderResourceView); // input
            context->OMSetRenderTargets(1, &pRenderTargetView, null); // output
            context->Draw(3, 0);

            ID3D11RenderTargetView* nullRTV = null;
            context->OMSetRenderTargets(1, &nullRTV, null);

            ID3D11ShaderResourceView* nullSRV = null;
            context->PSSetShaderResources(0, 1, &nullSRV);
            /*
            rect.top = 0;
            rect.left = 0;
            rect.right = (int)this.bbDesc.Width;
            rect.bottom = (int)this.bbDesc.Height;
            context->RSSetScissorRects(1, &rect);

            pRasterizerState->Release();*/
        }

        private void ReleaseRenderTargets()
        {
            Release(ref this.rtFull);
            Release(ref this.rtvFull);
            Release(ref this.srvFull);

            Release(ref this.rtHalf);
            Release(ref this.rtvHalf);
            Release(ref this.srvHalf);

            Release(ref this.rtQuarter);
            Release(ref this.rtvQuarter);
            Release(ref this.srvQuarter);

            Release(ref this.rtEights);
            Release(ref this.rtvEights);
            Release(ref this.srvEights);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        public struct ShaderParams
        {
            public Vector4 TexelSize; // x: 1/w, y: 1/h, z: w, w: h
            public float BlurOffset;
            public Vector3 Padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BlurCallbackParams
        {
            public Vector2 Position;
            public Vector2 Size;
            public float Strength;
        }
    }
}
