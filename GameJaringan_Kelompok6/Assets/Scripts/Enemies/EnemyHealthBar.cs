using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Health bar musuh: World Space Canvas di atas kepala musuh, menghadap kamera.
/// Update otomatis mengikuti sisa HP musuh (NetworkVariable hp, jadi update
/// benar di server maupun client) sejak terkena hit pertama.
///
/// SETUP OTOMATIS (direkomendasikan):
/// Cukup pasang komponen ini di prefab Enemy — Canvas + 2 Image (bg & fill)
/// dibuat otomatis saat Awake. Tidak perlu setup canvas manual.
///
/// SETUP MANUAL di Inspector (opsional, kalau mau custom visual):
/// 1. Buat child GameObject "HealthBar" di prefab Enemy.
/// 2. Tambahkan Canvas  -> Render Mode = World Space
///    (atur: Rect size ~ (1.4, 0.2), posisi di atas kepala, mis. Y = 1.1).
/// 3. Tambahkan CanvasRenderer + Image "Background" (warna gelap).
/// 4. Tambahkan child "Fill" dengan Image (warna hijau/merah).
/// 5. Drag "HealthBar" ke field worldSpaceCanvas, "Fill" ke field fillImage.
/// 6. Tambangkan komponen ini tetap menyala untuk update & billboard.
///
/// Catatan fisika prefab: Capsule 3D hanya VISUAL (MeshFilter+MeshRenderer);
/// collider yang dipakai adalah 2D (Circle/BoxCollider2D) — lihat komentar
/// di EnemyController.cs.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Manual Setup (kosongkan untuk auto-create)")]
    [SerializeField] private Canvas worldSpaceCanvas;
    [SerializeField] private Image fillImage;

    [Header("Auto Setup Settings")]
    [Tooltip("Offset bar di atas kepala musuh (satuan DUNIA, dihitung setelah skala prefab).")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 1.35f, 0f);
    [Tooltip("Ukuran bar dalam satuan dunia (lebar, tinggi).")]
    [SerializeField] private Vector2 barSize = new Vector2(1.6f, 0.22f);
    [Tooltip("Transparansi background bar (0 = terlihat musuh, 1 = solid).")]
    [SerializeField] private Color backgroundColor = new Color(0.06f, 0.06f, 0.06f, 0.85f);
    [SerializeField] private Color fillColor = new Color(0.25f, 1f, 0.3f, 1f);

    private EnemyController enemy;
    private RectTransform fillRect;
    private Camera mainCamera;

    private void Awake()
    {
        enemy = GetComponentInParent<EnemyController>();
        mainCamera = Camera.main;

        if (worldSpaceCanvas == null || fillImage == null)
        {
            CreateDefaultHealthBar();
        }

        fillRect = fillImage.rectTransform;
        UpdateBar(enemy.MaxHp, enemy.MaxHp); // mulai penuh

        // Hide saat musuh mati (canvas child ikut terhancurkan saat despawn)
        enemy.OnDied += HandleEnemyDied;
    }

    private void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.OnDied -= HandleEnemyDied;
            enemy.hp.OnValueChanged -= HandleHpChanged; // jaga-jaga (NetworkBehaviour despawn)
        }
    }

    private void LateUpdate()
    {
        // Billboard: selalu menghadap kamera (world-space canvas)
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        if (worldSpaceCanvas != null)
        {
            worldSpaceCanvas.transform.forward = mainCamera.transform.forward;
        }
    }

    private void OnEnable()
    {
        if (enemy != null)
        {
            enemy.hp.OnValueChanged += HandleHpChanged;
            UpdateBar(enemy.hp.Value, enemy.MaxHp);
        }
    }

    private void HandleHpChanged(int previousValue, int newValue)
    {
        UpdateBar(newValue, enemy.MaxHp);
    }

    private void HandleEnemyDied(EnemyController diedEnemy)
    {
        enemy.hp.OnValueChanged -= HandleHpChanged;

        if (worldSpaceCanvas != null)
        {
            worldSpaceCanvas.enabled = false; // sembunyikan bar saat mati
        }
    }

    private void UpdateBar(int currentHp, int maxHp)
    {
        if (fillRect == null) return;

        float ratio = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;

        // Fill horizontal: anchorMax.x = rasio HP (0..1)
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(ratio, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Perubahan warna hijau -> merah sebagai indikator tambahan
        if (fillImage != null)
        {
            fillImage.color = Color.Lerp(Color.red, fillColor, ratio);
        }
    }

    // --- Konstruksi UI default (World Space Canvas + Background + Fill) ---
    private void CreateDefaultHealthBar()
    {
        GameObject canvasObject = new GameObject("HealthBarCanvas");

        // BUGFIX: RectTransform harus ditambahkan SEBELUM localPosition diatur.
        // (Menambahkan RectTransform menggantikan Transform lama dan
        //  mereset posisi - kalau posisi diset dulu, bar muncul di TENGAH musuh.)
        RectTransform canvasRect = canvasObject.AddComponent<RectTransform>();
        canvasObject.transform.SetParent(transform, false);

        // Offset ditafsirkan dalam SATUAN DUNIA lalu dibagi skala induk, sehingga
        // bar tetap melayang jelas DI ATAS kepala meski prefab di-scale (0.8).
        Vector3 parentScale = transform.lossyScale;
        Vector3 localOffset = new Vector3(
            positionOffset.x / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
            positionOffset.y / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)),
            positionOffset.z / Mathf.Max(0.01f, Mathf.Abs(parentScale.z)));
        canvasObject.transform.localPosition = localOffset;

        worldSpaceCanvas = canvasObject.AddComponent<Canvas>();
        worldSpaceCanvas.renderMode = RenderMode.WorldSpace;
        worldSpaceCanvas.sortingOrder = 10; // tampil di atas mesh musuh

        // Kompensasi skala induk: ukuran bar selalu == barSize dalam satuan dunia
        float maxScale = Mathf.Max(
            Mathf.Abs(parentScale.x),
            Mathf.Abs(parentScale.y),
            Mathf.Abs(parentScale.z));
        canvasRect.localScale = Vector3.one * (1f / Mathf.Max(0.01f, maxScale));
        canvasRect.sizeDelta = barSize;

        Image background = CreateBarImage(canvasRect, "Background", backgroundColor);
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;

        Image fill = CreateBarImage(background.rectTransform, "Fill", fillColor);
        fillRect = fill.rectTransform;
        fillImage = fill;
    }

    private static Image CreateBarImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.rectTransform.localScale = Vector3.one;
        return image;
    }
}
