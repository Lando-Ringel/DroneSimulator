using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;


namespace DroneSimulator.UI
{
    public class FPVCanvasUIOrganizer : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_droneHUDTopRightText;
        [SerializeField] private TextMeshProUGUI m_droneHUDAlertText;
        private Coroutine m_alertCoroutine;
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
        public void UpdateDroneAlertText(string alertText, Color color)
        {
            m_droneHUDAlertText.color = color;
            m_droneHUDAlertText.text = alertText;

            // stop previous timer if a new message arrives
            if (m_alertCoroutine != null)
                StopCoroutine(m_alertCoroutine);

            m_alertCoroutine = StartCoroutine(ClearAlertAfterDelay(5f, color));
        }
        private IEnumerator ClearAlertAfterDelay(float delay, Color color)
        {
            yield return new WaitForSeconds(delay);

            float t = 0f;
            
            Color targetColor = color;
            targetColor.a = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime;
                m_droneHUDAlertText.color = Color.Lerp(color, targetColor, t);
                yield return null;
            }
            m_droneHUDAlertText.text = "";
            m_droneHUDAlertText.color = color;
        }
    }
}