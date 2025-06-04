using UnityEngine;
using UnityEngine.UI;

public class CoordinateTracker : MonoBehaviour
{
    public Transform targetObject;
    public Vector3 displayOffset = new Vector3(0, 2, 0);

    private Canvas displayCanvas;
    private Text coordinateText;

    void Start()
    {
        if (targetObject == null)
            targetObject = this.transform;

        CreateDisplay();
    }

    void Update()
    {
        UpdateCoordinates();
    }

    void CreateDisplay()
    {
        // Canvas作成
        GameObject canvasObj = new GameObject("CoordinateDisplay");
        canvasObj.transform.SetParent(this.transform);

        displayCanvas = canvasObj.AddComponent<Canvas>();
        displayCanvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = displayCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 80);
        canvasRect.localScale = Vector3.one * 0.01f;

        // テキスト作成
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform);

        coordinateText = textObj.AddComponent<Text>();
        coordinateText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        coordinateText.fontSize = 20;
        coordinateText.color = Color.white;
        coordinateText.alignment = TextAnchor.MiddleCenter;

        RectTransform textRect = coordinateText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    void UpdateCoordinates()
    {
        if (targetObject == null || coordinateText == null) return;

        Vector3 pos = targetObject.position;
        coordinateText.text = $"X: {pos.x:F1}\nY: {pos.y:F1}\nZ: {pos.z:F1}";

        displayCanvas.transform.position = targetObject.position + displayOffset;
    }
}