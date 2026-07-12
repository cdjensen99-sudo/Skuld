using System;
using UnityEngine;

namespace Skuld;

/// <summary>
/// Client-side halo + sound when a skill's debt is fully repaid.
/// </summary>
internal static class DebtClearedFeedback
{
    private const float DebounceSeconds = 0.3f;
    private const float PopSoundVolume = 0.75f;
    private const float FireworkFallbackVolume = 0.45f;
    private const float LevelUpFallbackVolume = 0.85f;

    private static readonly Color HaloGold = new(1f, 0.82f, 0.2f, 1f);
    private static readonly Color HaloDebtRed = new(0.85f, 0.12f, 0.18f, 1f);

    private static float lastPlayTime = -10f;

    internal static void TryPlay(Player player)
    {
        if (!ModConfig.EnableDebtClearedSound.Value || player == null || player != Player.m_localPlayer)
        {
            return;
        }

        if (Time.time - lastPlayTime < DebounceSeconds)
        {
            return;
        }

        lastPlayTime = Time.time;

        Vector3 headPosition = player.GetHeadPoint();
        Quaternion headRotation = player.transform.rotation;
        Transform headParent = player.transform;

        SpawnDebtHalo(player, headPosition, headRotation, headParent);
        if (!TryPlayPopSound(headPosition))
        {
            TryPlayLevelUpSound(player, headPosition);
        }
    }

    private static void SpawnDebtHalo(Player player, Vector3 position, Quaternion rotation, Transform parent)
    {
        EffectList effects = player.m_skillLevelupEffects;
        if (effects?.m_effectPrefabs == null)
        {
            return;
        }

        for (int i = 0; i < effects.m_effectPrefabs.Length; i++)
        {
            EffectList.EffectData entry = effects.m_effectPrefabs[i];
            if (!entry.m_enabled || entry.m_prefab == null || IsAudioOnlyEffect(entry.m_prefab))
            {
                continue;
            }

            Transform attachParent = parent;
            Vector3 spawnPosition = position;
            Quaternion spawnRotation = rotation;
            if (!string.IsNullOrEmpty(entry.m_childTransform) && parent != null)
            {
                Transform child = Utils.FindChild(parent, entry.m_childTransform);
                if (child != null)
                {
                    attachParent = child;
                    spawnPosition = child.position;
                }
            }

            if (attachParent != null && entry.m_inheritParentRotation)
            {
                spawnRotation = attachParent.rotation;
            }

            GameObject instance = UnityEngine.Object.Instantiate(entry.m_prefab, spawnPosition, spawnRotation, attachParent);
            ApplyDebtHaloTint(instance);
        }
    }

    private static bool IsAudioOnlyEffect(GameObject prefab)
    {
        if (prefab.name.StartsWith("sfx_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return prefab.GetComponent<ZSFX>() != null && prefab.GetComponentInChildren<ParticleSystem>(true) == null;
    }

    private static void ApplyDebtHaloTint(GameObject root)
    {
        ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = HaloGold;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateGoldToRedGradient());
        }

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].color = Color.Lerp(lights[i].color, HaloDebtRed, 0.35f);
        }
    }

    private static Gradient CreateGoldToRedGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(HaloGold, 0f),
                new GradientColorKey(Color.Lerp(HaloGold, HaloDebtRed, 0.55f), 0.45f),
                new GradientColorKey(HaloDebtRed, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.7f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static bool TryPlayPopSound(Vector3 position)
    {
        if (TryPlayZsfxPrefab("vfx_HealthUpgrade", position, PopSoundVolume, muteVisuals: true))
        {
            return true;
        }

        return TryPlayZsfxPrefab("sfx_firework_explode", position, FireworkFallbackVolume, muteVisuals: false);
    }

    private static bool TryPlayZsfxPrefab(string prefabName, Vector3 position, float volume, bool muteVisuals)
    {
        if (ZNetScene.instance == null)
        {
            return false;
        }

        GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
        if (prefab == null)
        {
            return false;
        }

        GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        if (muteVisuals)
        {
            MuteVisuals(instance);
        }

        ZSFX zsfx = instance.GetComponentInChildren<ZSFX>(true);
        if (zsfx == null)
        {
            UnityEngine.Object.Destroy(instance);
            return false;
        }

        zsfx.m_playOnAwake = false;
        zsfx.SetVolumeModifier(volume);
        zsfx.Play();
        return true;
    }

    private static void TryPlayLevelUpSound(Player player, Vector3 position)
    {
        EffectList effects = player.m_skillLevelupEffects;
        if (effects?.m_effectPrefabs != null)
        {
            for (int i = 0; i < effects.m_effectPrefabs.Length; i++)
            {
                GameObject prefab = effects.m_effectPrefabs[i].m_prefab;
                if (prefab == null || !IsAudioOnlyEffect(prefab))
                {
                    continue;
                }

                GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                ZSFX zsfx = instance.GetComponent<ZSFX>();
                if (zsfx != null)
                {
                    zsfx.m_playOnAwake = false;
                    zsfx.SetVolumeModifier(LevelUpFallbackVolume);
                    zsfx.Play();
                    return;
                }

                UnityEngine.Object.Destroy(instance);

                ZSFX source = prefab.GetComponent<ZSFX>();
                if (source?.m_audioClips != null && source.m_audioClips.Length > 0)
                {
                    PlayClip(
                        source.m_audioClips[UnityEngine.Random.Range(0, source.m_audioClips.Length)],
                        position,
                        LevelUpFallbackVolume);
                    return;
                }
            }
        }

        TryPlayZsfxPrefab("sfx_levelup", position, LevelUpFallbackVolume, muteVisuals: true);
    }

    private static void PlayClip(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null)
        {
            return;
        }

        GameObject audioObject = new GameObject("skuld_debt_cleared_audio");
        audioObject.transform.position = position;
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 0f;
        source.Play();
        UnityEngine.Object.Destroy(audioObject, clip.length + 0.1f);
    }

    private static void MuteVisuals(GameObject root)
    {
        ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].gameObject.SetActive(false);
        }

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].enabled = false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }
    }
}
