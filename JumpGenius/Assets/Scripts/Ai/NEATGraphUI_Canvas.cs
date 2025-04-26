using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visualizes NEAT training progress by drawing a graph of best fitness values across generations.
/// Draws dots, lines, and axis labels dynamically inside a UI canvas.
/// </summary>
public class NEATGraphUI_Canvas : MonoBehaviour
{
    public GameManager gameManager;

    [Header("Graph Settings")]
    public RectTransform graphContainer;
    public Sprite circleSprite;
    public Color lineColor = Color.green;
    public float yMax = 100f;
    public int maxVisiblePoints = 20;
    public int yStepCount = 5;
    public Font labelFont;
    public Text infoText;
    public Text platformTimerText; // Assign in Inspector


    private List<GameObject> pointObjects = new();
    private List<RectTransform> lineSegments = new();
    private List<GameObject> xAxisLabels = new();
    private bool yAxisDrawn = false;

    void Start()
    {
        if (gameManager == null)
            gameManager = GameManager.instance;
    }

    void Update()
    {
        if (gameManager == null || gameManager.neatManager == null)
            return;

        List<float> fitnessHistory = gameManager.neatManager.bestFitnessHistory;
        if (fitnessHistory == null)
            return;

        if (!yAxisDrawn)
        {
            DrawYAxis();
            yAxisDrawn = true;
        }

        ShowGraph(fitnessHistory);

        if (infoText != null)
        {
            int gen = gameManager.neatManager.CurrentGeneration;
            float lastBest = gameManager.neatManager.LastBestFitness;
            float allTimeBest = gameManager.neatManager.AllTimeBestFitness;
            //print(timer);
            infoText.text = $"Generation: {gen}\n" +
                            $"Current Best: {lastBest:F2}\n" +
                            $"All-Time Best: {allTimeBest:F2}";
        }

    }

    /// <summary>
    /// Draws graph using provided fitness history.
    /// </summary>
    void ShowGraph(List<float> valueList)
    {
        bool drawLines = valueList.Count >= 2;
        ClearGraph();

        float graphHeight = graphContainer.sizeDelta.y;
        float graphWidth = graphContainer.sizeDelta.x;

        int startIndex = Mathf.Max(0, valueList.Count - maxVisiblePoints);
        int visibleCount = Mathf.Min(maxVisiblePoints, valueList.Count);

        float xStep = graphWidth / Mathf.Max(visibleCount - 1, 1);
        float xPos = 0f;
        GameObject lastPoint = null;

        for (int i = 0; i < visibleCount; i++)
        {
            float value = valueList[startIndex + i];
            float yPos = Mathf.Clamp((value / yMax) * graphHeight, 0, graphHeight);

            GameObject point = CreatePoint(new Vector2(xPos, yPos));

            if (drawLines && lastPoint != null)
            {
                CreateLineBetween(
                    lastPoint.GetComponent<RectTransform>().anchoredPosition,
                    point.GetComponent<RectTransform>().anchoredPosition,
                    valueList[startIndex + i - 1],
                    valueList[startIndex + i]
                );
            }

            CreateXLabel(i, xPos, startIndex + i);
            lastPoint = point;
            xPos += xStep;
        }
    }

    GameObject CreatePoint(Vector2 anchoredPosition)
    {
        GameObject point = new GameObject("point", typeof(Image));
        point.transform.SetParent(graphContainer, false);
        point.GetComponent<Image>().sprite = circleSprite;

        RectTransform rt = point.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(8, 8);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);

        pointObjects.Add(point);
        return point;
    }

    void CreateLineBetween(Vector2 a, Vector2 b, float prevValue, float currentValue)
    {
        GameObject line = new GameObject("line", typeof(Image));
        line.transform.SetParent(graphContainer, false);

        Color segmentColor = Color.white;
        if (currentValue > prevValue) segmentColor = Color.green;
        else if (currentValue < prevValue) segmentColor = Color.red;

        line.GetComponent<Image>().color = segmentColor;

        RectTransform rt = line.GetComponent<RectTransform>();
        Vector2 dir = (b - a).normalized;
        float distance = Vector2.Distance(a, b);

        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.sizeDelta = new Vector2(distance, 2f);
        rt.anchoredPosition = a + dir * distance * 0.5f;
        rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        lineSegments.Add(rt);
    }

    void CreateXLabel(int index, float xPos, int generationNumber)
    {
        GameObject label = new GameObject("xLabel", typeof(Text));
        label.transform.SetParent(graphContainer, false);

        Text txt = label.GetComponent<Text>();
        txt.text = (generationNumber + 1).ToString();
        txt.font = labelFont;
        txt.fontSize = 20;
        txt.color = Color.white;
        txt.alignment = TextAnchor.UpperCenter;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(0, 0);
        lrt.anchoredPosition = new Vector2(xPos, -20);
        lrt.sizeDelta = new Vector2(50, 30);

        xAxisLabels.Add(label);
    }

    void DrawYAxis()
    {
        float graphHeight = graphContainer.sizeDelta.y;

        for (int i = 0; i <= yStepCount; i++)
        {
            float normalized = i / (float)yStepCount;
            float yPos = normalized * graphHeight;

            GameObject label = new GameObject("yLabel", typeof(Text));
            label.transform.SetParent(graphContainer, false);

            Text txt = label.GetComponent<Text>();
            txt.text = Mathf.RoundToInt(normalized * yMax).ToString();
            txt.font = labelFont;
            txt.fontSize = 20;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleRight;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(0, 0);
            lrt.pivot = new Vector2(1, 0.5f);
            lrt.anchoredPosition = new Vector2(-5, yPos);
            lrt.sizeDelta = new Vector2(40, 30);
        }
    }

    void ClearGraph()
    {
        foreach (var p in pointObjects)
            Destroy(p);
        pointObjects.Clear();

        foreach (var l in lineSegments)
            Destroy(l.gameObject);
        lineSegments.Clear();

        foreach (var x in xAxisLabels)
            Destroy(x);
        xAxisLabels.Clear();
    }
}
