using System.Numerics;
using TrueMoon.Aether.Preview;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorCameraNavigationTests
{
    [Fact]
    public void Orbit_UsesTotalLogicalDeltaClampsPitchAndReturnsToExactPoseWithFreshRevision()
    {
        var start=PreviewCamera.Default;
        Assert.True(EditorCameraNavigation.TryOrbit(start,start,40,20,out var moved));
        Assert.Equal(start.Pivot,moved.Pivot);Assert.Equal(start.Distance,moved.Distance);
        Assert.InRange(Vector3.Distance(moved.World.Translation,moved.Pivot),5.9999f,6.0001f);
        var forward=Vector3.TransformNormal(-Vector3.UnitZ,moved.World);
        Assert.InRange(forward.X,.30f,.32f);Assert.InRange(forward.Y,-.17f,-.15f);
        Assert.True(EditorCameraNavigation.TryOrbit(start,moved,40,20,out var repeat));Assert.Same(moved,repeat);
        Assert.True(EditorCameraNavigation.TryOrbit(start,moved,0,0,out var restored));
        Assert.Equal(start.World,restored.World);Assert.Equal(3ul,restored.Revision);
        Assert.True(EditorCameraNavigation.TryOrbit(start,restored,0,1e10,out var polar));
        Assert.InRange(Vector3.TransformNormal(-Vector3.UnitZ,polar.World).Y,-.99996f,-.99994f);
        Assert.True(float.IsFinite(polar.CreateProjection(640,360).ViewProjection.M11));
    }

    [Theory] [InlineData(360)] [InlineData(720)]
    public void Pan_UsesImageLogicalHeightAndMovesPivotWithCamera(int height)
    {
        var start=PreviewCamera.Default;
        Assert.True(EditorCameraNavigation.TryPan(start,start,30,15,height,out var moved));
        var expected=(float)(12*Math.Tan(Math.PI/6)/height);
        Assert.InRange(moved.Pivot.X,-30*expected-.00001f,-30*expected+.00001f);
        Assert.InRange(moved.Pivot.Y,1+15*expected-.00001f,1+15*expected+.00001f);
        Assert.Equal(moved.Pivot-start.Pivot,moved.World.Translation-start.World.Translation);
        Assert.Equal(start.Distance,moved.Distance);Assert.Equal(start.World.M11,moved.World.M11);
    }

    [Fact]
    public void Dolly_IsExponentialBoundedAndPreservesOrientation()
    {
        var start=PreviewCamera.Default;
        Assert.True(EditorCameraNavigation.TryDolly(start,1,out var near));Assert.InRange(near.Distance,5.1642f,5.1643f);
        Assert.True(EditorCameraNavigation.TryDolly(near,-1,out var back));Assert.InRange(back.Distance,5.99999f,6.00001f);
        Assert.Equal(start.Pivot,near.Pivot);Assert.Equal(start.World.M22,near.World.M22);
        Assert.True(EditorCameraNavigation.TryDolly(near,1e20,out var min));Assert.InRange(min.Distance,.10099f,.10101f);
        Assert.True(EditorCameraNavigation.TryDolly(min,-1e20,out var max));Assert.Equal(90f,max.Distance);
    }

    [Theory] [InlineData(640,360)] [InlineData(360,640)]
    public void Frame_FitsAllAffineCornersWithMarginAndPreservesRotation(int width,int height)
    {
        var world=Matrix4x4.CreateScale(2,.5f,3)*Matrix4x4.CreateRotationY(.7f)*Matrix4x4.CreateTranslation(3,2,-4);
        Assert.True(EditorCameraNavigation.TryFrame(PreviewCamera.Default,world,width,height,out var framed));
        Assert.InRange(Vector3.Distance(new(3,2,-4),framed.Pivot),0,.00001f);
        var projection=framed.CreateProjection(width,height);
        for(var x=0;x<2;x++)for(var y=0;y<2;y++)for(var z=0;z<2;z++)
        {
            Assert.True(projection.TryProject(Vector3.Transform(new(x-.5f,y-.5f,z-.5f),world),out var p));
            Assert.InRange(p.X,-1,1);Assert.InRange(p.Y,-1,1);Assert.InRange(p.Z,0,1);
        }
        Assert.Equal(1f,framed.World.M11);Assert.Equal(2ul,framed.Revision);
    }

    [Fact]
    public void InvalidMathRetainsLastValidValueAndRevisionExhaustionNeverWraps()
    {
        var start=PreviewCamera.Default;
        Assert.False(EditorCameraNavigation.TryOrbit(start,start,double.NaN,0,out var a));Assert.Same(start,a);
        Assert.False(EditorCameraNavigation.TryPan(start,start,0,0,0,out var b));Assert.Same(start,b);
        Assert.False(EditorCameraNavigation.TryDolly(start,double.PositiveInfinity,out var c));Assert.Same(start,c);
        Assert.False(EditorCameraNavigation.TryFrame(start,Matrix4x4.CreateScale(10000),640,360,out var d));Assert.Same(start,d);
        var exhausted=new PreviewCamera(start.World,start.Projection,start.Pivot,start.Distance,ulong.MaxValue);
        Assert.Throws<OverflowException>(()=>EditorCameraNavigation.TryDolly(exhausted,1,out _));
    }
}
