using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BurstParticles : MonoBehaviour
{
    [SerializeField] ParticleSystem ps;

    private void Awake()
    {
        if (ps == null)
            ps = GetComponent<ParticleSystem>();
    }

    public void EmitBurst(int num)
    {
        ps.Emit(num);
    }
}
