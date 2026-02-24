using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultCounter : MonoBehaviour
{
    public Sprite[] numberSprites;
    public Image displayImage;
    public TextMeshProUGUI multiplierText;
    
    private bool isDrifting, isLooping, isCoolingDown, useFullCooldown;
    private float loopTimer, updateTimer, qualityTimer, cooldownTimer;
    private int currentFrame;

    void Start()
    {
        displayImage.sprite = numberSprites[0];
        UpdateMultiplierText(1f);
    }

    void Update()
    {
        Cooldown();
        Loop();


    }

    public void Cooldown()
    {
        if (!isCoolingDown) return;

        cooldownTimer += Time.deltaTime;
        if (cooldownTimer < 0.1f) return;
        cooldownTimer = 0f;

        currentFrame += useFullCooldown ? 1 : -1;
        bool finished = useFullCooldown ? currentFrame > 22 : currentFrame < 0;
        isCoolingDown = isCoolingDown && !finished;

        currentFrame = Mathf.Clamp(currentFrame, 0, numberSprites.Length - 1);
        displayImage.sprite = finished ? numberSprites[0] : numberSprites[currentFrame];
    }

    public void Loop()
    {
        if (isCoolingDown) return;
        if (!isDrifting)
        {
            displayImage.sprite = numberSprites[0];
            UpdateMultiplierText(1f);
            return;
        }

        float mult = ScoreManager.instance.CurrentDriftMultiplier;
        UpdateMultiplierText(Mathf.RoundToInt(mult));

        qualityTimer += Time.deltaTime;
        if (qualityTimer >= 0.2f)
        {
            qualityTimer = 0f;
            bool startLoop = mult >= 7f && !isLooping;
            bool stopLoop = mult < 6.5f && isLooping;
            isLooping = startLoop ? true : stopLoop ? false : isLooping;
            if (startLoop) { currentFrame = 10; loopTimer = 0f; }
        }

        if (isLooping && mult >= 7f)
        {
            loopTimer += Time.deltaTime;
            if (loopTimer >= 0.15f)
            {
                loopTimer = 0f;
                currentFrame = currentFrame >= 12 ? 10 : currentFrame + 1;
                displayImage.sprite = numberSprites[currentFrame];
            }
            return;
        }

        updateTimer += Time.deltaTime;
        if (updateTimer < 0.1f) return;
        updateTimer = 0f;
        //if you delete the parentheses, the multcounter fire animation wont work correctly, why? i dont know
        int idx = mult <= 1f ? 0 : (mult >= 7f ? 9 : Mathf.RoundToInt(((mult - 1f) / 6f) * 9));
        displayImage.sprite = numberSprites[idx];
    }

    public void UpdateMultiplierText(float mult)
    {
        multiplierText.text = $"{mult}";
        multiplierText.color = mult >= 7f ? Color.red : mult >= 4f ? new Color(1f, 0.5f, 0f) : mult >= 2f ? Color.yellow : Color.white;
    }

    public void StartMultiplier(float multiplier, float quality, int mulplierInInt)
    {
        isDrifting = true;
        isCoolingDown = false;
        UpdateMultiplierText(Mathf.RoundToInt(multiplier));
    }

    // its the great reset of 2026 
    public void ResetMultiplier()
    {
        isDrifting = isLooping = false;
        isCoolingDown = true;
        cooldownTimer = 0f;
        UpdateMultiplierText(1f);

        int idx = System.Array.FindIndex(numberSprites, s => s == displayImage.sprite);
        idx = Mathf.Clamp(idx, 0, numberSprites.Length - 1);
        useFullCooldown = idx >= 10 && idx <= 12;
        currentFrame = useFullCooldown ? 13 : idx;
        displayImage.sprite = numberSprites[Mathf.Clamp(currentFrame, 0, numberSprites.Length - 1)];
    }
}