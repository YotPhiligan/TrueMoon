using System.Numerics;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Editor;

/// <summary>Pure v1 navigation over immutable cameras. Deltas are logical pixels; no document, UI or runtime mutation.</summary>
public static class EditorCameraNavigation
{
    /// <summary>Orbit sensitivity in radians per logical pixel.</summary>
    public const double OrbitSensitivity = .008;
    /// <summary>Finite pitch bound avoiding the polar singularity.</summary>
    public const double PitchLimit = Math.PI / 2 - .01;

    /// <summary>Orbits from the gesture's original pose, publishing against the latest revision.</summary>
    /// <param name="start">Frozen gesture baseline.</param><param name="latest">Latest desired revision.</param>
    /// <param name="dx">Total logical horizontal delta.</param><param name="dy">Total logical vertical delta.</param><param name="camera">Candidate, or latest on invalid math.</param>
    /// <returns>Whether the candidate is valid. Revision exhaustion throws instead of wrapping.</returns>
    public static bool TryOrbit(PreviewCamera start, PreviewCamera latest, double dx, double dy, out PreviewCamera camera)
    {
        camera=latest; if(!double.IsFinite(dx)||!double.IsFinite(dy))return false;
        if(dx==0&&dy==0)return Make(latest,start.World,start.Projection,start.Pivot,start.Distance,out camera);
        var forward=Vector3.TransformNormal(-Vector3.UnitZ,start.World);
        var yaw=Math.IEEERemainder(Math.Atan2(-forward.X,-forward.Z)-dx*OrbitSensitivity,Math.Tau);
        var pitch=Math.Clamp(Math.Asin(Math.Clamp((double)forward.Y,-1,1))-dy*OrbitSensitivity,-PitchLimit,PitchLimit);
        var rotation=Matrix4x4.CreateFromYawPitchRoll((float)yaw,(float)pitch,0);
        return Make(latest,rotation*Matrix4x4.CreateTranslation(start.Pivot-Vector3.TransformNormal(-Vector3.UnitZ,rotation)*start.Distance),
            start.Projection,start.Pivot,start.Distance,out camera);
    }

    /// <summary>Pans camera and pivot together using world units per logical image pixel.</summary>
    /// <param name="start">Frozen gesture baseline.</param><param name="latest">Latest desired revision.</param>
    /// <param name="dx">Total horizontal delta.</param><param name="dy">Total vertical delta.</param>
    /// <param name="imageHeight">Positive logical image height, excluding letterbox.</param><param name="camera">Candidate or unchanged latest.</param>
    /// <returns>False for invalid or unrepresentable input. Revision exhaustion throws.</returns>
    public static bool TryPan(PreviewCamera start, PreviewCamera latest, double dx, double dy, double imageHeight, out PreviewCamera camera)
    {
        camera=latest;
        if(!double.IsFinite(dx)||!double.IsFinite(dy)||!double.IsFinite(imageHeight)||imageHeight<=0)return false;
        var scale=2*start.Distance*Math.Tan(start.Projection.FieldOfView/2d)/imageHeight;
        var right=Vector3.TransformNormal(Vector3.UnitX,start.World);var up=Vector3.TransformNormal(Vector3.UnitY,start.World);
        var shift=new Vector3((float)((-right.X*dx+up.X*dy)*scale),(float)((-right.Y*dx+up.Y*dy)*scale),(float)((-right.Z*dx+up.Z*dy)*scale));
        var world=start.World;world.Translation+=shift;
        return Make(latest,world,start.Projection,start.Pivot+shift,start.Distance,out camera);
    }

    /// <summary>Dollies along the existing orientation toward the pivot, bounded by near/far perspective distances.</summary>
    /// <param name="current">Latest desired camera.</param><param name="wheelDelta">Finite logical wheel steps, positive toward the pivot.</param>
    /// <param name="camera">Candidate or unchanged current.</param><returns>False for invalid input. Revision exhaustion throws.</returns>
    public static bool TryDolly(PreviewCamera current, double wheelDelta, out PreviewCamera camera)
    {
        camera=current;if(!double.IsFinite(wheelDelta))return false;if(wheelDelta==0)return true;
        var min=Math.Max(.01,current.Projection.Near*1.01d);var max=current.Projection.Far*.9d;
        if(min>=max)return false;
        var distance=(float)Math.Clamp(current.Distance*Math.Exp(-Math.Clamp(wheelDelta,-100,100)*.15),min,max);
        var world=current.World;world.Translation=current.Pivot-Vector3.TransformNormal(-Vector3.UnitZ,world)*distance;
        return Make(current,world,current.Projection,current.Pivot,distance,out camera);
    }

    /// <summary>Frames the selected exact affine cube using a conservative world bounding sphere and physical aspect.</summary>
    /// <param name="current">Latest desired camera; its orientation is retained.</param><param name="cubeWorld">Exact affine cube transform.</param>
    /// <param name="width">Positive physical image width.</param><param name="height">Positive physical image height.</param>
    /// <param name="camera">Candidate or unchanged current.</param><returns>False if the bounds cannot fit the valid perspective range.</returns>
    public static bool TryFrame(PreviewCamera current, Matrix4x4 cubeWorld, int width, int height, out PreviewCamera camera)
    {
        camera=current;if(width<=0||height<=0)return false;
        try { new RenderPrimitive(cubeWorld,Vector3.One).Validate(); } catch(InvalidDataException){return false;}
        var min=new Vector3(float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity);
        for(var x=0;x<2;x++)for(var y=0;y<2;y++)for(var z=0;z<2;z++)
        {
            var p=Vector3.Transform(new(x-.5f,y-.5f,z-.5f),cubeWorld);
            if(!Finite(p))return false; min=Vector3.Min(min,p);max=Vector3.Max(max,p);
        }
        var center=new Vector3((float)(((double)min.X+max.X)/2),(float)(((double)min.Y+max.Y)/2),(float)(((double)min.Z+max.Z)/2));
        var sx=(double)max.X-min.X;var sy=(double)max.Y-min.Y;var sz=(double)max.Z-min.Z;
        var radius=Math.Sqrt(sx*sx+sy*sy+sz*sz)/2;
        var half=Math.Min(current.Projection.FieldOfView/2d,Math.Atan(Math.Tan(current.Projection.FieldOfView/2d)*width/height));
        var distance=Math.Max(radius/Math.Sin(half)*1.1,radius+current.Projection.Near*1.1d);
        if(!double.IsFinite(distance)||radius<=0||distance+radius>=current.Projection.Far)return false;
        var world=current.World;world.Translation=center-Vector3.TransformNormal(-Vector3.UnitZ,world)*(float)distance;
        return Make(current,world,current.Projection,center,(float)distance,out camera);
    }
    private static bool Make(PreviewCamera latest,Matrix4x4 world,PerspectiveProjection projection,Vector3 pivot,float distance,out PreviewCamera camera)
    {
        camera=latest;
        try{camera=latest.WithView(world,projection,pivot,distance);return true;}
        catch(Exception error)when(error is ArgumentException or RenderContentException){return false;}
    }
    private static bool Finite(Vector3 value)=>float.IsFinite(value.X)&&float.IsFinite(value.Y)&&float.IsFinite(value.Z);
}
