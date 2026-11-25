using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class basicNPC : MonoBehaviour
{
    [Header("Text Options")]
    [SerializeField, TextArea] private string[] textOptions =
    {
        "Option 1",
        "Option 2",
        "Option 3"
    };
    [SerializeField] private Color textColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField, Min(1f)] private float fontSize = 10f;
     private Vector2 textBoxSize = new Vector2(300f, 80f);
     private Vector2 textOffset = new Vector2(50f, 50f);
    [SerializeField] private float textDuration = 2f;
    [SerializeField] private float floatHeight = 10f;
    [SerializeField, Min(0f)] private float spawnWobbleDuration = 1f;
    [SerializeField, Min(0f)] private float spawnWobbleHeight = 1f;
    [SerializeField, Min(0f)] private float spawnWobbleRotation = 4f;
    [SerializeField, Min(0f)] private float spawnWobbleFrequency = 2f;

    [Header("Audio Options")]
     private AudioSource audioSource;
    [SerializeField] private AudioClip talkSound;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 1f;

 [Header("References")]
    private CircleCollider2D cc2D;

     private Canvas canvas;
    private bool isTalking;
    private Camera worldCamera;
    private RectTransform canvasRect;
    public TMP_FontAsset defaultFont;
    private void Awake()
    {
        cc2D = GetComponent<CircleCollider2D>();
        canvas = FindAnyObjectByType<Canvas>();
        canvasRect = canvas.GetComponent<RectTransform>();
        worldCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        defaultFont = TMP_Settings.defaultFontAsset;
        
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        float talkChance = Random.Range(0, 12);
        isTalking = talkChance > 6;

        if (isTalking)
        {
            ShowText();
            Debug.Log("Talking");
        }
        else
        {
            Debug.Log("Not Talking");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        isTalking = false;
        Debug.Log("out of range");
        
    }

    private void ShowText()
    {
        Vector2 screenPosition = (Vector2)worldCamera.WorldToScreenPoint(transform.position) + textOffset;
        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle( canvasRect, screenPosition, canvasCamera, out Vector2 canvasPosition))
        {
            return;
        }

        if (defaultFont == null)
            defaultFont = TMP_Settings.defaultFontAsset;
        TextMeshProUGUI text = CreateText();
        text.gameObject.SetActive(true);
        text.font = defaultFont;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = textOptions[Random.Range(0, textOptions.Length)];
        text.color = textColor;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.one * 0.5f;
        textRect.anchorMax = Vector2.one * 0.5f;
        textRect.pivot = Vector2.one * 0.5f;
        textRect.sizeDelta = textBoxSize;
        textRect.anchoredPosition = canvasPosition;
        StartCoroutine(AnimateText(text));
        PlayTalkAudio();
    }

    private TextMeshProUGUI CreateText()
    {
        GameObject textObject = new GameObject(
            "NPC Speech Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvas.transform, false);
        return textObject.GetComponent<TextMeshProUGUI>();
    }


    private void PlayTalkAudio()
    {
        

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        audioSource.PlayOneShot(talkSound, audioVolume);
    }

    private IEnumerator AnimateText(TextMeshProUGUI text)
    {
        RectTransform textRect = text.rectTransform;
        Vector2 startPosition = textRect.anchoredPosition;
        Color color = text.color;
        float duration = Mathf.Max(0.01f, textDuration);
        float wobbleDuration = Mathf.Max(0f, spawnWobbleDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float wobbleProgress = wobbleDuration > 0f ? Mathf.Clamp01(elapsed / wobbleDuration) : 1f;
            float wobbleEnvelope = 1f - wobbleProgress;
            float wobbleWave = Mathf.Sin(elapsed * spawnWobbleFrequency * Mathf.PI * 2f);
            float verticalWobble = wobbleWave * spawnWobbleHeight * wobbleEnvelope;
            float rotationWobble = wobbleWave * spawnWobbleRotation * wobbleEnvelope;
            textRect.anchoredPosition = startPosition
                + Vector2.up * (floatHeight * progress + verticalWobble);
            textRect.localRotation = Quaternion.Euler(0f, 0f, rotationWobble);
            color.a = textColor.a * (1f - progress);
            text.color = color;
            yield return null;
        }

        textRect.localRotation = Quaternion.identity;
        text.gameObject.SetActive(false);
        Destroy(text.gameObject);
    }
}
