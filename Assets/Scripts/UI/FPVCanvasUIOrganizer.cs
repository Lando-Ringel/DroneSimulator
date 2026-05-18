using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace DroneSimulator.UI
{
    public class FPVCanvasUIOrganizer : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_droneHUDTopRightText;

        public void UpdateDroneHudText(float altitude, float climbSpeed, float groundSpeed, float cameraTilt, float battery, int ammo, int maxAmmo, int lifeCount)
        {
            string text = "";
            text += $"Altitude: {altitude:F1}\n";
            text += $"Climb Speed: {climbSpeed:F1}\n";
            text += $"Ground Speed: {groundSpeed:F1}\n";
            text += $"Camera Tilt: {cameraTilt:F1}\u00B0\n";
            text += $"Battery: {battery:F1}%\n";
            text += $"Ammo: {ammo}/{maxAmmo}\n";
            text += $"N: {lifeCount}";
            m_droneHUDTopRightText.text = text;

        }
    }
}