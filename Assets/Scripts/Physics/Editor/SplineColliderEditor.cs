using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SplineCollider))]
public class SplineColliderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        SplineCollider myTarget = (SplineCollider)target;

        if (GUILayout.Button("Sync Collider"))
        {
            myTarget.ResetCachedLength();
            myTarget.SyncEdgesToSpline();
        }
    }
}
