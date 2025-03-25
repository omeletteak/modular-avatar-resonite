using System.Numerics;
using Elements.Core;
using ResoPuppetSchema;

namespace nadena.dev.resonity.remote.puppeteer.rpc;

public static class Conversions
{
    public static Vector2 Vec2(this ResoPuppetSchema.Vector v)
    {
        if (v.HasZ || v.HasW)
        {
            throw new System.Exception("Invalid vector cardinality");
        }
        
        return new Vector2(v.X, v.Y);
    }
    
    public static Vector3 Vec3(this ResoPuppetSchema.Vector v)
    {
        if (!v.HasZ || v.HasW)
        {
            throw new System.Exception("Invalid vector cardinality");
        }
        
        return new Vector3(v.X, v.Y, v.Z);
    }
    
    public static floatQ Quat(this ResoPuppetSchema.Quaternion q)
    {
        return new floatQ(q.X, q.Y, q.Z, q.W);
    }
    
    public static Vector4 Vec4(this ResoPuppetSchema.Vector v)
    {
        if (!v.HasZ || !v.HasW)
        {
            throw new System.Exception("Invalid vector cardinality");
        }
        
        return new Vector4(v.X, v.Y, v.Z, v.W);
    }
    
    public static color Color(this ResoPuppetSchema.Vector v)
    {
        return new color(v.X, v.Y, v.Z, v.W);
    }

    // ReSharper disable once InconsistentNaming
    public static float4x4 To4x4(this Matrix m)
    {
        return new float4x4(m.Values.ToArray());
    }
}