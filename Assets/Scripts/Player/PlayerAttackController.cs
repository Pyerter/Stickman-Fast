using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackController : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] float attackDuration = 0.25f;
    [SerializeField] float attackCooldown = 0.5f;
    [SerializeField] float lastAttack = 0f;

    private void OnEnable()
    {
        if (Time.fixedTime - lastAttack > attackCooldown)
        {
            lastAttack = Time.fixedTime;
        } else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance.Paused)
        {
            lastAttack += Time.fixedDeltaTime;
            return;
        }
        if (Time.fixedTime - lastAttack > attackDuration)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Enemy enemy;
        if (collision.TryGetComponent<Enemy>(out enemy)) {
            player.currentScore += (int)(enemy.killPoints * player.currentCombo);
            Destroy(enemy.gameObject);
        }
    }

}
