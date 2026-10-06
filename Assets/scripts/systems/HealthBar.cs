using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider slider;

    [Header("Follow Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);


    // ==================== UNITY START ====================

    // Keeps the health bar above its target.
    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = target.position + worldOffset;
    }

    // ===================== UNITY END =====================


    // ==================== HEALTH BAR ====================

    // Updates the slider according to the character's health.
    public void SetHealth(int currentHealth, int maxHealth)
    {
        if (slider == null)
        {
            Debug.LogWarning(
                "[HealthBar] Slider reference is missing on " +
                gameObject.name
            );

            return;
        }

        if (maxHealth <= 0)
        {
            slider.value = 0f;
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;

        float healthPercent =
            (float)currentHealth / maxHealth;

        slider.value = healthPercent;
    }


    // Hides this health bar.
    public void Hide()
    {
        gameObject.SetActive(false);
    }


    // Shows this health bar.
    public void Show()
    {
        gameObject.SetActive(true);
    }

    // ===================== HEALTH BAR END =====================
}