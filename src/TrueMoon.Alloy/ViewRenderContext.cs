// using System.Numerics;
// using System.Text;
// using SkiaSharp;
// using Topten.RichTextKit;
// using Veldrid;
// using Veldrid.SPIRV;
//
// namespace TrueMoon.Alloy;
//
// public class ViewRenderContext : IDisposable
// {
//     public void CreateResources(GraphicsDevice graphicsDevice)
//     {
//         var factory = graphicsDevice.ResourceFactory;
//
//         var w = (int)graphicsDevice.SwapchainFramebuffer.Width;
//         var h = (int)graphicsDevice.SwapchainFramebuffer.Height;
//         
//         _img = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
//         _canvas = new SKCanvas(_img);
//         _canvas.Clear(SKColors.Transparent);
//         using var skPaint = new SKPaint
//         {
//             Color = SKColors.White,
//             Style = SKPaintStyle.StrokeAndFill,
//             TextSize = 20,
//             IsAntialias = true
//         };
//         _canvas.DrawText("OLOLOLOL Test Text2",50, 50, skPaint);
//         _canvas.Flush();  
//         //canvas.Dispose();
//         
//         _texData = new TextureData(PixelFormat.R8_G8_B8_A8_UNorm, TextureType.Texture2D, (uint)w, (uint)h, 1,1, 1);
//         
//         VertexPositionColor[] quadVertices =
//         {
//             // Front                                                              
//             new (new Vector2(-1.0f, +1.0f), new Vector2(0, 0), RgbaFloat.Red),
//             new (new Vector2(+1.0f, +1.0f), new Vector2(1, 0), RgbaFloat.Red),
//             new (new Vector2(+1.0f, -1.0f), new Vector2(1, 1), RgbaFloat.Red),
//             new (new Vector2(-1.0f, -1.0f), new Vector2(0, 1), RgbaFloat.Red),
//         };        
//
//         var vbDescription = new BufferDescription
//         (
//             (uint)(quadVertices.Length * VertexPositionColor.SizeInBytes),
//             BufferUsage.VertexBuffer
//         );
//         _vertexBuffer = factory.CreateBuffer(vbDescription);
//         graphicsDevice.UpdateBuffer(_vertexBuffer, 0, quadVertices);
//
//         ushort[] quadIndices = {0,1,2, 0,2,3,};
//         //ushort[] quadIndices = {0,1,2, 3,4,5,};
//         var ibDescription = new BufferDescription
//         (
//             (uint)(quadIndices.Length * sizeof(ushort)),
//             BufferUsage.IndexBuffer
//         );
//         _indexBuffer = factory.CreateBuffer(ibDescription);
//         graphicsDevice.UpdateBuffer(_indexBuffer, 0, quadIndices);
//
//         _texture = _texData.CreateDeviceTexture(graphicsDevice, factory, TextureUsage.Sampled, _img.GetPixelSpan());
//         _textureView = factory.CreateTextureView(_texture);
//
//         var vertexLayout = new VertexLayoutDescription
//         (
//             new VertexElementDescription
//                 ("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
//             new VertexElementDescription
//                 ("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
//             new VertexElementDescription
//                 ("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4)
//         );
//
//         var vertexShaderDesc = new ShaderDescription
//         (
//             ShaderStages.Vertex,
//             Encoding.UTF8.GetBytes(VertexCode),
//             "main"
//         );
//         var fragmentShaderDesc = new ShaderDescription
//         (
//             ShaderStages.Fragment,
//             Encoding.UTF8.GetBytes(FragmentCode),
//             "main"
//         );
//         
//         _shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
//
//         ResourceLayout textureLayout = factory.CreateResourceLayout(
//             new ResourceLayoutDescription(
//                 new ResourceLayoutElementDescription("SurfaceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
//                 new ResourceLayoutElementDescription("SurfaceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));
//         
//         // Create pipeline
//         var pipelineDescription = new GraphicsPipelineDescription
//         {
//             BlendState = BlendStateDescription.SingleAdditiveBlend,
//             DepthStencilState = new
//             (
//                 true,
//                 true,
//                 ComparisonKind.LessEqual
//             ),
//             RasterizerState = new
//             (
//                 FaceCullMode.Back,
//                 PolygonFillMode.Solid,
//                 FrontFace.Clockwise,
//                 true,
//                 false
//             ),
//             PrimitiveTopology = PrimitiveTopology.TriangleList,
//             ShaderSet = new
//             (
//                 new[] {vertexLayout},
//                 _shaders
//             ),
//             ResourceLayouts = new[] { textureLayout },
//             Outputs = graphicsDevice.SwapchainFramebuffer.OutputDescription
//         };
//
//         _pipeline = factory.CreateGraphicsPipeline(pipelineDescription);
//         
//         _textureSet = factory.CreateResourceSet(new ResourceSetDescription(
//             textureLayout,
//             _textureView,
//             graphicsDevice.Aniso4xSampler));
//     }
//
//     public void Render(CommandList commandList, GraphicsDevice graphicsDevice)
//     {
//         // Set all relevant state to draw our quad.
//         commandList.SetPipeline(_pipeline);
//         UpdateTexture(graphicsDevice);
//         commandList.SetVertexBuffer(0, _vertexBuffer);
//         commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
//         commandList.SetGraphicsResourceSet(0, _textureSet);
//         // Issue a Draw command for a single instance with 4 indices.
//         commandList.DrawIndexed(6, 1, 0, 0, 0);
//     }
//
//     private SKPaint skPaint = new SKPaint
//     {
//         Color = SKColors.White,
//         Style = SKPaintStyle.StrokeAndFill,
//         TextSize = 20,
//         IsAntialias = true
//     };
//     
//     private Style style = new Style
//     {
//         FontFamily = "Arial", 
//         FontSize = 14,
//         FontWeight = 400,
//         FontItalic = false,
//         TextColor = SKColors.White
//     };
//     
//     private void UpdateTexture(GraphicsDevice graphicsDevice)
//     {
//         //var canvas = new SKCanvas(_img);
//         _canvas.Clear(SKColors.Transparent);
//
//         //_canvas.DrawText($"Other Text: {DateTime.Now}",200, 200, skPaint);
//         //_canvas.DrawText($"Bottom right",graphicsDevice.SwapchainFramebuffer.Width - 50, graphicsDevice.SwapchainFramebuffer.Height - 50, skPaint);
//         //_canvas.DrawCircle(100, 100, 100, skPaint);
//         
//         var tb = new TextBlock();
//         
//         tb.AddText($"Other Text: {DateTime.Now}", style);
//         
//         tb.Paint(_canvas, new SKPoint(200, 200));
//         
//         _canvas.Flush();  
//         //_canvas.Dispose();
//         
//         _texData.UpdateDeviceTexture(graphicsDevice, _texture, _img.GetPixelSpan());
//     }
//     
//     public void Dispose()
//     {
//         ClearResources();
//     }
//
//     private void ClearResources()
//     {
//         _pipeline?.Dispose();
//         if (_shaders != null)
//         {
//             foreach (var shader in _shaders)
//             {
//                 shader.Dispose();
//             }
//         }
//         
//         _vertexBuffer?.Dispose();
//         _indexBuffer?.Dispose();
//         _textureSet?.Dispose();
//         _texture?.Dispose();
//         _canvas?.Dispose();
//         _img?.Dispose();
//     }
//
//
//     struct VertexPositionColor
//     {
//         public const uint SizeInBytes = 32;
//         public Vector2 Position;
//         public Vector2 UV;
//         public RgbaFloat Color;
//
//         public VertexPositionColor(Vector2 position, Vector2 uv, RgbaFloat color)
//         {
//             Position = position;
//             UV = uv;
//             Color = color;
//         }
//     }
//     
//     private DeviceBuffer? _vertexBuffer;
//     private DeviceBuffer? _indexBuffer;
//     private Shader[]? _shaders;
//     private Pipeline? _pipeline;
//     private TextureData _texData;
//     private Texture _texture;
//     private TextureView _textureView;
//     private ResourceSet _textureSet;
//     private SKBitmap _img;
//     private SKCanvas _canvas;
//
//     private const string VertexCode = @"
//     #version 450
//     layout(location = 0) in vec2 Position;
//     layout(location = 1) in vec2 TexCoords;
//     layout(location = 2) in vec4 Color;
//     layout(location = 0) out vec2 fsin_TexCoords;
//     layout(location = 1) out vec4 fsin_Color;
//     void main()
//     {
//         gl_Position = vec4(Position, 0, 1);
//         fsin_TexCoords = TexCoords;
//         fsin_Color = Color;
//     }";
//
//     private const string FragmentCode = @"
// #version 450
// layout(location = 0) in vec2 fsin_texCoords;
// layout(location = 1) in vec4 fsin_Color;
// layout(location = 0) out vec4 fsout_color;
// layout(set = 0, binding = 0) uniform texture2D SurfaceTexture;
// layout(set = 0, binding = 1) uniform sampler SurfaceSampler;
// void main()
// {
// fsout_color =  texture(sampler2D(SurfaceTexture, SurfaceSampler), fsin_texCoords);
// }";
//
//     public void Resize(GraphicsDevice graphicsDevice)
//     {
//         ClearResources();
//         CreateResources(graphicsDevice);
//     }
// }