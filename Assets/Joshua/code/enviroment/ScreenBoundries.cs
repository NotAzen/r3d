using System;
using UnityEngine;

public class ScreenBoundries : MonoBehaviour
{
    public enum TransitionStyle
    {
        Slide,
        Fade,
        Wipe,
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera cam;

    [Header("Grid")]
    [SerializeField] private Vector2Int gridSize = new Vector2Int(3, 3);
    [SerializeField] private Vector2Int startRoom = new Vector2Int(1, 1);
    [SerializeField] private bool alignGridToCamera = true;
    [SerializeField] private Vector2 roomSizeOverride = Vector2.zero;

    [Header("Screen Wrap")]
    [SerializeField] private bool wrapX;
    [SerializeField] private bool wrapY;
    [SerializeField, Min(0f)] private float edgePadding = 0f;

    [Header("Edge Camera")]
    [SerializeField] private bool followAtEdges = true;
    [SerializeField] private bool followEdgesX = true;
    [SerializeField, Min(0f)] private float edgeVisibilityX = 4f;
    [SerializeField] private bool followEdgesY = true;
    [SerializeField, Min(0f)] private float edgeVisibilityY = 4f;

    [Header("Transition")]
    [SerializeField] private TransitionStyle style = TransitionStyle.Slide;
    [SerializeField, Min(0f)] private float transitionTime = 0.6f;
    [SerializeField, Range(0f, 2f)] private float slideOvershoot = 0.5f;
    [SerializeField, Min(0f)] private float entryPadding = 1f;
    [SerializeField] private Color overlayColor = Color.black;

    [Header("Freeze During Transition")]
    [SerializeField] private bool pauseGame = true;
    [SerializeField] private bool freezePlayer = true;
    [SerializeField] private Behaviour[] disableDuringTransition;
    //[SerializeField] private WeaponSystem weaponSystem;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawRoomLabels = true;
    [SerializeField] private Color areaFillColor = new Color(0.2f, 1f, 0.4f, 0.05f);
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color startRoomColor = Color.cyan;
    [SerializeField] private Color currentRoomColor = Color.green;
    [SerializeField] private Color solidEdgeColor = Color.red;
    [SerializeField] private Color wrapEdgeColor = Color.magenta;

    public event Action<Vector2Int, Vector2Int> OnRoomChanged;

    private Rigidbody2D playerBody;
    public Vector2Int currentRoom;

    private bool originLocked;
    private Vector2 lockedOrigin;

    private bool transitioning;
    private float transitionT;
    private Vector2 transitionFrom;
    private Vector2 wipeDir = Vector2.right;
    private TransitionStyle activeStyle;

    private bool frozen;
    private Vector3 frozenPos;
    private Vector3 entryPos;
    private float savedTimeScale = 1f;
    private bool pausedByUs;
    private bool lockedWeapons;
    private bool[] wasEnabled;

    public Vector2Int CurrentRoom => currentRoom;
    public bool IsTransitioning => transitioning;

    public Vector2 RoomSize
    {
        get
        {
            if (roomSizeOverride.x > 0f && roomSizeOverride.y > 0f)
                return roomSizeOverride;

            Camera c = cam != null ? cam : Camera.main;
            if (c == null || !c.orthographic) return Vector2.zero;

            float h = c.orthographicSize * 2f;
            return new Vector2(h * c.aspect, h);
        }
    }

    private void Awake()
    {
        if (cam == null) cam = Camera.main;

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        if (player != null) playerBody = player.GetComponent<Rigidbody2D>();

        if (cam != null && !cam.orthographic)
            Debug.LogWarning("[ScreenBoundries] Camera is not orthographic. Set a Room Size Override.", this);
    }

    private void Start()
    {
        Vector2 size = RoomSize;

        if (cam != null && size.x > 0f && size.y > 0f)
        {
            lockedOrigin = GetOrigin(size);
            originLocked = true;
            WarnIfPlayerOutsideGrid(size);
        }

        SnapToPlayer();
    }

    private void OnDisable()
    {
        transitioning = false;
        Unfreeze();
    }

    private void OnValidate()
    {
        gridSize.x = Mathf.Max(1, gridSize.x);
        gridSize.y = Mathf.Max(1, gridSize.y);
        startRoom.x = Mathf.Clamp(startRoom.x, 0, gridSize.x - 1);
        startRoom.y = Mathf.Clamp(startRoom.y, 0, gridSize.y - 1);
        roomSizeOverride.x = Mathf.Max(0f, roomSizeOverride.x);
        roomSizeOverride.y = Mathf.Max(0f, roomSizeOverride.y);
    }

