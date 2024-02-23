using CryingOnion.MultiplayerTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthView : MonoBehaviour
{
    [SerializeField] private Image healthBar;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private NetworkPlayerController networkPlayerController;

    private ushort maxHealth;
    private Camera mainCam;

    private void Start()
    {
        maxHealth = networkPlayerController.PlayerMaxHealth.Value;
        UpdateHealth(networkPlayerController.PlayerHealth.Value);
        networkPlayerController.PlayerHealth.OnChange += OnPlayerHealthChange;
        mainCam = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCam)
            transform.rotation = Quaternion.LookRotation(mainCam.transform.forward);
    }

    private void OnDestroy()
    {
        if (networkPlayerController != null)
            networkPlayerController.PlayerHealth.OnChange -= OnPlayerHealthChange;
    }

    private void OnPlayerHealthChange(ushort prev, ushort next, bool asserver) => UpdateHealth(next);

    private void UpdateHealth(ushort cur)
    {
        float current = (float)cur / maxHealth;
        healthText.text = $"%{current * maxHealth}";
        healthBar.rectTransform.anchorMax = new Vector2(current, 1);
    }
}