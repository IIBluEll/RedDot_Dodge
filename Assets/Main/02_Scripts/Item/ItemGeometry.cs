using UnityEngine;

public static class ItemGeometry
{
    public static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 travel = end - start;
        float progress = travel.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, travel) / travel.sqrMagnitude) : 0f;
        return (point - (start + travel * progress)).sqrMagnitude;
    }

    public static bool TryGetContactTime(Vector2 relativeStart, Vector2 relativeEnd, float radius, out float contactTime)
    {
        contactTime = 0f;
        float c = relativeStart.sqrMagnitude - radius * radius;
        if (c <= 0f)
            return true;

        Vector2 travel = relativeEnd - relativeStart;
        float a = travel.sqrMagnitude;
        if (a <= 0f)
            return false;

        float b = 2f * Vector2.Dot(relativeStart, travel);
        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
            return false;

        contactTime = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
        return contactTime >= 0f && contactTime <= 1f;
    }
}
