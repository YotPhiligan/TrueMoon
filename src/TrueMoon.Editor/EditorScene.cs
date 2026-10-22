using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Editor;

public static class EditorScene
{
    public static SceneSnapshot Demo() => new("Demo room", 1,
    [
        Box("red-cube", -1.5f, 1, -1, 1, 1, 1, new(.8f,.2f,.15f), true),
        Box("green-cube", 1.5f, 1, -1, 1, 1, 1, new(.2f,.8f,.3f), true),
        Box("switch", 0, 1, -2, 1, 1, 1, new(.15f,.4f,.9f), true),
        Box("floor", 0, -.1f, -1, 8, .2f, 10, new(.35f,.4f,.45f)),
        Box("back-wall", 0, 1.5f, -5, 8, 3, .2f, new(.45f,.45f,.5f)),
        Box("left-wall", -4, 1.5f, -1, .2f, 3, 8, new(.5f,.4f,.35f)),
        Box("right-wall", 4, 1.5f, -1, .2f, 3, 8, new(.35f,.45f,.4f))
    ]);
    private static SceneEntity Box(string id, float x, float y, float z, float sx, float sy, float sz, Vector3 color, bool interactive = false)
        => new() { Id = id, X = x, Y = y, Z = z, Interactive = interactive,
            Primitive = ScenePrimitive.FromPrimitive(new(Matrix4x4.CreateScale(sx, sy, sz) * Matrix4x4.CreateTranslation(x, y, z), color)) };
    public static string Name(string id) => id switch
    { "red-cube" => "Красный куб", "green-cube" => "Зелёный куб", "switch" => "Синий куб", "floor" => "Пол", "back-wall" => "Задняя стена", "left-wall" => "Левая стена", "right-wall" => "Правая стена", _ => id };
}
