using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] public LayerMask hitMask;
    [SerializeField] int damage = 1;
    [SerializeField] Vector2 slowBounce = new Vector2(-0.2f, -0.5f);
    [SerializeField] public int killPoints = 10;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        PlayerController player;
        if (collision.TryGetComponent<PlayerController>(out player))
        {
            Vector3 velocity = player.rigidbody.velocity;
            velocity *= slowBounce;
            player.rigidbody.velocity = velocity;
            player.currentCombo -= player.comboPerSecond * 2;
            player.health -= damage;
            Destroy(gameObject);
        }
    }
}
