using UnityEngine;
using UnityEngine.UI; 

public class GridCell : MonoBehaviour
{
    public Image cellImage; 
    private Color originalColor; 

    void Awake()
    {
        cellImage = GetComponent<Image>();
        originalColor = cellImage.color;
    }

    public void Highlight(Color highlightColor)
    {
        cellImage.color = highlightColor;
    }
    public void ResetColor()
    {
        cellImage.color = originalColor;
    }
}


