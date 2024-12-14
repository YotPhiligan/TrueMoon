using Silk.NET.OpenGL;

namespace TrueMoon.Alloy;

public class HelloWorldSample
{
    private GL _gl;

    private uint _vbo;
    private uint _ebo;
    private uint _vao;
    private uint _shader;

    //Vertex shaders are run on each vertex.
    private const string VertexShaderSource = @"
    #version 330 core //Using version GLSL version 3.3
    layout (location = 0) in vec4 vPos;
    
    void main()
    {
        gl_Position = vec4(vPos.x, vPos.y, vPos.z, 1.0);
    }
    ";

    //Fragment shaders are run on each fragment/pixel of the geometry.
    private const string FragmentShaderSource = @"
    #version 330 core
    out vec4 FragColor;

    void main()
    {
        FragColor = vec4(1.0f, 0.5f, 0.2f, 1.0f);
    }
    ";

    //Vertex data, uploaded to the VBO.
    private static readonly float[] Vertices =
    {
        //X    Y      Z
        0.5f,  0.5f, 0.0f,
        0.5f, -0.5f, 0.0f,
        -0.5f, -0.5f, 0.0f,
        -0.5f,  0.5f, 0.5f
    };

    //Index data, uploaded to the EBO.
    private static readonly uint[] Indices =
    {
        0, 1, 3,
        1, 2, 3
    };

    public unsafe void Load(GL gl)
    {
        //Getting the opengl api for drawing to the screen.
        _gl = gl;

        //Creating a vertex array.
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        //Initializing a vertex buffer that holds the vertex data.
        _vbo = _gl.GenBuffer(); //Creating the buffer.
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo); //Binding the buffer.
        fixed (void* v = &Vertices[0])
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint) (Vertices.Length * sizeof(uint)), v, BufferUsageARB.StaticDraw); //Setting buffer data.
        }

        //Initializing a element buffer that holds the index data.
        _ebo = _gl.GenBuffer(); //Creating the buffer.
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo); //Binding the buffer.
        fixed (void* i = &Indices[0])
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint) (Indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw); //Setting buffer data.
        }

        //Creating a vertex shader.
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, VertexShaderSource);
        _gl.CompileShader(vertexShader);

        //Checking the shader for compilation errors.
        string infoLog = _gl.GetShaderInfoLog(vertexShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            Console.WriteLine($"Error compiling vertex shader {infoLog}");
        }

        //Creating a fragment shader.
        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, FragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        //Checking the shader for compilation errors.
        infoLog = _gl.GetShaderInfoLog(fragmentShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            Console.WriteLine($"Error compiling fragment shader {infoLog}");
        }

        //Combining the shaders under one shader program.
        _shader = _gl.CreateProgram();
        _gl.AttachShader(_shader, vertexShader);
        _gl.AttachShader(_shader, fragmentShader);
        _gl.LinkProgram(_shader);

        //Checking the linking for errors.
        _gl.GetProgram(_shader, GLEnum.LinkStatus, out var status);
        if (status == 0)
        {
            Console.WriteLine($"Error linking shader {_gl.GetProgramInfoLog(_shader)}");
        }

        //Delete the no longer useful individual shaders;
        _gl.DetachShader(_shader, vertexShader);
        _gl.DetachShader(_shader, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        //Tell opengl how to give the data to the shaders.
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), null);
        _gl.EnableVertexAttribArray(0);
    }

    public unsafe void Render(double obj) //Method needs to be unsafe due to draw elements.
    {
        //Clear the color channel.
        //_gl.Clear((uint) ClearBufferMask.ColorBufferBit);

        //Bind the geometry and shader.
        _gl.BindVertexArray(_vao);
        _gl.UseProgram(_shader);

        //Draw the geometry.
        _gl.DrawElements(PrimitiveType.Triangles, (uint) Indices.Length, DrawElementsType.UnsignedInt, null);
    }

    public void Release()
    {
        //Remember to delete the buffers.
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteProgram(_shader);
    }
}