using System.Numerics;
using System.Text;
using SkiaSharp;
using Veldrid;
using Veldrid.SPIRV;

namespace TrueMoon.Alloy;

public class RenderObject : IDisposable
{
    public void CreateResources(GraphicsDevice graphicsDevice)
    {
        var factory = graphicsDevice.ResourceFactory;

        var w = 960;
        var h = 540;
        
        _img = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var canvas = new SKCanvas(_img);
        canvas.Clear(SKColors.Transparent);
        using var skPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.StrokeAndFill,
            TextSize = 20,
            IsAntialias = true
        };
        canvas.DrawText("OLOLOLOL Test Text2",50, 50, skPaint);
        canvas.Flush();  
        canvas.Dispose();
        //var bytes = _img.Bytes;
        
        var textureData = _img.Bytes;
        _texData = new ProcessedTexture(PixelFormat.R8_G8_B8_A8_UNorm, TextureType.Texture2D, (uint)w, (uint)h, 1,1, 1, textureData);
        
        VertexPositionColor[] quadVertices =
        {
            // Front                                                              
            new (new Vector2(-1.0f, +1.0f), new Vector2(0, 0), RgbaFloat.Red),
            new (new Vector2(+1.0f, +1.0f), new Vector2(1, 0), RgbaFloat.Red),
            new (new Vector2(+1.0f, -1.0f), new Vector2(1, 1), RgbaFloat.Red),
            new (new Vector2(-1.0f, -1.0f), new Vector2(0, 1), RgbaFloat.Red),
        };        
        // VertexPositionColor[] quadVertices =
        // {
        //     // Front                                                              
        //     new (new Vector2(-0.5f, +0.5f), new Vector2(0, 0), RgbaFloat.Red),
        //     new (new Vector2(+0.5f, +0.5f), new Vector2(1, 0), RgbaFloat.Red),
        //     new (new Vector2(+0.5f, -0.5f), new Vector2(1, 1), RgbaFloat.Red),
        //     
        //     new (new Vector2(-0.5f, +0.5f), new Vector2(0, 0), RgbaFloat.Red),
        //     new (new Vector2(+0.5f, -0.5f), new Vector2(1, 1), RgbaFloat.Red),
        //     new (new Vector2(-0.5f, -0.5f), new Vector2(0, 1), RgbaFloat.Red),
        // };
        var vbDescription = new BufferDescription
        (
            (uint)(quadVertices.Length * VertexPositionColor.SizeInBytes),
            BufferUsage.VertexBuffer
        );
        _vertexBuffer = factory.CreateBuffer(vbDescription);
        graphicsDevice.UpdateBuffer(_vertexBuffer, 0, quadVertices);

        ushort[] quadIndices = {0,1,2, 0,2,3,};
        //ushort[] quadIndices = {0,1,2, 3,4,5,};
        var ibDescription = new BufferDescription
        (
            (uint)(quadIndices.Length * sizeof(ushort)),
            BufferUsage.IndexBuffer
        );
        _indexBuffer = factory.CreateBuffer(ibDescription);
        graphicsDevice.UpdateBuffer(_indexBuffer, 0, quadIndices);

        _texture = _texData.CreateDeviceTexture(graphicsDevice, factory, TextureUsage.Sampled);
        _textureView = factory.CreateTextureView(_texture);

