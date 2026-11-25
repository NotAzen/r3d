using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class basicNPC : MonoBehaviour
{
    private static int nextConversationOrder;

    [Header("Text Options")]
    [SerializeField, TextArea] private string[] textOptions =
    {
        "Option 1",
        "Option 2",
        "Option 3"
    };
    [SerializeField, TextArea] private string[] npcTextOptions =
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

    [Header("Patrol")]
    [SerializeField, Min(0f)] private float patrolSpeed = 1f;
    [SerializeField, Range(1f, 50f)] private float minPatrolDistance = 1f;
    [SerializeField, Range(1f, 50f)] private float maxPatrolDistance = 50f;
    [SerializeField, Min(0f)] private float idleDuration = 1f;
    [SerializeField] private LayerMask groundLayers = 1 << 6;
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.2f, 1f);
    [SerializeField, Min(0f)] private float wallCheckDistance = 0.2f;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.2f, 0.15f);
    [SerializeField, Min(0f)] private float edgeCheckOffset = 0.3f;

    [Header("References")]
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Canvas canvas;
    private readonly HashSet<Collider2D> playersInRange = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> activeTalkers = new HashSet<Collider2D>();
    private bool isTalking;
    private bool isNpcConversation;
    private bool isIdling;
    private int patrolDirection = 1;
    private int conversationOrder;
    private float patrolDistance;
    private float patrolStartX;
    private float idleTimeRemaining;
    private Camera worldCamera;
    private RectTransform canvasRect;
    public TMP_FontAsset defaultFont;
    private void Awake()
    {
        conversationOrder = nextConversationOrder++;
        body = GetComponent<Rigidbody2D>();
        body.freezeRotation = true;
        spriteRenderer = GetComponent<SpriteRenderer>();
        patrolStartX = body.position.x;
        minPatrolDistance = Mathf.Clamp(minPatrolDistance, 1f, 50f);
        maxPatrolDistance = Mathf.Clamp(maxPatrolDistance, minPatrolDistance, 50f);
        patrolDistance = Random.Range(minPatrolDistance, maxPatrolDistance);
        UpdateSpriteFacing();
        canvas = FindAnyObjectByType<Canvas>();
        canvasRect = canvas.GetComponent<RectTransform>();
        worldCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        defaultFont = TMP_Settings.defaultFontAsset;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (!playersInRange.Add(collision))
            {
                return;
            }

            if (Random.Range(0, 3) != 0)
            {
                return;
            }

            activeTalkers.Add(collision);
            isTalking = true;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            ShowText(textOptions);
            Debug.Log("Talking");
        }
        else if (collision.CompareTag("NPC"))
        {
            basicNPC otherNpc = collision.GetComponentInParent<basicNPC>();
            if (otherNpc == null || otherNpc == this || !activeTalkers.Add(collision))
            {
                return;
            }

            isTalking = true;
            if (conversationOrder < otherNpc.conversationOrder
                && !isNpcConversation && !otherNpc.isNpcConversation)
            {
                StartCoroutine(NpcConversation(otherNpc));
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        bool wasTalkingToPlayer = collision.CompareTag("Player") && activeTalkers.Remove(collision);
        playersInRange.Remove(collision);
        if (!wasTalkingToPlayer && !activeTalkers.Remove(collision))
        {
            return;
        }

        isTalking = activeTalkers.Count > 0;
        if (wasTalkingToPlayer)
        {
            isIdling = false;
            idleTimeRemaining = 0f;
        }

        Debug.Log("out of range");
    }

    private void FixedUpdate()
    {
        if (isTalking || isNpcConversation)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            return;
        }

        if (isIdling)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            idleTimeRemaining -= Time.fixedDeltaTime;
            if (idleTimeRemaining <= 0f)
            {
                isIdling = false;
            }

            return;
        }

        float targetX = patrolStartX + patrolDirection * patrolDistance;
        if (HasWallAhead() || !HasGroundAhead()
            || Mathf.Abs(body.position.x - targetX) <= patrolSpeed * Time.fixedDeltaTime)
        {
            ReverseAndIdle();
            return;
        }

        body.linearVelocity = new Vector2(patrolDirection * patrolSpeed, body.linearVelocity.y);
        UpdateSpriteFacing();
    }

    private bool HasWallAhead()
    {
        Vector2 origin = GetCheckOrigin();
        origin.x += patrolDirection * ((spriteRenderer != null ? spriteRenderer.bounds.extents.x : 0f)
            + wallCheckDistance);
        origin.y += spriteRenderer != null ? spriteRenderer.bounds.extents.y : wallCheckSize.y * 0.5f;
        return Physics2D.BoxCast(
            origin,
            wallCheckSize,
            0f,
            Vector2.right * patrolDirection,
            wallCheckDistance,
            groundLayers);
    }

    private bool HasGroundAhead()
    {
        Vector2 origin = GetCheckOrigin();
        origin.x += patrolDirection * ((spriteRenderer != null ? spriteRenderer.bounds.extents.x : 0f)
            + edgeCheckOffset);
        return Physics2D.OverlapBox(origin, groundCheckSize, 0f, groundLayers) != null;
    }

    private Vector2 GetCheckOrigin()
    {
        if (spriteRenderer != null)
        {
            return new Vector2(spriteRenderer.bounds.center.x, spriteRenderer.bounds.min.y);
        }

        return body.position;
    }

    private void ReverseAndIdle()
    {
        patrolDirection *= -1;
        idleTimeRemaining = idleDuration;
        isIdling = idleDuration > 0f;
        UpdateSpriteFacing();
    }

    private void UpdateSpriteFacing()
    {
        if (spriteRenderer != null)
        {
            Vector3 scale = spriteRenderer.transform.localScale;
            scale.x = Mathf.Sign(patrolDirection) * Mathf.Abs(scale.x);
            spriteRenderer.transform.localScale = scale;
        }
    }

    private void ShowText(string[] options)
    {

        ShowText(options[Random.Range(0, options.Length)]);
    }

    private void ShowText(string line)
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
        text.text = line;
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

    private IEnumerator NpcConversation(basicNPC otherNpc)
    {
        isNpcConversation = true;
        otherNpc.isNpcConversation = true;
        isTalking = true;
        otherNpc.isTalking = true;

        int conversationLineCount = Random.Range(3, 13);
        for (int lineIndex = 0; lineIndex < conversationLineCount; lineIndex++)
        {
            basicNPC speaker = lineIndex % 2 == 0 ? this : otherNpc;
            if (speaker.npcTextOptions != null && speaker.npcTextOptions.Length > 0)
            {
                string line = speaker.npcTextOptions[Random.Range(0, speaker.npcTextOptions.Length)];
                speaker.ShowText(line);
            }
            else
            {
                Debug.LogWarning("No NPC dialogue options are configured.", speaker);
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, speaker.textDuration));
        }

        bool thisGoesLeft = body.position.x < otherNpc.body.position.x
            || (Mathf.Approximately(body.position.x, otherNpc.body.position.x)
                && conversationOrder < otherNpc.conversationOrder);
        FinishNpcConversation(thisGoesLeft ? -1 : 1);
        otherNpc.FinishNpcConversation(thisGoesLeft ? 1 : -1);
    }

    private void FinishNpcConversation(int direction)
    {
        isNpcConversation = false;
        activeTalkers.RemoveWhere(collider => collider != null && collider.CompareTag("NPC"));
        isTalking = activeTalkers.Count > 0;
        isIdling = false;
        idleTimeRemaining = 0f;
        patrolDirection = direction;
        patrolStartX = body.position.x;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        UpdateSpriteFacing();
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
