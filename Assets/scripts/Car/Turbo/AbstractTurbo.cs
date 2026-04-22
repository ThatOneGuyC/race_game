using System.Collections;
using UnityEngine;

// Extend this class to create a new type of turbo
// Override the Use() function to apply logic when player is using turbo

[RequireComponent(typeof(BaseCarController))]
public abstract class Turbo : MonoBehaviour
{
    [Tooltip("How strong the turbo is")]
    [SerializeField] protected float strength = 10f;
    [Tooltip("Maximum amount of turbo")]
    [SerializeField] protected float maxAmount = 100f;
    [Tooltip("Starting % amount of turbo.")]
    [Range(0f, 100f)]
    [SerializeField] protected float startingAmount = 100.0f;
    [Tooltip("How much turbo is consumed per second")]
    [SerializeField] protected float consumeRate = 10f;
    [Tooltip("How much turbo is regenerated per second")]
    [SerializeField] protected float regenerationRate = 10f;
    [Tooltip("How long to wait to start recharging turbo")]
    [SerializeField] protected float waitTime = 1f;
    protected WaitForSeconds waiter;
    protected float amount;
    protected BaseCarController carController;
    protected Coroutine turboCoroutine;

    // Used for running the specific turbo's logic when the player wants to use turbo.
    protected abstract void Use();

    protected virtual void Awake()
    {
        carController = GetComponent<BaseCarController>();

        amount = startingAmount / 100f * maxAmount;
        waiter = new WaitForSeconds(waitTime);
    }

    public virtual void Activate()
    {
        if (amount <= 0) return;

        carController.isTurboActive = true;
        StopCoroutine(turboCoroutine);
        turboCoroutine = StartCoroutine(Consume());
    }

    public virtual void Stop()
    {
        carController.isTurboActive = false;
        StopCoroutine(turboCoroutine);
        turboCoroutine = StartCoroutine(Regenerate());
    }


    protected virtual IEnumerator Consume()
    {
        while (amount > 0)
        {
            amount = Mathf.Lerp(amount, 0, consumeRate * Time.deltaTime);
            Use();
            yield return null;
        }

        Stop();
        yield break;
    }

    protected virtual IEnumerator Regenerate()
    {
        yield return waiter;

        while (amount < maxAmount)
        {
            amount = Mathf.Lerp(amount, maxAmount, regenerationRate * Time.deltaTime);
            yield return null;
        }
        yield break;
    }
}
