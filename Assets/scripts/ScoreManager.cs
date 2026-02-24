using UnityEngine;
using System;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] float basePointsPerSecond = 1.5f, baseSpeedMultiplier = 1f, maxForwardSpeedForBase = 40f;

    [Header("Drift")]
    [SerializeField] float peakSharpness = 60f, sharpnessExponent = 0.75f, timeScale = 2f;
    [SerializeField] float minSharpnessForScoring = 3f, minLateralSpeed = 0.5f, minForwardSpeed = 1f;
    [Tooltip("Multiplier UI")]
    public float driftMultiplierRate = 0.6f;
    [SerializeField] float maxDriftMultiplier = 10f;

    [Header("Bonus")]
    [SerializeField] float minDriftBonus = 300f, midDriftBonus = 2000f, maxDriftBonus = 7500f, grassMaxBonus = 350f;
    [SerializeField, Range(0f, 1f)] float midTierThreshold = 0.4f;
    [SerializeField] float bonusApplyDuration = 1f;
    [SerializeField] bool debugScoreBreakdown;

    [Header("UI")]
    [SerializeField] MultCounter multCounter;
    [SerializeField] AudioSource driftMultLost;
    public TextMeshProUGUI TimeScoreText, TotalScoreText, DriftScoreText;

    public static ScoreManager instance;
    public event Action<int> OnScoreChanged;
    public float CurrentDriftMultiplier => isDriftingActive ? driftCompoundMultiplier : 0f;

    PlayerCarController carController;
    RacerScript racerScript;

    [SerializeField] float scoreFloat, driftTime, driftCompoundMultiplier = 0.65f;
    float pendingDriftBonusTotal, bonusApplyProgress, bonusAddedSoFar;
    float driftSessionBaseGain, driftStartScore, scoreMultiplier = 1f;
    float TimeStartPoint = 15000f, RaceTimer;
    const float PointsDecayTime = 300f;
    int lastReportedScore = -1, driftCount;
    bool isOnGrass, isDriftingActive, touchedGrassWhileDrifting, isApplyingBonus;

    void Awake() => instance = this;

    void Start()
    {
        carController = FindFirstObjectByType<PlayerCarController>();
        racerScript  = FindFirstObjectByType<RacerScript>();
        multCounter  ??= FindFirstObjectByType<MultCounter>();
    }

    void Update()
    {
        float dt  = Time.deltaTime;
        Vector3 vel = carController?.CarRb != null ? carController.CarRb.linearVelocity : Vector3.zero;

        if (racerScript != null)
        {
            if (racerScript.racestarted && !racerScript.raceFinished)
            {
                RaceTimer += dt;
                TimeStartPoint = Mathf.Max(0f, 15000f * (1f - RaceTimer / PointsDecayTime));
            }
            if (racerScript.raceFinished) ShowScores();
        }

        UpdateBaseScore(dt, vel);
        UpdateDriftState(dt, vel);
        AnimatePendingBonus(dt);

        int s = Mathf.FloorToInt(scoreFloat);
        if (s != lastReportedScore) { lastReportedScore = s; OnScoreChanged?.Invoke(s); }
    }



    public void ShowScores()
    {
        TotalScoreText.text = "Final Score: " + GetScoreInt();
        TimeScoreText.text  = "Time: " + Mathf.FloorToInt(TimeStartPoint);
        DriftScoreText.text = "Drift: " + Mathf.FloorToInt(scoreFloat);
    }

    public int GetScoreInt()  => racerScript != null && racerScript.raceFinished
        ? Mathf.FloorToInt(scoreFloat + TimeStartPoint)
        : Mathf.FloorToInt(scoreFloat);
    public float GetDriftTime() => driftTime;
    public void  SetScoreMultiplier(float m) => scoreMultiplier = m;

    public void SetOnGrass(bool grassContact)
    {
        isOnGrass = grassContact;
        if (isOnGrass && isDriftingActive && driftCompoundMultiplier > 1.01f)
        {
            touchedGrassWhileDrifting = true;
            Debug.Log($"[ScoreManager] Grass hit - mult: x{driftCompoundMultiplier:F2}, time: {driftTime:F2}s");
            multCounter.UpdateMultiplierText(1f);
            driftMultLost.Play();
        }
    }


    void UpdateBaseScore(float dt, Vector3 vel)
    {
        if (isOnGrass || racerScript == null || racerScript.raceFinished) return;
        float fwd   = Mathf.Max(0f, Vector3.Dot(vel, carController.transform.forward));
        float factor = Mathf.Clamp01(fwd / Mathf.Max(0.0001f, maxForwardSpeedForBase));
        scoreFloat += basePointsPerSecond * (1f + factor * baseSpeedMultiplier) * scoreMultiplier * dt;
    }

    void UpdateDriftState(float deltatime, Vector3 velocity)
    {
        bool canDrift = !isOnGrass && carController != null && carController.IsDrifting;
        if (canDrift)
        {
            if (!isDriftingActive) StartDrift();
            float multiplier = ComputeDriftMultiplierIncrement(velocity, deltatime);
            if (multiplier > 0f)
            {
                float target = driftCompoundMultiplier * Mathf.Pow(multiplier, deltatime * driftMultiplierRate);
                float lerpFactor = Mathf.Clamp01(deltatime * driftMultiplierRate * 50f); //if you dont like how slowly the multiplier goes, ramp it up to 75f, or 100, anything over that becomes a child's play
                driftCompoundMultiplier = Mathf.Min(Mathf.Lerp(driftCompoundMultiplier, target, lerpFactor), maxDriftMultiplier);
                driftSessionBaseGain    += basePointsPerSecond * deltatime;
            }
        }
        else if (isDriftingActive) EndDrift();
    }

    void StartDrift()
    {
        isDriftingActive = true;  driftStartScore = scoreFloat;
        driftSessionBaseGain = 0f; driftCompoundMultiplier = 0.93f;
        driftTime = 0f; touchedGrassWhileDrifting = false;
        multCounter?.StartMultiplier(1f, 0f, 1);
    }

    void EndDrift()
    {
        ApplyDriftBonus();
        isDriftingActive = false; driftTime = 0f;
        driftCompoundMultiplier = 1f; driftSessionBaseGain = 0f;
        multCounter?.ResetMultiplier(); // straight to 0 bcs Project manager >:(
    }

    void AnimatePendingBonus(float dt)
    {
        if (!isApplyingBonus || pendingDriftBonusTotal <= 0f) return;
        bonusApplyProgress = Mathf.Clamp01(bonusApplyProgress + (bonusApplyDuration <= 0f ? 1f : dt / bonusApplyDuration));
        float add = pendingDriftBonusTotal * bonusApplyProgress - bonusAddedSoFar;
        if (add > 0f) { scoreFloat += add; bonusAddedSoFar += add; }
        if (bonusApplyProgress >= 1f) { isApplyingBonus = false; pendingDriftBonusTotal = bonusApplyProgress = bonusAddedSoFar = 0f; }
    }

    float ComputeDriftMultiplierIncrement(Vector3 vel, float dt)
    {
        if (dt <= 0f || carController?.CarRb == null) return 0f;
        if (vel.magnitude < minForwardSpeed) return 0f;
        if (Mathf.Abs(Vector3.Dot(vel, carController.transform.right)) < minLateralSpeed) return 0f;
        float sharpness = Mathf.Abs(carController.GetDriftSharpness());
        if (sharpness < minSharpnessForScoring) return 0f;

        driftTime += dt;
        float sharpBonus = Mathf.Pow(Mathf.InverseLerp(minSharpnessForScoring, peakSharpness, sharpness), sharpnessExponent);
        float fwd = Mathf.Max(0f, Vector3.Dot(vel, carController.transform.forward));
        float speedFactor = Mathf.Clamp01(fwd / Mathf.Max(0.5f, maxForwardSpeedForBase));
        return (1f + sharpBonus + driftTime / timeScale) * (1f + speedFactor);
    }

    void ApplyDriftBonus()
    {
        touchedGrassWhileDrifting = false;
        if (driftTime <= 0.2f || driftCompoundMultiplier <= 1.01f) return;
        driftCount++;
        float intensity     = Mathf.InverseLerp(1f, maxDriftMultiplier, driftCompoundMultiplier);
        float combinedQuality = Mathf.Clamp01(0.5f * intensity + 0.5f * driftTime / 3f);
        float bonus         = CalculateDriftBonus(combinedQuality) * 0.65f * scoreMultiplier;
        pendingDriftBonusTotal = bonus; isApplyingBonus = bonus > 0f; bonusApplyProgress = bonusAddedSoFar = 0f;
        if (debugScoreBreakdown)
            Debug.Log($"[DRIFT #{driftCount}] {(combinedQuality < midTierThreshold ? "LOW-MID" : "MID-HIGH")} | dur:{driftTime:F2}s mult:x{driftCompoundMultiplier:F2} quality:{combinedQuality:P0} bonus:+{bonus:N0}");
    }

    float CalculateDriftBonus(float score)
    {
        if (touchedGrassWhileDrifting) return Mathf.Lerp(0f, grassMaxBonus, score);
        return score < midTierThreshold
            ? Mathf.Lerp(minDriftBonus, midDriftBonus, score / midTierThreshold)
            : Mathf.Lerp(midDriftBonus, maxDriftBonus, (score - midTierThreshold) / (1f - midTierThreshold));
    }
}