    private void LateUpdate()
    {
        Vector2 size = RoomSize;
        if (player == null || cam == null || size.x <= 0f || size.y <= 0f) return;

        if (transitioning)
        {
            if (!freezePlayer) KeepPlayerInGrid(size, out _);

            UpdateTransition(size);
            return;
        }

        bool wrapped = KeepPlayerInGrid(size, out Vector2 wrapDir);
        UpdateRoom(size, wrapped, wrapDir);

        if (!transitioning)
            SetCameraXY(CameraTarget(size, player.position));
    }

    public void SnapToPlayer()
    {
        Vector2 size = RoomSize;
        if (player == null || cam == null || size.x <= 0f || size.y <= 0f) return;

        currentRoom = RoomAt(player.position, size);
        transitioning = false;
        Unfreeze();
        SetCameraXY(CameraTarget(size, player.position));
    }

    private void WarnIfPlayerOutsideGrid(Vector2 size)
    {
        if (player == null) return;

        Vector2 min = GridMin(size);
        Vector2 max = min + new Vector2(size.x * gridSize.x, size.y * gridSize.y);
        Vector3 p = player.position;

        if (p.x < min.x || p.x > max.x || p.y < min.y || p.y > max.y)
            Debug.LogWarning("[ScreenBoundries] The player starts outside the grid, so it will be pushed " +
                             "back inside. Check Grid Size and Start Room against where the camera and " +
                             "player start. The green area in the Scene view is where the player can go.", this);
    }

    private bool KeepPlayerInGrid(Vector2 size, out Vector2 wrapDir)
    {
        Vector2 min = GridMin(size);
        Vector2 max = min + new Vector2(size.x * gridSize.x, size.y * gridSize.y);

        Vector3 p = player.position;
        bool moved = false;

        float x = Constrain(p.x, min.x, max.x, wrapX, out float wrapDirX, ref moved);
        float y = Constrain(p.y, min.y, max.y, wrapY, out float wrapDirY, ref moved);

        wrapDir = new Vector2(wrapDirX, wrapDirY);

        if (moved)
        {
            p.x = x;
            p.y = y;
            MovePlayer(p);
        }

        return wrapDir != Vector2.zero;
    }

    private float Constrain(float v, float min, float max, bool wrap, out float wrapDir, ref bool moved)
    {
        wrapDir = 0f;

        float lo = wrap ? min : min + edgePadding;
        float hi = wrap ? max : max - edgePadding;

        if (v >= lo && v <= hi) return v;

        moved = true;

        if (wrap)
        {
            wrapDir = v > max ? 1f : -1f;
            return min + Mathf.Repeat(v - min, max - min);
        }

        return Mathf.Clamp(v, lo, hi);
    }

    private void MovePlayer(Vector3 p)
    {
        player.position = p;
        if (playerBody != null) playerBody.position = p;
    }

    private Vector3 EntryPosition(Vector2Int room, Vector2 size)
    {
        Vector3 p = player.position;
        if (entryPadding <= 0f) return p;

        Vector2 c = RoomCenter(room, size);
        float pad = Mathf.Min(entryPadding, Mathf.Min(size.x, size.y) * 0.45f);

        p.x = Mathf.Clamp(p.x, c.x - size.x * 0.5f + pad, c.x + size.x * 0.5f - pad);
        p.y = Mathf.Clamp(p.y, c.y - size.y * 0.5f + pad, c.y + size.y * 0.5f - pad);
        return p;
    }

    private void UpdateRoom(Vector2 size, bool wrapped, Vector2 wrapDir)
    {
        Vector2Int room = RoomAt(player.position, size);
        if (room == currentRoom) return;

        Vector2Int old = currentRoom;
        currentRoom = room;

        Vector3 entry = EntryPosition(room, size);

        if (transitionTime <= 0f)
        {
            MovePlayer(entry);
            SetCameraXY(CameraTarget(size, entry));
        }
        else
        {
            BeginTransition(old, room, wrapped, wrapDir, entry);
        }

        OnRoomChanged?.Invoke(old, room);
    }

    private void BeginTransition(Vector2Int old, Vector2Int room, bool wrapped, Vector2 wrapDir, Vector3 entry)
    {
        activeStyle = (wrapped && style == TransitionStyle.Slide) ? TransitionStyle.Fade : style;

        Vector2Int delta = room - old;
        wipeDir = new Vector2(
            wrapDir.x != 0f ? wrapDir.x : Math.Sign(delta.x),
            wrapDir.y != 0f ? wrapDir.y : Math.Sign(delta.y));
        if (wipeDir == Vector2.zero) wipeDir = Vector2.right;

        transitionFrom = cam.transform.position;
        transitionT = 0f;
        transitioning = true;
        entryPos = entry;

        Freeze();

        if (!freezePlayer) MovePlayer(entry);
    }

