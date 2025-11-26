using UnityEngine;
using UnityEngine.UI;

public class NavigationBarButton : MonoBehaviour
{
    [SerializeField] private Color newColor;
    [SerializeField] private GameObject otherPanel;
    [SerializeField] private GameObject displayPanel;
    [SerializeField] private GameObject otherButton;
    public void OnClickedButton()
    {
        otherPanel.SetActive(false);
        displayPanel.SetActive(true);
        otherButton.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
        GetComponent<Image>().color = newColor;
    }
}
