using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicFlyingEnemy : Enemy
{
    [SerializeField] public float maxFollowDist = 100f;
    [SerializeField] public float speed = 5f;
    [SerializeField] public float rotationSpeed = 2f;
    PlayerController player;

    private void Awake()
    {
        player = FindObjectOfType<PlayerController>();
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance.Paused)
            return;

        transform.Rotate(Vector3.forward, rotationSpeed * Time.fixedDeltaTime);
        Vector2 direction = player.transform.position - this.transform.position;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, Mathf.Infinity, hitMask);
        PlayerController tempPlayer;
        if (hit.collider != null && hit.collider.TryGetComponent<PlayerController>(out tempPlayer))
        {
            direction.Normalize();
            Vector2 position = transform.position;
            position += (direction * speed * Time.fixedDeltaTime);
            transform.position = position;
        }
    }
}