    private void UpdateTransition(Vector2 size)
    {
        Vector2 target = CameraTarget(size, entryPos);

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        transitionT += dt / Mathf.Max(0.0001f, transitionTime);
        float t = Mathf.Clamp01(transitionT);

        if (activeStyle == TransitionStyle.Slide)
        {
            float k = EaseInOutBack(t, slideOvershoot);
            SetCameraXY(Vector2.LerpUnclamped(transitionFrom, target, k));
        }
        else if (t >= 0.5f)
        {
            SetCameraXY(target);
        }

        if (freezePlayer)
            MovePlayer(Vector3.Lerp(frozenPos, entryPos, Mathf.SmoothStep(0f, 1f, t)));

        if (transitionT >= 1f)
        {
            transitioning = false;
            SetCameraXY(target);
            if (freezePlayer) MovePlayer(entryPos);
            Unfreeze();
        }
    }

    private static float EaseInOutBack(float t, float overshoot)
    {
        float c1 = overshoot * 1.70158f;
        float c2 = c1 * 1.525f;

        return t < 0.5f
            ? (Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2)) / 2f
            : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
    }

    private void Freeze()
    {
        if (frozen) return;
        frozen = true;

        if (player != null)
            frozenPos = player.position;

        if (pauseGame && Time.timeScale > 0f)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByUs = true;
        }

        if (disableDuringTransition != null)
        {
            wasEnabled = new bool[disableDuringTransition.Length];

            for (int i = 0; i < disableDuringTransition.Length; i++)
            {
                Behaviour b = disableDuringTransition[i];
                if (b == null) continue;

                wasEnabled[i] = b.enabled;
                b.enabled = false;
            }
        }

