using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FadeOnEnabled : MonoBehaviour
{
    [SerializeField] public Image image;
    [SerializeField] private float maxOpacity;
    [SerializeField] private float lerpValue = 0.4f;

    private void Awake()
    {
        if (image == null)
            image = GetComponent<Image>();
    }

    // Start is called before the first frame update
    private void OnEnable()
    {
        Color color = image.color;
        color.a = 0;
        image.color = color;
    }

    private void OnDisable()
    {
        
    }

    private void Update()
    {
        Color color = image.color;
        float a = Mathf.Lerp(color.a, maxOpacity, lerpValue);
        color.a = a;
        image.color = color;
    }
}
