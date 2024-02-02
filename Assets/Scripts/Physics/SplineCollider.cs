using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(EdgeCollider2D))]
public class SplineCollider : MonoBehaviour
{
    [Header("References")]
    [SerializeField] protected EdgeCollider2D _collider;
    public EdgeCollider2D Collider { get { if (_collider == null) _collider = GetComponent<EdgeCollider2D>(); return _collider; } }

    [SerializeField] protected SplineContainer _splineContainer;
    public SplineContainer Spline { get { if (_splineContainer == null) _splineContainer = GetComponent<SplineContainer>(); return _splineContainer; } }

    [Header("Parameters")]
    [SerializeField][Range(0.0001f, 100)] protected float dx; // increment distance to create edge collider points

    protected float cachedLength = -1;

    public void ResetCachedLength()
    {
        cachedLength = -1;
    }

    public void SyncEdgesToSpline()
    {
        float splineLength = Spline.CalculateLength(); // spline length :)
        if (dx <= 0)
            dx = 0.1f;

        if (splineLength == cachedLength)
            return;

        cachedLength = splineLength;
        Collider.points = CalculatePointsAlongSpline().ToArray();
    }

    public List<Vector2> CalculatePointsAlongSpline()
    {
        float deltaPercent = dx / cachedLength; // calculate chance in percentage (0-1)
        int numberPoints = Mathf.RoundToInt(1 / deltaPercent); // round to int
        deltaPercent = 1f / numberPoints; // recalculate after rounding
        numberPoints++; // add 1 for the point at the end

        Vector3 offset = -Spline.transform.position;

        List<Vector2> points = new List<Vector2>();
        for (int i = 0; i < numberPoints; i++)
        {
            float currentPercent = deltaPercent * i;
            Vector3 currentPosition = Spline.EvaluatePosition(currentPercent);
            currentPosition += offset;
            points.Add(new Vector2(currentPosition.x, currentPosition.y));
        }

        return points;
    }

    protected virtual void FixedUpdate()
    {
        CalculatePointsAlongSpline();
    }

    private void Update()
    {
        
    }
}
