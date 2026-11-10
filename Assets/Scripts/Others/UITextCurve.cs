
using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class UITextCurve : MonoBehaviour
{
    [Range(0f, 30f)]
    public float curveAmount = 8f;

    private TMP_Text tmpText;

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        if (tmpText != null)
            tmpText.OnPreRenderText -= OnTextPreRender;
    }

    private void Initialize()
    {
        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();

        tmpText.OnPreRenderText -= OnTextPreRender;
        tmpText.OnPreRenderText += OnTextPreRender;

        PreviewCurve();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;

        PreviewCurve();
    }

    [ContextMenu("Preview Curve")]
    public void PreviewCurve()
    {
        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();

        if (tmpText == null)
            return;

        tmpText.ForceMeshUpdate();
    }

    private void OnTextPreRender(TMP_TextInfo textInfo)
    {
        if (textInfo.characterCount == 0)
            return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible)
                continue;

            minX = Mathf.Min(minX, charInfo.bottomLeft.x);
            maxX = Mathf.Max(maxX, charInfo.topRight.x);
        }

        if (maxX <= minX)
            return;

        float centerX = (minX + maxX) * 0.5f;
        float halfWidth = (maxX - minX) * 0.5f;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible)
                continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Vector3[] vertices =
                textInfo.meshInfo[materialIndex].vertices;

            for (int j = 0; j < 4; j++)
            {
                Vector3 vertex = vertices[vertexIndex + j];

                float normalizedX =
                    (vertex.x - centerX) / halfWidth;

                float offsetY =
                    curveAmount * (1f - normalizedX * normalizedX);

                vertex.y += offsetY;

                vertices[vertexIndex + j] = vertex;
            }
        }
    }
}
