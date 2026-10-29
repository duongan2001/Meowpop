
using UnityEngine;

public class BoxLayout : MonoBehaviour
{
    #region VARIABLES

    [Header("--- REFERENCES ---")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform leftWall;
    [SerializeField] private Transform rightWall;
    [SerializeField] private Transform bottomWall;

    [Header("--- MARGINS ---")]
    [SerializeField] private float leftMargin = 0.5f;
    [SerializeField] private float rightMargin = 0.5f;
    [SerializeField] private float topMargin = 0.5f;
    [SerializeField] private float bottomMargin = 0.5f;

    [Header("--- WALL SETTINGS ---")]
    [SerializeField] private float wallThickness = 0.15f;

    #endregion


    #region UNITY METHODS

    private void Start()
    {
        SetupBox();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && !Application.isPlaying)
                SetupBox();
        };
    }
#endif

    #endregion


    #region BOX METHODS

    [ContextMenu("Setup Box")]
    public void SetupBox()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || !targetCamera.orthographic)
            return;

        float screenHeight = targetCamera.orthographicSize * 2f;
        float screenWidth = screenHeight * targetCamera.aspect;

        Vector3 camPos = targetCamera.transform.position;

        float leftX = camPos.x - screenWidth / 2f + leftMargin;
        float rightX = camPos.x + screenWidth / 2f - rightMargin;

        float bottomY = camPos.y - screenHeight / 2f + bottomMargin;
        float topY = camPos.y + screenHeight / 2f - topMargin;

        float width = rightX - leftX;
        float height = topY - bottomY;

        if (width <= 0f || height <= 0f)
        {
            Debug.LogWarning("BoxLayout: Invalid margins!");
            return;
        }

        float centerX = (leftX + rightX) / 2f;
        float centerY = (bottomY + topY) / 2f;

        SetupWall(leftWall, leftX, centerY, wallThickness, height);
        SetupWall(rightWall, rightX, centerY, wallThickness, height);
        SetupWall(bottomWall, centerX, bottomY, width + wallThickness, wallThickness);
    }

    private void SetupWall(Transform wall, float x, float y, float width, float height)
    {
        if (wall == null)
            return;

        wall.position = new Vector3(x, y, wall.position.z);

        SpriteRenderer sr = wall.GetComponent<SpriteRenderer>();

        Vector2 spriteSize = Vector2.one;

        if (sr != null && sr.sprite != null)
            spriteSize = sr.sprite.bounds.size;

        wall.localScale = new Vector3(width / spriteSize.x, height / spriteSize.y, wall.localScale.z);
    }

    #endregion
}
