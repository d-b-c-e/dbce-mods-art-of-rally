namespace Dbce.TripleScreen;

/// <summary>A world-space ray from the measured eye through a physical panel point.</summary>
public readonly struct EyeRay
{
    public EyeRay(Vector3d origin, Vector3d direction)
    {
        Origin = origin;
        Direction = direction.Normalized();
    }

    public Vector3d Origin { get; }
    public Vector3d Direction { get; }
}
