using System.Numerics;
using BepuPhysics.Collidables;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton.Bepu;

internal sealed partial class BepuWorld
{
    public PhysicsShapeId CreateShape(PhysicsShapeDefinition definition)
    {
        Enter();
        try
        {
            PhysicsValidation.ValidateShape(definition, Options.Limits).ThrowIfFailure();
            var slot = Shapes.NextSlot("Shapes");
            var captured = definition is PhysicsStaticMesh source ? new PhysicsStaticMesh(source.Vertices.ToArray(), source.Indices.ToArray()) : definition;
            PhysicsValidation.ValidateShape(captured, Options.Limits).ThrowIfFailure();
            var id = new PhysicsShapeId(Id, slot, Shapes.Generation(slot));
            try
            {
                Faults.Visit(Checkpoint.BeforeMutation);
                var backend = captured switch
                {
                    PhysicsSphere s => Simulation.Shapes.Add(new Sphere(s.Radius)),
                    PhysicsBox b => Simulation.Shapes.Add(new Box(b.Size.X, b.Size.Y, b.Size.Z)),
                    PhysicsCapsule c => Simulation.Shapes.Add(new Capsule(c.Radius, c.CylinderLength)),
                    PhysicsStaticMesh m => CookMesh(m),
                    _ => throw new InvalidOperationException("Shape validation mismatch.")
                };
                Shapes.Publish(slot, new ShapeEntry(id, captured, backend)); Resources.Sample(); return id;
            }
            catch (Exception error) { throw Fault("CreateShape", error); }
        }
        finally { Exit(); }
    }
    private TypedIndex CookMesh(PhysicsStaticMesh mesh)
    {
        var indices = mesh.Indices.Span; var vertices = mesh.Vertices.Span;
        Resources.Pool.Take<Triangle>(indices.Length / 3, out var triangles);
        for (var i = 0; i < triangles.Length; i++)
            triangles[i] = new Triangle(vertices[indices[i * 3]], vertices[indices[i * 3 + 1]], vertices[indices[i * 3 + 2]]);
        return Simulation.Shapes.Add(new Mesh(triangles, Vector3.One, Resources.Pool));
    }
    public void RemoveShape(PhysicsShapeId shape)
    {
        Enter();
        try
        {
            var entry = Shape(shape);
            if (entry.BodyReferences != 0) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ResourceInUse, "Shape.Bodies");
            try { Faults.Visit(Checkpoint.BeforeMutation); Simulation.Shapes.RemoveAndDispose(entry.Backend, Resources.Pool); Shapes.Remove(shape.Slot); }
            catch (Exception error) { throw Fault("RemoveShape", error); }
        }
        finally { Exit(); }
    }
}
