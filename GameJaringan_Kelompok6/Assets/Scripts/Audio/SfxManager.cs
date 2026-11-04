using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SfxManager: pusat pemutaran SFX satu-tempat (singleton).
///
/// - Punya pool AudioSource agar tidak membuat/menghancurkan objek tiap suara.
/// - Dipanggil dari mana saja: SfxManager.Instance.PlayAtPoint(clip, posisi);
/// - Script lain (PlayerController, EnemyController) memanggil ini lewat
///   ClientRpc sehingga suara terdengar identik di semua Client.
///
/// Setup di Inspector: taruh di GameObject root scene (semua field cukup default).
/// </summary>
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [Header("SFX Settings")]
    [Tooltip("Volume dasar seluruh SFX.")]
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 1f;

    private readonly List<AudioSource> audioSourcePool = new List<AudioSource>();

    private void Awake()
    {
        // Pola singleton yang sama dengan GameManager / ScoreManager
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Memainkan satu clip SFX pada posisi dunia tertentu.
    /// Aman dipanggil meski clip / Instance masih null (sekadar skip).
    /// </summary>
    public void PlayAtPoint(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetPooledSource();
        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Clamp01(defaultVolume * volumeScale);
        source.pitch = 1f;
        source.Play();
    }

    // Ambil AudioSource menganggur dari pool, atau buat baru jika penuh
    private AudioSource GetPooledSource()
    {
        for (int i = 0; i < audioSourcePool.Count; i++)
        {
            if (!audioSourcePool[i].isPlaying)
            {
                return audioSourcePool[i];
            }
        }

        GameObject sourceObject = new GameObject("SFX_Source_" + audioSourcePool.Count);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        audioSourcePool.Add(source);
        return source;
    }
}