        //if (weaponSystem != null && !weaponSystem.inputLocked)
       // {
       //     weaponSystem.inputLocked = true;
       //     lockedWeapons = true;
       // }
    }

    private void Unfreeze()
    {
        if (!frozen) return;
        frozen = false;

        if (pausedByUs)
        {
            if (Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = savedTimeScale;

            pausedByUs = false;
        }

        if (disableDuringTransition != null && wasEnabled != null)
        {
            for (int i = 0; i < disableDuringTransition.Length && i < wasEnabled.Length; i++)
            {
                Behaviour b = disableDuringTransition[i];
                if (b != null && wasEnabled[i]) b.enabled = true;
            }
        }

       // if (lockedWeapons)
       // {
           // if (weaponSystem != null) weaponSystem.inputLocked = false;
       ///     lockedWeapons = false;
       // }
    }

    private Vector2 CameraTarget(Vector2 size, Vector2 playerPos)
    {
        Vector2 center = RoomCenter(currentRoom, size);
        if (!followAtEdges) return center;

        if (followEdgesX && !wrapX)
            center.x = EdgeShift(center.x, playerPos.x, size.x, edgeVisibilityX,
                currentRoom.x == 0, currentRoom.x == gridSize.x - 1);

        if (followEdgesY && !wrapY)
            center.y = EdgeShift(center.y, playerPos.y, size.y, edgeVisibilityY,
                currentRoom.y == 0, currentRoom.y == gridSize.y - 1);

        return center;
    }

    private float EdgeShift(float roomCenter, float p, float roomSize, float visibility, bool atLow, bool atHigh)
    {
        float half = roomSize * 0.5f;
        float v = Mathf.Min(visibility, half);

        if (atLow) roomCenter = Mathf.Min(roomCenter, p - v + half);
        if (atHigh) roomCenter = Mathf.Max(roomCenter, p + v - half);

        return roomCenter;
    }

    private void SetCameraXY(Vector2 xy)
    {
        Vector3 p = cam.transform.position;
        cam.transform.position = new Vector3(xy.x, xy.y, p.z);
    }

    private Vector2 GetOrigin(Vector2 size)
    {
        if (!alignGridToCamera) return transform.position;
        if (Application.isPlaying && originLocked) return lockedOrigin;

        Camera c = cam != null ? cam : Camera.main;
        Vector2 camPos = c != null ? (Vector2)c.transform.position : (Vector2)transform.position;
        return camPos - new Vector2(startRoom.x * size.x, startRoom.y * size.y);
    }

    private Vector2 GridMin(Vector2 size) => GetOrigin(size) - size * 0.5f;

    private Vector2 RoomCenter(Vector2Int room, Vector2 size) => RoomCenter(room, size, GetOrigin(size));

    private static Vector2 RoomCenter(Vector2Int room, Vector2 size, Vector2 origin) =>
        origin + new Vector2(room.x * size.x, room.y * size.y);

    private Vector2Int RoomAt(Vector2 pos, Vector2 size)
    {
        Vector2 o = GetOrigin(size);
        int x = Mathf.FloorToInt((pos.x - o.x + size.x * 0.5f) / size.x);
        int y = Mathf.FloorToInt((pos.y - o.y + size.y * 0.5f) / size.y);

        return new Vector2Int(
            Mathf.Clamp(x, 0, gridSize.x - 1),
            Mathf.Clamp(y, 0, gridSize.y - 1));
    }

    private void OnGUI()
    {
        if (!transitioning || activeStyle == TransitionStyle.Slide) return;
        if (Event.current.type != EventType.Repaint) return;

        float t = Mathf.Clamp01(transitionT);
        float w = Screen.width;
        float h = Screen.height;

        bool covering = t < 0.5f;
        float p = Mathf.SmoothStep(0f, 1f, covering ? t * 2f : (t - 0.5f) * 2f);

        Rect rect;
        float alpha = overlayColor.a;

        if (activeStyle == TransitionStyle.Fade)
        {
            rect = new Rect(0f, 0f, w, h);
            alpha *= covering ? p : 1f - p;
        }
        else
        {
            rect = WipeRect(covering, p, w, h);
        }

        GUI.depth = -1000;
        GUI.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, alpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private Rect WipeRect(bool covering, float p, float w, float h)
    {
        bool horizontal = Mathf.Abs(wipeDir.x) >= Mathf.Abs(wipeDir.y);

        if (horizontal)
        {
            bool right = wipeDir.x >= 0f;

            if (covering)
                return right ? new Rect(0f, 0f, w * p, h) : new Rect(w * (1f - p), 0f, w * p, h);

            return right ? new Rect(w * p, 0f, w * (1f - p), h) : new Rect(0f, 0f, w * (1f - p), h);
        }

        bool up = wipeDir.y > 0f;

        if (covering)
            return up ? new Rect(0f, h * (1f - p), w, h * p) : new Rect(0f, 0f, w, h * p);

        return up ? new Rect(0f, 0f, w, h * (1f - p)) : new Rect(0f, h * p, w, h * (1f - p));
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Vector2 size = RoomSize;
        if (size.x <= 0f || size.y <= 0f) return;

        Vector2 origin = GetOrigin(size);
        Vector2 min = origin - size * 0.5f;
        Vector3 areaSize = new Vector3(size.x * gridSize.x, size.y * gridSize.y, 0.01f);
        Vector3 areaCenter = new Vector3(min.x + areaSize.x * 0.5f, min.y + areaSize.y * 0.5f, 0f);

        Gizmos.color = areaFillColor;
        Gizmos.DrawCube(areaCenter, areaSize);

        Vector3 roomBox = new Vector3(size.x, size.y, 0.01f);

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                bool isCurrent = Application.isPlaying && currentRoom.x == x && currentRoom.y == y;
                bool isStart = !Application.isPlaying && startRoom.x == x && startRoom.y == y;

                Gizmos.color = isCurrent ? currentRoomColor : isStart ? startRoomColor : gridColor;

                Vector3 center = RoomCenter(new Vector2Int(x, y), size, origin);
                Gizmos.DrawWireCube(center, roomBox);
                Gizmos.DrawLine(center + Vector3.left * 0.25f, center + Vector3.right * 0.25f);
                Gizmos.DrawLine(center + Vector3.down * 0.25f, center + Vector3.up * 0.25f);


                //if (drawRoomLabels)
                //{
                //    UnityEditor.Handles.Label(
                //        center + new Vector3(-size.x * 0.5f + 0.3f, size.y * 0.5f - 0.3f, 0f),
                //        "(" + x + ", " + y + ")");
                //}

            }
        }

        Vector3 bl = new Vector3(min.x, min.y, 0f);
        Vector3 tr = bl + new Vector3(areaSize.x, areaSize.y, 0f);
        Vector3 tl = new Vector3(bl.x, tr.y, 0f);
        Vector3 br = new Vector3(tr.x, bl.y, 0f);

        Gizmos.color = wrapX ? wrapEdgeColor : solidEdgeColor;
        Gizmos.DrawLine(bl, tl);
        Gizmos.DrawLine(br, tr);

        Gizmos.color = wrapY ? wrapEdgeColor : solidEdgeColor;
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(bl, br);
    }
}