        var vertexLayout = new VertexLayoutDescription
        (
            new VertexElementDescription
                ("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
            new VertexElementDescription
                ("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
            new VertexElementDescription
                ("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4)
        );

        var vertexShaderDesc = new ShaderDescription
        (
            ShaderStages.Vertex,
            Encoding.UTF8.GetBytes(VertexCode),
            "main"
        );
        var fragmentShaderDesc = new ShaderDescription
        (
            ShaderStages.Fragment,
            Encoding.UTF8.GetBytes(FragmentCode),
            "main"
        );
        
        _shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);

        ResourceLayout textureLayout = factory.CreateResourceLayout(
            new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("SurfaceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("SurfaceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));
        
        // Create pipeline
        var pipelineDescription = new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleAdditiveBlend,
            DepthStencilState = new
            (
                true,
                true,
                ComparisonKind.LessEqual
            ),
            RasterizerState = new
            (
                FaceCullMode.Back,
                PolygonFillMode.Solid,
                FrontFace.Clockwise,
                true,
                false
            ),
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ShaderSet = new
            (
                new[] {vertexLayout},
                _shaders
            ),
            ResourceLayouts = new[] { textureLayout },
            Outputs = graphicsDevice.SwapchainFramebuffer.OutputDescription
        };

        _pipeline = factory.CreateGraphicsPipeline(pipelineDescription);
        
        _textureSet = factory.CreateResourceSet(new ResourceSetDescription(
            textureLayout,
            _textureView,
            graphicsDevice.Aniso4xSampler));
    }

    public void Render(CommandList commandList, GraphicsDevice graphicsDevice)
    {
        // Set all relevant state to draw our quad.
        commandList.SetPipeline(_pipeline);
        UpdateTexture(graphicsDevice);
        commandList.SetVertexBuffer(0, _vertexBuffer);
        commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        commandList.SetGraphicsResourceSet(0, _textureSet);
        // Issue a Draw command for a single instance with 4 indices.
        commandList.DrawIndexed(6, 1, 0, 0, 0);
    }

    private void UpdateTexture(GraphicsDevice graphicsDevice)
    {
        var canvas = new SKCanvas(_img);
        canvas.Clear(SKColors.Transparent);
        using var skPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.StrokeAndFill,
            TextSize = 20,
            IsAntialias = true
        };
        canvas.DrawText($"Other Text: {DateTime.Now}",200, 200, skPaint);
        canvas.Flush();  
        canvas.Dispose();
        
        _texData.UpdateDeviceTexture(graphicsDevice, _texture, _img.GetPixelSpan());
    }
    
    public void Dispose()
    {
        _pipeline?.Dispose();
        if (_shaders != null)
        {
            foreach (var shader in _shaders)
            {
                shader.Dispose();
            }
        }
        
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _textureSet?.Dispose();
        _texture?.Dispose();
        _img?.Dispose();
    }
    

    struct VertexPositionColor
    {
        public const uint SizeInBytes = 32;
        public Vector2 Position;
        public Vector2 UV;
        public RgbaFloat Color;

        public VertexPositionColor(Vector2 position, Vector2 uv, RgbaFloat color)
        {
            Position = position;
            UV = uv;
            Color = color;
        }
    }
    
    private DeviceBuffer? _vertexBuffer;
    private DeviceBuffer? _indexBuffer;
    private Shader[]? _shaders;
    private Pipeline? _pipeline;
    private ProcessedTexture _texData;
    private Texture _texture;
    private TextureView _textureView;
    private ResourceSet _textureSet;
    private SKBitmap _img;

    private const string VertexCode = @"
    #version 450
    layout(location = 0) in vec2 Position;
    layout(location = 1) in vec2 TexCoords;
    layout(location = 2) in vec4 Color;
    layout(location = 0) out vec2 fsin_TexCoords;
    layout(location = 1) out vec4 fsin_Color;
    void main()
    {
        gl_Position = vec4(Position, 0, 1);
        fsin_TexCoords = TexCoords;
        fsin_Color = Color;
    }";

    private const string FragmentCode = @"
#version 450
layout(location = 0) in vec2 fsin_texCoords;
layout(location = 1) in vec4 fsin_Color;
layout(location = 0) out vec4 fsout_color;
layout(set = 0, binding = 0) uniform texture2D SurfaceTexture;
layout(set = 0, binding = 1) uniform sampler SurfaceSampler;
void main()
{
fsout_color =  texture(sampler2D(SurfaceTexture, SurfaceSampler), fsin_texCoords);
}";

}